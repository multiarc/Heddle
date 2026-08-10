extern alias gen;
using System.Linq;
using System.Reflection;
using Xunit;
using BodyModelRules = gen::Heddle.Language.BodyModelRules;
using BodyModelSource = gen::Heddle.Language.BodyModelSource;
using CallTargetRules = gen::Heddle.Language.CallTargetRules;
using ParticipantScan = gen::Heddle.Language.ParticipantScan;
using SlotRules = gen::Heddle.Language.SlotRules;
using TemplateEmitter = gen::Heddle.Generator.Emit.TemplateEmitter;

namespace Heddle.Generator.Tests
{
    /// <summary>The extraction acceptance: asserts structurally that generator-side copies are deleted.
    /// Also pins body-model-typing conformance: emitter branches must match the runtime table.</summary>
    public class EmitterSharedRuleAdoptionTests
    {
        // Pins by source text inputs, not names, so re-implementation cannot hide by renaming. Loop shape is not
        // a member: reflection cannot reliably detect duplication.

        private static string EmitterSource()
        {
            var dir = System.IO.Path.GetDirectoryName(typeof(EmitterSharedRuleAdoptionTests).Assembly.Location);
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++, dir = System.IO.Path.GetDirectoryName(dir))
            {
                var candidate = System.IO.Path.Combine(dir, "Heddle.Generator", "Emit", "TemplateEmitter.cs");
                if (System.IO.File.Exists(candidate))
                    return System.IO.File.ReadAllText(candidate);
            }

            Assert.Fail("TemplateEmitter.cs was not found by walking up from " +
                        typeof(EmitterSharedRuleAdoptionTests).Assembly.Location +
                        ". This gate reads the emitter's source and must not be skipped.");
            return null;
        }

        /// <summary>Statements of the emitter's source, split coarsely on <c>;</c> — enough to attribute a token to
        /// the call it participates in, which is all these pins need.</summary>
        private static string[] EmitterStatements() => EmitterSource().Split(';');

        /// <summary>
        /// The scope-channel predicate — the <em>only</em> input a participant scan can be written from — is read off
        /// the binder in exactly one place, and every use of it is either the shared <see cref="ParticipantScan"/> or
        /// the <c>DocumentShaper</c> hand-off, which forwards it into the same shared scan. A private re-implementation
        /// under any name would have to appear here as a third consumer, or as a second read of <c>Info.HasScopeChannel</c>.
        /// </summary>
        [Fact]
        public void TheEmitterHasNoPrivateParticipantScan()
        {
            var source = EmitterSource();
            Assert.Equal(1, CountOf(source, "i.HasScopeChannel"));

            var consumers = EmitterStatements()
                .Where(s => s.Contains("HasScopeChannel"))
                .Where(s => !s.Contains("private bool HasScopeChannel("))
                .ToList();
            Assert.All(consumers, s => Assert.True(
                s.Contains("ParticipantScan.") || s.Contains("DocumentShaper.Shape("),
                "The scope-channel predicate is consumed by something other than the shared scan — a private "
                + "participant scan has come back (under any name). Statement: " + s.Trim()));
            Assert.Equal(3, consumers.Count);   // 2 shared-scan call sites + 1 DocumentShaper hand-off (bodies)

            Assert.NotNull(typeof(ParticipantScan).GetMethod("BodyHostsParticipant",
                BindingFlags.Static | BindingFlags.NonPublic));
            Assert.NotNull(typeof(ParticipantScan).GetMethod("ChainHostsParticipant",
                BindingFlags.Static | BindingFlags.NonPublic));
        }

