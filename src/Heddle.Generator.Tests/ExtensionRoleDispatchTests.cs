using System.Collections.Generic;
using System.Linq;
using Heddle.Generator.Diagnostics;
using Heddle.Generator.Emit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// <b>The dispatch half of the agnosticism rule.</b> The emitter reached its slot-projection and its
    /// child-template route by comparing the call's name against two built-ins, so a third-party extension doing
    /// the identical job — projecting the enclosing definition's slot, or compiling and hosting a template its
    /// body names — was served by neither, for no reason but what it was called. The routes are keyed on
    /// <c>[SlotProjection]</c>/<c>[ChildTemplateHost]</c> read off the bound type instead, and these pin that a
    /// custom extension carrying one arrives at the same route the built-in does.
    /// <para>What is asserted is the <b>route</b>, not a working precompilation: each route is entered with a call
    /// shape it declines, and the decline's category (HED7031's machine-readable half) is one only that route
    /// produces. A control extension carrying no role, called identically, must not produce it. Making these
    /// shapes precompile for a third party is Stage B/C work.</para>
    /// </summary>
    public class ExtensionRoleDispatchTests
    {
        private const string RoleExtensions = @"
[assembly: Heddle.Attributes.ExportExtensions]
namespace Probe
{
    [Heddle.Attributes.ExtensionName(""project"")]
    [Heddle.Attributes.SlotProjection]
    public sealed class ProjectExtension : Heddle.Core.AbstractExtension
    {
        public override object ProcessData(in Heddle.Data.Scope scope) => string.Empty;
        public override void RenderData(in Heddle.Data.Scope scope) { }
    }

    [Heddle.Attributes.ExtensionName(""include"")]
    [Heddle.Attributes.ChildTemplateHost]
    public sealed class IncludeExtension : Heddle.Core.AbstractExtension
    {
        public override object ProcessData(in Heddle.Data.Scope scope) => string.Empty;
        public override void RenderData(in Heddle.Data.Scope scope) { }
    }

    [Heddle.Attributes.ExtensionName(""both"")]
    [Heddle.Attributes.SlotProjection]
    [Heddle.Attributes.ChildTemplateHost]
    public sealed class BothExtension : Heddle.Core.AbstractExtension
    {
        public override object ProcessData(in Heddle.Data.Scope scope) => string.Empty;
        public override void RenderData(in Heddle.Data.Scope scope) { }
    }

    [Heddle.Attributes.ExtensionName(""plain"")]
    public sealed class PlainExtension : Heddle.Core.AbstractExtension
    {
        public override object ProcessData(in Heddle.Data.Scope scope) => string.Empty;
        public override void RenderData(in Heddle.Data.Scope scope) { }
    }
}";

        private static readonly Dictionary<string, string> Root =
            new Dictionary<string, string> { ["build_property.HeddleTemplateRoot"] = "/repo/app" };

        /// <summary>The categories HED7031 recorded for a template, read off the diagnostic's properties rather
        /// than out of its message.</summary>
        private static IReadOnlyList<string> RefusalCategories(string body)
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("/repo/app/role-dispatch.heddle", "@model(){{System.String}}@\\\n" + body + "\n") },
                new[] { RoleExtensions }, Root);

            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            return run.GeneratorDiagnostics
                .Where(d => d.Id == "HED7031")
                .Select(d => d.Properties.TryGetValue(GeneratorDiagnostics.RefusalCategoryProperty, out var c)
                    ? c
                    : "(none)")
                .ToList();
        }

        /// <summary>A bodied call is a shape the slot projection cannot carry, and <c>SlotChannel</c> is reachable
        /// from nowhere else — so a third-party extension declaring the projection reaching it is proof the call
        /// went down the projection route, and the roleless control not reaching it is proof the route is the
        /// declaration's doing and not the shape's.</summary>
        [Fact]
        public void AThirdPartySlotProjectionTakesTheSlotChannelRoute()
        {
            Assert.Contains(RefusalCategories("@project(){{body}}"),
                c => c == RefusalCategory.SlotChannel.ToString());

            Assert.DoesNotContain(RefusalCategories("@plain(){{body}}"),
                c => c == RefusalCategory.SlotChannel.ToString());
        }

        /// <summary>A body that names no template is a shape the child-template host cannot carry, and
        /// <c>PartialName</c> is reachable from nowhere else.</summary>
        [Fact]
        public void AThirdPartyChildTemplateHostTakesTheChildTemplateRoute()
        {
            Assert.Contains(RefusalCategories("@include()"),
                c => c == RefusalCategory.PartialName.ToString());

            Assert.DoesNotContain(RefusalCategories("@plain()"),
                c => c == RefusalCategory.PartialName.ToString());
        }

        /// <summary>Two roles on one type is two incompatible call shapes — the projection takes no body and the
        /// host's body is its child's name — so there is nothing to dispatch to and the emitter refuses rather
        /// than picking whichever it happens to test first.</summary>
        [Fact]
        public void AnExtensionDeclaringBothRolesIsRefusedRatherThanGuessed()
        {
            var run = GeneratorHarness.RunWithSources(
                new[] { ("/repo/app/both-roles.heddle", "@model(){{System.String}}@\\\n@both()\n") },
                new[] { RoleExtensions }, Root);

            var refusal = Assert.Single(run.GeneratorDiagnostics.Where(d => d.Id == "HED7031"));
            Assert.True(refusal.Properties.TryGetValue(GeneratorDiagnostics.RefusalCategoryProperty, out var category));
            Assert.Equal(RefusalCategory.ExtensionBinding.ToString(), category);
            Assert.Contains("both", refusal.GetMessage());
        }
    }
}
