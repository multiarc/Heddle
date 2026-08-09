using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;

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

    /// <summary>A custom extension that overrides the compile-time hook <c>InitStart</c>. The build does not
    /// evaluate that override, and no longer needs to: the generated static initializer constructs this type inside
    /// the consumer's assembly and calls the real method, so the call precompiles in a default build and the
    /// <c>HED7015</c> warning it once carried is absent. Exported (see <c>BranchRoleExtensions.cs</c>), so both
    /// tiers can render it.</summary>
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

    /// <summary>
    /// An extension whose author has declared that a static initializer cannot reproduce what its hook does — the
    /// <c>[PrecompileUnsupported]</c> case, and its primary population. Its <c>InitStart</c> counts the enclosing
    /// document's rendering chains, and no call site can carry that document: the token stream <i>is</i> the parse
    /// tree, so a synthesized parse context is the one place the binding seam presents an empty member rather than
    /// an absent one. The declaration is what keeps the seam from handing it a zero it would believe.
    /// <para><see cref="Observed"/> is the observation, exposed so a test can read it; what the extension
    /// <b>renders</b> is deliberately independent of it, because a fixture whose bytes differ between tiers would
    /// be asserting that a fallback is allowed to change output, and it is not.</para>
    /// </summary>
    [ExtensionName("scanner")]
    [PrecompileUnsupported("reads the enclosing document through InitContext.ParseContext")]
    public sealed class ScannerExtension : AbstractExtension
    {
        /// <summary>Chains the hook saw in the enclosing document — non-zero under a real compile, zero under a
        /// synthesized call-site context.</summary>
        public int Observed { get; private set; }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            foreach (var chain in initContext.ParseContext.OutputChains)
                if (chain.Chain != null)
                    Observed += chain.Chain.Count;
            return base.InitStart(initContext, dataType, chainedType, parent);
        }

        public override object ProcessData(in Scope scope) => "scanned:" + (scope.ModelData ?? string.Empty);

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>A referenced third-party extension with the step-back hook shape: its <c>InitStart</c> re-types the
    /// default body against the CALLER's scope and does nothing else, exactly as the engine's own encoders do. The
    /// build reads none of that out of metadata and no longer needs to: the hook runs for real at static-init, so a
    /// bodied call to it precompiles — the third-party case the binding seam exists for.</summary>
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
