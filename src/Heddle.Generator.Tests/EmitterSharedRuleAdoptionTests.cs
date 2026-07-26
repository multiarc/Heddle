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
    /// <summary>
    /// The build-tier half of the extraction acceptance. Each extraction's done-when is "the generator-side copy is
    /// deleted", which is a claim about the emitter's <em>shape</em> rather than about any rendered byte, so it is
    /// asserted here structurally. The byte-neutrality half is the unchanged snapshot/golden/differential suites.
    /// <para>Also carries the build-tier side of body-model-typing conformance: the emitter's pinned emission branches
    /// must declare the same rows the runtime conforms to, asserted over the table rather than over emitted text.</para>
    /// </summary>
    public class EmitterSharedRuleAdoptionTests
    {
        // -------------------------------------------------------------------------------------------------------
        // "The generator-side copy is deleted" used to be asserted by NAME — Assert.DoesNotContain(methods,
        // m => m.Name == "ScanHostsParticipant"). A copy reintroduced under any other name walked straight past it,
        // which is the one failure mode the assertion existed for. The pins below are name-independent: they
        // constrain the *inputs* a re-implementation would necessarily have to read, over the emitter's source.
        //
        // Source text rather than reflection because a duplicated loop is a shape, not a member: IL-level detection
        // needs a full opcode walker for no extra fidelity. Locating the file is the same walk-up
        // CorpusDifferentialTests does for the corpus, and a missing file FAILS rather than skips.
        // -------------------------------------------------------------------------------------------------------

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
            Assert.Equal(3, consumers.Count);   // 2 shared-scan call sites + the DocumentShaper hand-off

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
            Assert.Equal(CountOf(source, "SlotRules.SlotTypeName"), CountOf(source, "SlotTypeName"));
            Assert.Equal(CountOf(source, "SlotRules.HasSlot"), CountOf(source, "HasSlot"));

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

        // -------------------------------------------------------------------------------------------------------
        // WI10, build-tier conformance. What used to stand here was a theory whose InlineData rows were the table's
        // own rows, asserted against the table — tautological, and it never touched the emitter. The emitter's only
        // link to the table was a Debug.Assert, i.e. nothing at all in Release.
        //
        // Both halves are fixed. TemplateEmitter.TryNestedBodyContext now *derives* the nested body's build context
        // from the row (Parent → keep the enclosing typed context; ElementOfData → build on the dynamic tier), and
        // the three arms that emit a pinned body call it, so the row decides emitted bytes. The tests below run the
        // generator and read those bytes back: a row that stopped saying Parent, or one that stopped saying
        // ElementOfData, changes them — and a deleted row degrades the template, which ExpectTypedBody catches too.
        // -------------------------------------------------------------------------------------------------------

        private const string ModelSource =
            "namespace RuleAdoption { public class Person { public string Name { get; set; } " +
            "public int[] Scores { get; set; } } }";

        private const string ModelCast = "(global::RuleAdoption.Person)scope.ModelData";

        /// <summary>The text of the first nested body class in the generated template source. Nested-body
        /// classes are emitted after <c>Body0</c> (the document root), so <c>Body1</c> is the host's body.</summary>
        private static string NestedBodySource(string template)
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("views/rule-adoption.heddle", "@model(){{RuleAdoption.Person}}@\\\n" + template) },
                new[] { ModelSource });
            var source = run.GeneratedSourceTexts.FirstOrDefault(s => s.Contains("class Body1"));
            Assert.NotNull(source);   // a degraded template emits no nested body at all — a deleted row shows up here
            var at = source.IndexOf("class Body1", System.StringComparison.Ordinal);
            return source.Substring(at);
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
        /// <c>@list</c> element body is NOT the enclosing model — the element type is discoverable only through
        /// <c>ListExtension.InitStart</c>'s reflection, so the body is built on the dynamic tier. This is the row
        /// whose confusion with Parent would silently bind a member of the wrong type.</summary>
        [Fact]
        public void TheEmitterTypesAListElementBodyOnTheDynamicTier()
        {
            // The body reads Name — a member of the ENCLOSING model. Under the ElementOfData row the emitter has no
            // element type, so it routes the read through the dynamic member router; under Parent it would bind
            // Person.Name statically and cast. The two emissions are textually unmistakable.
            var body = NestedBodySource("@list(Scores){{@(Name)}}");
            Assert.DoesNotContain(ModelCast, body);
            Assert.Contains("PrecompiledRuntime.DynamicMember(m, \"Name\")", body);
        }

        /// <summary>The table's key set is the emitter's pinned-host set: exactly the names whose bodies the emitter
        /// emits from a row. A row added without an emission arm (or an arm added without a row) shows up here
        /// rather than as a degraded template nobody noticed.</summary>
        [Fact]
        public void TheTablePinsExactlyTheNamesTheEmitterEmitsBodiesFor()
        {
            Assert.Equal(
                new[] { "elif", "else", "elseif", "for", "if", "ifnot", "list" },
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
