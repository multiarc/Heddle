using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

// YellExtension is exported to the dynamic backend via the combined [assembly: ExportExtensions(...)] in
// BranchRoleExtensions.cs (ExportExtensions is AllowMultiple=false, so a single assembly-level list carries every
// exported fixture type). HookedExtension exists solely for the build-time HED7015 refusal (never registered).

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>A plain custom extension — overrides only <c>ProcessData</c>/<c>RenderData</c>, no compile-time hook —
    /// so <c>PrecompiledRuntime.Bind</c> reproduces its behavior exactly and both backends render byte-identically.</summary>
    [ExtensionName("yell")]
    public sealed class YellExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope)
        {
            var text = scope.ModelData?.ToString() ?? string.Empty;
            return text.ToUpperInvariant() + "!";
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>A custom extension that overrides the compile-time hook <c>InitStart</c> — the generator cannot
    /// evaluate that logic at build time, so binding it is refused with <c>HED7015</c> (D22). Not exported: it exists
    /// only as a build-visible <c>[ExtensionName]</c> type for the diagnostic fixture.</summary>
    [ExtensionName("hooked")]
    public sealed class HookedExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, dataType, chainedType, parent);
        }

        public override object ProcessData(in Scope scope) => scope.ModelData;

        public override void RenderData(in Scope scope)
        {
            if (scope.ModelData != null)
                scope.Renderer.Render(scope.ModelData.ToString());
        }
    }
}