        /// <summary>
        /// A slot-type base-chain walk needs <c>DefinitionItem.SlotTypeName</c>, and an <c>@out</c>-value approximation
        /// needs <c>CallParameter.IsModelTypeParameter</c> as its first term. The emitter reads <c>SlotTypeName</c>
        /// nowhere — only <c>SlotRules.SlotTypeName</c> exists — and every <c>IsModelTypeParameter</c> read belongs to
        /// a call-shape decision, not to an out-value test.
        /// </summary>
        [Fact]
        public void TheEmitterHasNoPrivateSlotWalk()
        {
            var source = EmitterSource();

            // Reading the declared slot type off a layer is what the walk does; the shared rule is the only reader.
            // Counted as whole identifiers: a member whose name merely BEGINS with one of these tokens
            // (Info.HasSlotProjection) is a different name, and a substring count would read it as a private walk.
            Assert.Equal(CountOf(source, "SlotRules.SlotTypeName"), CountIdentifier(source, "SlotTypeName"));
            Assert.Equal(CountOf(source, "SlotRules.HasSlot"), CountIdentifier(source, "HasSlot"));

            // No statement may combine IsModelTypeParameter with the other four out-value carriers — that
            // conjunction IS the approximation SlotRules.HasOutValue replaced.
            Assert.All(EmitterStatements().Where(s => s.Contains("IsModelTypeParameter")), s => Assert.False(
                s.Contains("NativeExpression") || s.Contains("ChainParameter") || s.Contains("CSharpExpression"),
                "An @out-value approximation has been rebuilt in the emitter. Statement: " + s.Trim()));

            Assert.NotNull(typeof(SlotRules).GetMethod("HasOutValue", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.NotNull(typeof(SlotRules).GetMethod("SlotTypeName", BindingFlags.Static | BindingFlags.NonPublic));
        }

        /// <summary>
        /// A manifest binding row's type and assembly come from <c>ExtensionBinder.Info</c> — <c>BareTypeName</c>
        /// carries the metadata <c>+</c> for a nested type and <c>AssemblyName</c> the real assembly — never from
        /// stripping <c>global::</c> off a display name with the assembly defaulted to <c>"Heddle"</c>. That spelling
        /// produces <c>Ns.Outer.Inner, Heddle</c> where the gauntlet computes <c>Ns.Outer+Inner, &lt;asm&gt;</c>;
        /// it was unreachable only because the four engine branch-role extensions are top-level. The helper is deleted,
        /// and this keeps it deleted — the defect is a *shape*, so the source is where it is visible.
        /// </summary>
        [Fact]
        public void ManifestTypeNamesComeFromTheBinderNotFromStringSurgery()
        {
            var source = EmitterSource();
            Assert.Equal(0, CountOf(source, "StartsWith(\"global::\""));
            Assert.Equal(0, CountOf(source, "Substring(\"global::\".Length)"));
            Assert.Equal(0, CountOf(source, "StripGlobal"));
        }

        private static int CountOf(string text, string token)
        {
            int n = 0;
            for (int at = text.IndexOf(token, System.StringComparison.Ordinal); at >= 0;
                 at = text.IndexOf(token, at + token.Length, System.StringComparison.Ordinal))
                n++;
            return n;
        }

        /// <summary>Whole-identifier occurrences: the token neither continues nor is continued by an identifier
        /// character. A plain substring count answers "is this name written here" with yes for every longer name
        /// that starts the same way, which is a different member and not the re-implementation being pinned.</summary>
        private static int CountIdentifier(string text, string identifier) =>
            System.Text.RegularExpressions.Regex.Matches(text,
                "(?<![A-Za-z0-9_])" + System.Text.RegularExpressions.Regex.Escape(identifier) +
                "(?![A-Za-z0-9_])").Count;

        /// <summary>The precedence classifier is linked into the generator and reachable from it — the
        /// no-Roslyn/netstandard2.0 constraint is enforced by this project compiling at all.</summary>
        [Fact]
        public void TheClassifierIsLinkedIntoTheGenerator()
        {
            Assert.NotNull(typeof(CallTargetRules).GetMethod("ResolveCallTarget",
                BindingFlags.Static | BindingFlags.NonPublic));
            Assert.Equal("Heddle.Generator", typeof(CallTargetRules).Assembly.GetName().Name);
        }

        /// <summary>Every shared file this phase added compiles into <b>both</b> assemblies — the linked-source
        /// mechanism working, and the netstandard2.0/no-Roslyn constraint holding for each of them.</summary>
        [Theory]
        [InlineData("Heddle.Language.ParticipantScan")]
        [InlineData("Heddle.Language.SlotRules")]
        [InlineData("Heddle.Language.CallTargetRules")]
        [InlineData("Heddle.Language.BodyModelRules")]
        [InlineData("Heddle.Language.Expressions.EmbeddedCSharpNames")]
        [InlineData("Heddle.Data.OutputProfileRules")]
        [InlineData("Heddle.Data.RenderTypeRules")]
        public void EverySharedRuleFileExistsInBothAssemblies(string typeName)
        {
            Assert.NotNull(typeof(TemplateEmitter).Assembly.GetType(typeName));
            Assert.NotNull(typeof(HeddleTemplate).Assembly.GetType(typeName));
        }

        // Build-tier conformance: TemplateEmitter.TryNestedBodyContext derives nested body context from the row.
        // Tests run the generator and read emitted bytes: row changes change bytes.

        private const string ModelSource =
            "namespace RuleAdoption { public class Person { public string Name { get; set; } " +
            "public int[] Scores { get; set; } public string[] Tags { get; set; } } }";

        private const string ModelCast = "(global::RuleAdoption.Person)scope.ModelData";

        /// <summary>The text from the first nested body class onward. Nested-body classes are emitted after
        /// <c>Body0</c> (the document root), and their numbering carries gaps: a body that compiles to no
        /// processors is handed to its call site as <c>null</c> and its class is never written, so the first
        /// nested class is found by scanning rather than by naming <c>Body1</c>.</summary>
        private static string NestedBodySource(string template)
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/rule-adoption.heddle", "@model(){{RuleAdoption.Person}}@\\\n" + template) },
                new[] { ModelSource });
            var source = run.GeneratedSourceTexts.FirstOrDefault(s => NestedBodyIndex(s) >= 0);
            Assert.NotNull(source);   // a degraded template emits no nested body at all — a deleted row shows up here
            return source.Substring(NestedBodyIndex(source));
        }

