using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;

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

    /// <summary>
    /// A third-party <b>slot projection</b>: not derived from the engine's own, not named after it, and carrying
    /// nothing but <c>[SlotProjection]</c>. It wraps the value the call site hands it and renders that; it
    /// declares no slot rules of its own, because the two members the role's contract names —
    /// <c>CompileContext.SlotParameterType</c> and the scope's slot carrier — are engine-internal, so the
    /// compile-time half of the role is not reachable from outside the engine assembly at all.
    /// <para>What it proves is the half that is: a call to it inside a slot-declaring definition body takes the
    /// ordinary bound-extension route and precompiles in a default build. The build no longer applies the
    /// <b>built-in's</b> slot reasoning to it — a valueless call, and a value the declared slot type could not
    /// take, are refusals <c>OutExtension</c> raises for itself and this extension does not, so the engine
    /// compiles both and the precompiled tier has to keep them.</para>
    /// </summary>
    [ExtensionName("project")]
    [SlotProjection]
    public sealed class ProjectExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => "<" + (scope.ModelData ?? "-") + ">";

        public override void RenderData(in Scope scope)
        {
            scope.Renderer.Render((string) ProcessData(scope));
        }
    }

    /// <summary>
    /// A third-party <b>child-template host</b>: not derived from the engine's own, not named after it, and
    /// written the way a package author would write one — the role is the <c>[ChildTemplateHost]</c> declaration
    /// and nothing else. It evaluates its body once at compile time to get the child's name, queues the child's
    /// compile on the enclosing context, takes delivery in <c>CompleteInit</c>, and renders the child in place of
    /// its own body.
    /// <para>What it proves is that the precompiled tier reaches the same machinery for it as for the built-in:
    /// the call precompiles in a default build, the hook runs at static init, and the child arrives through the
    /// engine's child supply — from the registry when it is precompiled, from disk when it is not.</para>
    /// <para>It renders through <c>Generate</c> rather than streaming, because <c>HeddleTemplate.Render</c> into a
    /// caller's renderer is engine-internal. That is the shape a third party actually has, so it is the shape the
    /// fixture uses.</para>
    /// </summary>
    [ExtensionName("include")]
    [ChildTemplateHost]
    public sealed class IncludeExtension : AbstractExtension
    {
        private HeddleTemplate _child;

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, dataType, chainedType, parent);
            initContext.ParameterTemplate = GetInnerResult(Scope.Null);
            var name = initContext.ParameterTemplate?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                initContext.CompileScope.CompileContext.AddDelayedCompileTemplate(
                    new CompileScope(
                        new CompileContext(initContext.CompileScope.CompileContext, dataType, name),
                        initContext.CompileScope.CSharpContext),
                    initContext.ParseContext, this);
            }

            return typeof(string);
        }

        public override void CompleteInit(CompileScope newScope, ParseContext parseContext)
        {
            _child = new HeddleTemplate();
            var result = _child.Compile(newScope.CompileContext);
            if (!result.Success)
                newScope.CompileErrors.AddRange(result.Errors);
        }

        public override object ProcessData(in Scope scope)
            => _child?.Generate(scope.ModelData, scope.ChainedData);

        public override void RenderData(in Scope scope)
        {
            if (_child != null)
                scope.Renderer.Render(_child.Generate(scope.ModelData, scope.ChainedData));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _child?.Dispose();
        }
    }
}
