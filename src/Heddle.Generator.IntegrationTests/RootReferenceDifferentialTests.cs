using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The first differential coverage for <c>::</c>-rooted paths. Both tiers root them at the TEMPLATE's own
    /// model — the engine at <c>RootScopeType</c>, generated code at <c>PrecompiledRuntime.RootModel</c> cast to
    /// the template's <c>@model</c> — so a <c>::</c> read inside a definition body typed by another model still
    /// reads the root, and a null hop along the way defaults exactly as a model-rooted hop does.
    /// </summary>
    public class RootReferenceDifferentialTests
    {
        private const string HostType = "Heddle.Generator.IntegrationTests.Fixtures.RootHost";
        private const string ChildType = "Heddle.Generator.IntegrationTests.Fixtures.RootChild";

        private static RootHost Host(RootChild child = null) =>
            new RootHost { Name = "root", Count = 7, Child = child };

        [Fact]
        public void RootPathAtTemplateScope_PrecompilesAndMatches()
        {
            var t = "@model(){{" + HostType + "}}@\\\nname: @(::Name), count: @(::Count)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("root/at-scope.heddle", t, typeof(RootHost), Host());
            Assert.Equal(dyn, precompiled);
            Assert.Contains("name: root, count: 7", dyn);
        }

        [Fact]
        public void RootOperandInANativeExpression_PrecompilesAndMatches()
        {
            var t = "@model(){{" + HostType + "}}@\\\nvalue: @(::Count * 2 + 1)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("root/native.heddle", t, typeof(RootHost), Host());
            Assert.Equal(dyn, precompiled);
            Assert.Contains("value: 15", dyn);
        }

        /// <summary>The inheritance lane: the definition body is typed <c>:: RootChild</c> and both models carry a
        /// <c>Name</c>, so the rendered pair proves the bare path read the body's model while <c>::</c> read the
        /// outer template's root.</summary>
        [Fact]
        public void DefinitionBodyInheritsTheOuterRoot()
        {
            var t = "@model(){{" + HostType + "}}@\\\n" +
                    "@%<row>{{[@(Name)|@(::Name)]}} :: " + ChildType + "%@\n" +
                    "@row(Child)\n";
            var model = Host(new RootChild { Name = "child", Amount = 3 });
            var (precompiled, dyn) = DifferentialHarness.Render("root/def-member.heddle", t, typeof(RootHost), model);
            Assert.Equal(dyn, precompiled);
            Assert.Contains("[child|root]", dyn);
        }

        [Fact]
        public void RootOperandInsideADefinitionBodyExpression()
        {
            var t = "@model(){{" + HostType + "}}@\\\n" +
                    "@%<sum>{{@(::Count + Amount)}} :: " + ChildType + "%@\n" +
                    "@sum(Child)\n";
            var model = Host(new RootChild { Amount = 3 });
            var (precompiled, dyn) = DifferentialHarness.Render("root/def-native.heddle", t, typeof(RootHost), model);
            Assert.Equal(dyn, precompiled);
            Assert.Contains("10", dyn);
        }

        /// <summary>A multi-hop <c>::</c> path over a nullable reference hop, in both tiers' shapes: the member
        /// tier and a native expression over the same hops. The null lanes matter most — the engine defaults the
        /// failed hop and keeps walking, and the generated chain must do the same off the root read.</summary>
        [Theory]
        [InlineData(0, "none")]
        [InlineData(1, "null-child")]
        [InlineData(2, "full")]
        [InlineData(3, "null-root")]
        public void MultiHopRootPathWithANullableHop(int lane, string tag)
        {
            RootHost model;
            switch (lane)
            {
                case 0: model = Host(); break;
                case 1: model = Host(new RootChild { Name = null, Amount = 2 }); break;
                case 2: model = Host(new RootChild { Name = "x", Amount = 2 }); break;
                default: model = null; break;
            }

            var t = "@model(){{" + HostType + "}}@\\\n" +
                    "child: @(::Child.Name), amount: @(::Child.Amount), sum: @(::Child.Amount + 1)\n";
            var (precompiled, dyn) = DifferentialHarness.Render("root/hop-" + tag + ".heddle", t,
                typeof(RootHost), model);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A <c>::</c> value as a definition prop argument — the dynamic-setter path, which evaluates the
        /// argument against the caller's scope but roots the read at the template's model on both tiers.</summary>
        [Fact]
        public void RootReferencePropArgument_PrecompilesAndMatches()
        {
            var t = "@model(){{" + HostType + "}}@\\\n" +
                    "@% <panel(style: string)>{{[@(style)]}} :: " + ChildType + " %@\n" +
                    "@panel(Child, style: ::Name)\n";
            var model = Host(new RootChild { Name = "child" });
            var (precompiled, dyn) = DifferentialHarness.Render("root/prop-arg.heddle", t, typeof(RootHost), model);
            Assert.Equal(dyn, precompiled);
            Assert.Contains("[root]", dyn);
        }

        /// <summary>An untyped template keeps <c>::</c> on the dynamic member tier: the engine compiles a
        /// <c>RootDynamicParameter</c> and the generated code walks the same per-segment DLR chain off the root
        /// read, so the pair must agree without any static root type.</summary>
        [Fact]
        public void UntypedTemplateRootPath_PrecompilesAndMatches()
        {
            var t = "name: @(::Name)\n";
            var (precompiled, dyn) = DifferentialHarness.RenderWithOptions("root/untyped.heddle", t, null,
                Host(), new TemplateOptions());
            Assert.Equal(dyn, precompiled);
            Assert.Contains("name: root", dyn);
        }
    }
}
