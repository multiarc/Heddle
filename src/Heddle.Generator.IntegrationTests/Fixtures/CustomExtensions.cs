using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

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
    /// evaluate that logic at build time, so the call site degrades to the dynamic tier under the <c>HED7015</c>
    /// <b>warning</b> rather than failing the build. Exported (see <c>BranchRoleExtensions.cs</c>), so the dynamic
    /// tier the degrade routes to can actually render it.</summary>
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

    /// <summary>A referenced third-party extension with the step-back hook shape: its <c>InitStart</c> re-types the
    /// default body against the CALLER's scope and does nothing else, exactly as the engine's own encoders do. The
    /// build cannot read that out of metadata, so without hook probing a bodied call degrades under HED7015; with
    /// probing it precompiles, and this fixture is the third-party case the whole probe exists for.</summary>
    [ExtensionName("bellow")]
    public sealed class BellowExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            var model = scope.ModelData;
            var text = model == null ? GetInnerResult(scope.Parent())?.ToString() : model.ToString();
            return (text ?? string.Empty).ToUpperInvariant();
        }

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessData(scope));
        }
    }
}
