extern alias gen;
using System;
using System.Linq;
using System.Reflection;
using Xunit;
using DocumentShaper = gen::Heddle.Generator.Emit.DocumentShaper;
using DocumentShaping = gen::Heddle.Language.DocumentShaping;
using DocumentParser = gen::Heddle.Language.DocumentParser;
using ParserSettings = gen::Heddle.Language.ParserSettings;
using ParseContext = gen::Heddle.Language.ParseContext;
using OutputChain = gen::Heddle.Language.OutputChain;
using OutputItem = gen::Heddle.Language.OutputItem;
using BranchRole = gen::Heddle.Attributes.BranchRole;
using BlockPosition = gen::Heddle.Strings.Core.BlockPosition;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Adapter-level tests for the emitter's shaping driver: the clamp fix at template granularity,
    /// <c>Participant</c> classification, and empty-default-chain alignment. The machine-level pins live in
    /// <see cref="DocumentShapingCharacterizationTests"/>.
    /// </summary>
    public class DocumentShaperAdapterTests
    {
        private static readonly Func<OutputChain, bool> DirectiveIsZeroOutput = chain =>
        {
            var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0].ExtensionName : null;
            return leftmost == "model" || leftmost == "using" || leftmost == "import" || leftmost == "profile";
        };

        /// <summary>
        /// <para>The input class the runtime's clamp was added for, reproduced end to end through the parser: an
        /// <b>indented</b> <c>@&lt;&lt;</c> composition import on the document's last line, importing a file whose
        /// content carries a zero-output directive. The import's re-based chains keep the import-site offset
        /// (<c>HeddleMainListener</c>), the import block's own line is widened away by <c>RemoveDefinitions</c>
        /// under <c>TrimDirectiveLines</c> — leading whitespace included — and the surviving chain's stored start
        /// now overshoots the shortened working document.</para>
        /// <para>Previously threw <c>IndexOutOfRangeException</c> out of <c>WidenToWholeLine</c>, swallowed by
        /// the generator's per-template <c>catch (Exception)</c> into a silent loss of precompilation. Now
        /// the generator clamps exactly as the runtime does and the working document is the runtime's.</para>
        /// </summary>
        [Theory]
        [InlineData("X\n   @<<{{inc.heddle}}\n", "X\n")]   // the overshoot: widened start (2) < chain start (5)
        [InlineData("X\n@<<{{inc.heddle}}\n", "X\n")]      // same shape, no indent — in bounds even before the clamp fix
        [InlineData("   @<<{{inc.heddle}}\nTail\n", "Tail\n")]
        public void OvershootingImportChainsShapeWithoutThrowing(string document, string expectedWorking)
        {
            var settings = new ParserSettings { ImportReader = _ => "@model(System.String)\nBODY\n" };
            var parse = DocumentParser.Parse(document, settings, out var clean);

            var shape = DocumentShaper.Shape(clean, parse, true, DirectiveIsZeroOutput);

            Assert.Equal(expectedWorking, shape.WorkingDocument);
        }

        [Fact]
        public void ScopeChannelNonRoleExtensionClassifiesParticipantAndDisarmsTheStrip()
        {
            // if → participant → elif: the participant disarms, so no gap is collected across it and the document
            // is untouched.
            var parse = new ParseContext();
            AddChain(parse, "if", 0, 2);
            AddChain(parse, "part", 4, 2);
            AddChain(parse, "elif", 8, 2);

            var shape = DocumentShaper.Shape("if  pp  el", parse, false, _ => false,
                isDefinition: _ => false,
                roleOf: n => n == "if" ? BranchRole.Opener : n == "elif" ? (BranchRole?) BranchRole.Continuation : null,
                hasScopeChannel: n => n == "part");

            Assert.Equal("if  pp  el", shape.WorkingDocument);
            Assert.Equal(new[] { 0, 4, 8 }, shape.Elements.Select(e => e.Position.StartIndex).ToArray());
        }

        [Fact] // the classifier really produces Participant (not Other) for a [ScopeChannel] non-role name
        public void ClassifierMapsScopeChannelToParticipant()
        {
            var parse = new ParseContext();
            AddChain(parse, "part", 0, 2);
            var classifier = (Func<OutputChain, DocumentShaping.BranchKind>) typeof(DocumentShaper)
                .GetMethod("ClassifierFor", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[]
                {
                    (Func<string, bool>) (_ => false),
                    (Func<string, BranchRole?>) (_ => null),
                    (Func<string, bool>) (n => n == "part")
                });

            Assert.Equal(DocumentShaping.BranchKind.Participant, classifier(parse.OutputChains[0]));
        }

        /// <summary>
        /// The runtime models an empty default chain as a zero-length <c>DocumentElement</c> with an empty call
        /// chain at document end (<c>HeddleCompiler.CompileBody</c>); the generator used to skip it outright.
        /// Now both model the same element. This turns red if either side reintroduces the skip.
        /// </summary>
        [Fact]
        public void EmptyDefaultChainIsModelledAsAZeroLengthElementAtDocumentEnd()
        {
            var parse = new ParseContext();
            parse.DefaultChains.Add(new OutputChain(parse) { BlockPosition = new BlockPosition(0, 0) });

            var shape = DocumentShaper.Shape("HELLO", parse, false, DirectiveIsZeroOutput);

            var element = Assert.Single(shape.Elements);
            Assert.Equal(shape.WorkingDocument.Length, element.Position.StartIndex);
            Assert.Equal(0, element.Position.Length);
            Assert.Empty(element.Chain.Chain);
        }

        [Fact] // the zero-length element renders nothing: the piece walk still yields exactly the document
        public void TheZeroLengthDefaultElementContributesNoBytes()
        {
            var pieces = new System.Collections.Generic.List<string>();
            DocumentShaping.SlicePieces(new[] { new BlockPosition(5, 0) }, p => p, "HELLO",
                pieces.Add, _ => true);

            Assert.Equal(new[] { "HELLO" }, pieces.ToArray());
        }

        /// <summary>
        /// The emitter keeps <b>no</b> list of directive names. Zero-output classification is read off
        /// <c>[ZeroOutput]</c> through the binder and nothing else, so a custom zero-output extension is classified
        /// on the same terms as a built-in one and the four-name fallback that used to sit behind it — the thing
        /// that made a custom zero-output extension diverge silently — cannot come back under another name.
        /// <para>The names that remain are the language's own, in the engine's shared table: a build cannot bind
        /// anything until it knows the model type, the <c>@using</c> set and the running profile, so those three are
        /// read out of the directive stream at parse level by both tiers. That table is asserted here to hold
        /// exactly three entries, so a fourth name cannot be smuggled in under the word "grammar"; the
        /// <c>@&lt;&lt;</c>-era <c>import</c> spelling is not among them, because the emitter no longer classifies
        /// it at all.</para>
        /// </summary>
        [Fact]
        public void TheEmitterKeepsNoDirectiveNamePredicate()
        {
            var emitter = typeof(gen::Heddle.Generator.Emit.TemplateEmitter);
            Assert.Null(emitter.GetMethod("IsDirectiveName", BindingFlags.NonPublic | BindingFlags.Static));

            var table = typeof(gen::Heddle.Language.DirectiveNames);
            var names = table.GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string) f.GetRawConstantValue())
                .OrderBy(n => n, System.StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(new[] { "model", "profile", "using" }, names);
        }

        private static void AddChain(ParseContext context, string name, int start, int length)
        {
            var chain = new OutputChain(context) { BlockPosition = new BlockPosition(start, length) };
            chain.Chain.Add(new OutputItem(name, new BlockPosition(start, length)));
            context.OutputChains.Add(chain);
        }
    }
}
