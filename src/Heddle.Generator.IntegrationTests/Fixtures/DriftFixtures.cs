using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>Nested-type AQN drift: reflection vs. Roslyn spelling differ (+ vs. .), causing extension rejection.</summary>
    public static class DriftContainer
    {
        /// <summary>Plain nested extension, no compile-time hook override.</summary>
        [ExtensionName("driftnested")]
        public sealed class NestedYellExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) =>
                (scope.ModelData?.ToString() ?? string.Empty).ToUpperInvariant();

            public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>Base of inherited-name drift pair: declares name that subclass inherits and overrides.</summary>
    [ExtensionName("driftbase")]
    public class DriftBaseExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => scope.ModelData?.ToString() ?? string.Empty;

        public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
    }

    /// <summary>Subclass with no declared [ExtensionName]: inherits parent name at runtime but generator sees only declaration.</summary>
    public sealed class DriftInheritedExtension : DriftBaseExtension
    {
    }
}
