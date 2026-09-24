using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Tests
{
    /// <summary>
    /// The corpus's custom-extension population for the compiled-form gates. These are the extensions the
    /// <c>ext-*</c> fixtures call, carrying real <c>InitStart</c>/<c>ProcessData</c>/<c>RenderData</c> bodies
    /// under their real call names, so the build-tier classification the gates assert observes the real hook paths.
    /// <para>Registered via <see cref="TemplateFactory.AddExtensions"/> like <c>BranchTestExtensions</c>,
    /// not by assembly scan: the registry is process-wide, and an assembly attribute would take these
    /// names for every suite in the process.</para>
    /// </summary>
    internal static class CorpusExtensionFixtures
    {
        private static readonly object Gate = new object();
        private static bool _registered;

        public static void Register()
        {
            lock (Gate)
            {
                if (_registered)
                    return;
                Add("hooked", typeof(CorpusHookedExtension));
                Add("bellow", typeof(CorpusBellowExtension));
                Add("scanner", typeof(CorpusScannerExtension));
                Add("shroud", typeof(CorpusShroudExtension));
                _registered = true;
            }
        }

        private static void Add(string name, Type type)
        {
            if (!TemplateFactory.Exists(name))
                TemplateFactory.AddExtensions(new[] { new ExtensionType(name, type, false) });
        }
    }

    /// <summary>A custom extension that overrides the compile-time hook <c>InitStart</c> with a pass-through
    /// body. The build does not evaluate that override, and no longer needs to: the generated static
    /// initializer constructs this type inside the consumer's assembly and calls the real method, so a
    /// bodied call to it records no refusal.</summary>
    [ExtensionName("hooked")]
    public sealed class CorpusHookedExtension : AbstractExtension
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
    /// An extension whose author has declared that a static initializer cannot reproduce what its hook does:
    /// its <c>InitStart</c> counts the enclosing document's rendering chains, and no call site can carry that
    /// document. The declaration is what keeps the seam from handing it a zero it would believe.
    /// <para><see cref="Observed"/> is the observation, exposed so a test can read it; what the extension
    /// <b>renders</b> is deliberately independent of it, because a fixture whose bytes differ between tiers would
    /// be asserting that a fallback is allowed to change output, and it is not.</para>
    /// </summary>
    [ExtensionName("scanner")]
    [PrecompileUnsupported("reads the enclosing document through InitContext.ParseContext")]
    public sealed class CorpusScannerExtension : AbstractExtension
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
    /// default body against the CALLER's scope and does nothing else, exactly as the engine's own encoders do.
    /// The build reads none of that out of metadata and no longer needs to: the hook runs for real at static-init,
    /// so a bodied call to it records no refusal — the third-party case the binding seam exists for.</summary>
    [ExtensionName("bellow")]
    public sealed class CorpusBellowExtension : AbstractExtension
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

    /// <summary>The bodied twin of <see cref="CorpusScannerExtension"/>: the same author declaration that a
    /// static initializer cannot reproduce its hook, but it renders its own body under the caller's scope (the
    /// always-true <c>@if</c> shape). A refusal site needs a body that reaches the output before what the
    /// recompiled fragment resolves inside that body — an <c>@&lt;&lt;</c> import, a root-rooted read — can be
    /// byte-compared against the dynamic tier at all.</summary>
    [ExtensionName("shroud")]
    [PrecompileUnsupported("reads the enclosing document through InitContext.ParseContext")]
    public sealed class CorpusShroudExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope) => GetInnerResult(scope.Parent());

        public override void RenderData(in Scope scope)
        {
            RenderInnerResult(scope.Parent());
        }
    }
}
