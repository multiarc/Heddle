extern alias gen;
using System.Linq;
using System.Reflection;
using Xunit;
using BodyModelRules = gen::Heddle.Language.BodyModelRules;
using BodyModelSource = gen::Heddle.Language.BodyModelSource;
using ChainedModelSource = gen::Heddle.Language.ChainedModelSource;
using CallTargetRules = gen::Heddle.Language.CallTargetRules;
using ParticipantScan = gen::Heddle.Language.ParticipantScan;
using SlotRules = gen::Heddle.Language.SlotRules;
using TemplateEmitter = gen::Heddle.Generator.Emit.TemplateEmitter;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Generator plan phase 1 — the build-tier half of the extraction acceptance. Each extraction's done-when is
    /// "the generator-side copy is deleted", which is a claim about the emitter's <em>shape</em> rather than about
    /// any rendered byte, so it is asserted here structurally. The byte-neutrality half is the unchanged
    /// snapshot/golden/differential suites.
    /// <para>Also carries the build-tier side of the WI10 body-model-typing conformance: the emitter's pinned
    /// emission branches must declare the same rows the runtime conforms to, asserted over the table rather than
    /// over emitted text.</para>
    /// </summary>
    public class EmitterSharedRuleAdoptionTests
    {
        private static MethodInfo[] EmitterMethods() =>
            typeof(TemplateEmitter).GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>WI4: the generator has zero private participant scans — both probes now call the shared
        /// <see cref="ParticipantScan"/>.</summary>
        [Fact]
        public void TheEmitterHasNoPrivateParticipantScan()
        {
            Assert.DoesNotContain(EmitterMethods(), m => m.Name == "ScanHostsParticipant");
            Assert.NotNull(typeof(ParticipantScan).GetMethod("BodyHostsParticipant",
                BindingFlags.Static | BindingFlags.NonPublic));
            Assert.NotNull(typeof(ParticipantScan).GetMethod("ChainHostsParticipant",
                BindingFlags.Static | BindingFlags.NonPublic));
        }

        /// <summary>WI5: the emitter's two copies of the slot-type base-chain walk are gone; the canonical
        /// five-way <c>HasOutValue</c> exists once, in the shared file.</summary>
        [Fact]
        public void TheEmitterHasNoPrivateSlotWalk()
        {
            Assert.DoesNotContain(EmitterMethods(), m => m.Name == "DefinitionHasSlot");
            Assert.NotNull(typeof(SlotRules).GetMethod("HasOutValue", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.NotNull(typeof(SlotRules).GetMethod("SlotTypeName", BindingFlags.Static | BindingFlags.NonPublic));
        }

        /// <summary>WI7: the precedence classifier is linked into the generator and reachable from it — the
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

        /// <summary>WI10, build-tier conformance: the rows the emitter's pinned emission branches are written
        /// against. The branch trio and <c>@for</c> type their bodies by the ENCLOSING model, <c>@list</c> by the
        /// element type — the three choices whose confusion silently changes which member the emitted C# binds.</summary>
        [Theory]
        [InlineData("if", BodyModelSource.Parent, ChainedModelSource.None)]
        [InlineData("ifnot", BodyModelSource.Parent, ChainedModelSource.None)]
        [InlineData("elif", BodyModelSource.Parent, ChainedModelSource.None)]
        [InlineData("elseif", BodyModelSource.Parent, ChainedModelSource.None)]
        [InlineData("else", BodyModelSource.Parent, ChainedModelSource.None)]
        [InlineData("for", BodyModelSource.Parent, ChainedModelSource.Int32Index)]
        [InlineData("list", BodyModelSource.ElementOfData, ChainedModelSource.None)]
        internal void TheEmittersPinnedBranchesCiteTheTableRows(string name, BodyModelSource body,
            ChainedModelSource chained)
        {
            Assert.True(BodyModelRules.TryGet(name, out var actualBody, out var actualChained));
            Assert.Equal(body, actualBody);
            Assert.Equal(chained, actualChained);
        }

        /// <summary>The definition/caller/region rows, which are not keyed by an extension name.</summary>
        [Fact]
        public void TheDefinitionSideRowsAreStated()
        {
            Assert.Equal(BodyModelSource.Declared, BodyModelRules.DefinitionBody);
            Assert.Equal(BodyModelSource.SlotOrData, BodyModelRules.CallerContent);
            Assert.Equal(BodyModelSource.DeclaredOrParent, BodyModelRules.RegionBody);
        }

        /// <summary>WI9: zero-output classification runs through the binder, so a custom <c>[ZeroOutput]</c>
        /// extension is classified too; the hard-coded directive-name list survives only as the unresolvable-name
        /// fallback.</summary>
        [Fact]
        public void ZeroOutputClassificationIsBinderBacked()
        {
            var info = typeof(gen::Heddle.Generator.Emit.ExtensionBinder.Info);
            Assert.NotNull(info.GetProperty("IsZeroOutput", BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
