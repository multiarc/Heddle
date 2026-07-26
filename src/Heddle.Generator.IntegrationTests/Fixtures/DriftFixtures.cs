using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>Container for the <b>nested</b>-type AQN drift fixture. Reflection spells the nested
    /// type <c>Ns.DriftContainer+NestedYellExtension</c>; the generator's Roslyn
    /// <c>FullyQualifiedFormat</c> spelling is <c>Ns.DriftContainer.NestedYellExtension</c> — the two identity
    /// strings never match, so the gauntlet's extension check rejects the entry on every request.</summary>
    public static class DriftContainer
    {
        /// <summary>A plain extension (no compile-time hook override) that happens to be a nested type.</summary>
        [ExtensionName("driftnested")]
        public sealed class NestedYellExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) =>
                (scope.ModelData?.ToString() ?? string.Empty).ToUpperInvariant();

            public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>Base of the inherited-<c>[ExtensionName]</c> drift pair. Declares the name; the runtime
    /// reads <c>[ExtensionName]</c> with <c>inherit: true</c>, so the subclass below claims the same name and
    /// (being assignable to this type) replaces it in the live table.</summary>
    [ExtensionName("driftbase")]
    public class DriftBaseExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => scope.ModelData?.ToString() ?? string.Empty;

        public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
    }

    /// <summary>Subclass carrying <b>no declared</b> <c>[ExtensionName]</c>. The runtime registers it under
    /// <c>"driftbase"</c> (inherited attribute + <c>IsAssignableFrom</c> override); the generator's declared-only
    /// attribute read never sees it and binds the base — manifest says <c>DriftBaseExtension</c>, live says
    /// <c>DriftInheritedExtension</c>.</summary>
    public sealed class DriftInheritedExtension : DriftBaseExtension
    {
    }
}