        /// <summary>The offset of the first <c>class Body&lt;n&gt;</c> other than the document root's, or -1.</summary>
        private static int NestedBodyIndex(string source)
        {
            var match = System.Text.RegularExpressions.Regex.Match(source, @"class Body(?!0\b)\d+");
            return match.Success ? match.Index : -1;
        }

        /// <summary>The <see cref="BodyModelSource.Parent"/> rows, read off the emitted bytes: a branch body and a
        /// <c>@for</c> body keep the ENCLOSING model, so the generated body class casts <c>scope.ModelData</c> to
        /// the document's model type exactly as the root body does.</summary>
        [Theory]
        [InlineData("@if(Name){{@(Name)}}")]
        [InlineData("@ifnot(Name){{@(Name)}}")]
        [InlineData("@if(Name){{x}}@elif(Name){{@(Name)}}")]
        [InlineData("@if(Name){{x}}@elseif(Name){{@(Name)}}")]
        [InlineData("@if(Name){{x}}@else(){{@(Name)}}")]
        [InlineData("@for(2){{@(Name)}}")]
        public void TheEmitterTypesAParentRowBodyByTheEnclosingModel(string template)
        {
            Assert.Contains(ModelCast, NestedBodySource(template));
        }

        /// <summary>The <see cref="BodyModelSource.ElementOfData"/> row, read off the emitted bytes: an
        /// <c>@list</c> element body is typed by the ELEMENT type, not the enclosing model —
        /// <c>ListExtension.InitStart</c> hands the body the collection's <c>IEnumerable&lt;T&gt;</c> argument and
        /// the engine compiles it in a scope of that type. This is the row whose confusion with Parent would
        /// silently bind a member of the wrong type; the two casts are textually unmistakable.</summary>
        [Fact]
        public void TheEmitterTypesAListElementBodyByTheElementType()
        {
            // The body reads Length — a member of the ELEMENT type. Under Parent it would be looked for on Person.
            var body = NestedBodySource("@list(Tags){{@(Length)}}");
            Assert.DoesNotContain(ModelCast, body);
            Assert.Contains("(string)scope.ModelData", body);
        }

        /// <summary>The same row from the refusing side, which is what stops the row above from being satisfied by
        /// typing the body as anything at all: a member the ELEMENT type does not carry is refused, though the
        /// enclosing model carries it. The engine raises <c>HED0001</c> on the element type for this template, so
        /// nothing is emitted here either.</summary>
        [Fact]
        public void AMemberOfTheEnclosingModelIsNotFoundOnTheElement()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/rule-adoption.heddle", "@model(){{RuleAdoption.Person}}@\\\n@list(Scores){{@(Name)}}") },
                new[] { ModelSource });

            Assert.Contains(run.GeneratorDiagnostics,
                d => d.Id == gen::Heddle.Data.HeddleDiagnosticIds.BuildUnresolvableMember);
            Assert.DoesNotContain(run.GeneratedSourceTexts, s => s.Contains("class Body1"));
        }

        /// <summary>The table's key set, spelled out. It is everything the build knows about extension hooks, so
        /// a row appearing or disappearing changes which templates precompile by default and
        /// must be a reviewed change rather than a diff nobody read. The nine step-back encoders joined the four
        /// the emitter used to name in a private list: they share one hook body, and five of them were absent from
        /// that list for no reason but its length.</summary>
        [Fact]
        public void TheTablePinsExactlyTheNamesTheEmitterEmitsBodiesFor()
        {
            Assert.Equal(
                new[]
                {
                    "attr", "date", "elif", "else", "elseif", "for", "guid", "if", "ifnot", "int", "js", "list",
                    "money", "string", "time", "url"
                },
                BodyModelRules.PinnedNames.OrderBy(n => n, System.StringComparer.Ordinal).ToArray());
        }

        /// <summary>The definition/caller/region rows, which are not keyed by an extension name.</summary>
        [Fact]
        public void TheDefinitionSideRowsAreStated()
        {
            Assert.Equal(BodyModelSource.Declared, BodyModelRules.DefinitionBody);
            Assert.Equal(BodyModelSource.SlotOrData, BodyModelRules.CallerContent);
            Assert.Equal(BodyModelSource.DeclaredOrParent, BodyModelRules.RegionBody);
        }

        /// <summary>Zero-output classification runs through the binder, so a custom <c>[ZeroOutput]</c> extension is
        /// classified too; the hard-coded directive-name list survives only as the unresolvable-name fallback.</summary>
        [Fact]
        public void ZeroOutputClassificationIsBinderBacked()
        {
            var info = typeof(gen::Heddle.Generator.Emit.ExtensionBinder.Info);
            Assert.NotNull(info.GetProperty("IsZeroOutput", BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
