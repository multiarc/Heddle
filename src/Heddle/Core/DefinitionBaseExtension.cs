using System.Threading;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Precompiled;
using Heddle.Runtime;

namespace Heddle.Core
{
    internal class DefinitionBaseExtension : AbstractExtension
    {
        public DefinitionBaseExtension DefinitionParameterTemplate { get; set; }
        private readonly ThreadLocal<int> _recursionCount = new ThreadLocal<int>();
        private int _maxRecursionCount;

        // Phase 7 D5/D23: the precompiled props carriage. BindDefinition bypasses the InitStart that would build a
        // PropsBinder, so it installs a frozen prototype (+ optional per-invocation dynamic setters) directly.
        private object[] _precompiledPropsPrototype;
        private PrecompiledPropSetter[] _precompiledPropSetters;

        /// <summary>The per-call-site props binder (D7), or <c>null</c> for a prop-less definition (the fast
        /// path — no props code runs). Written once at compile in <c>CreateExtension</c>, read at render.</summary>
        internal PropsBinder PropsBinder { get; set; }

        /// <summary>Phase 7 D23: assigns the recursion limit that <see cref="InitStart"/> would otherwise set from
        /// <c>TemplateOptions.MaxRecursionCount</c>. <see cref="PrecompiledRuntime.BindDefinition"/> bypasses
        /// <c>InitStart</c>, so without this the guard field stays 0 and every precompiled definition throws on its
        /// first invocation. The build's <c>HeddleMaxRecursionCount</c> is baked at each generated call site.</summary>
        internal void SetMaxRecursion(int maxRecursionCount) => _maxRecursionCount = maxRecursionCount;

        /// <summary>Phase 7 D5: installs the precompiled props carriage (frozen prototype + dynamic setters) that the
        /// runtime backend would produce via <c>PropsBinder</c>. The frozen prototype is shared when all-constant;
        /// otherwise it is cloned per invocation and each setter runs against the caller view (phase 5 D8).</summary>
        internal void SetPrecompiledProps(object[] prototype, PrecompiledPropSetter[] setters)
        {
            _precompiledPropsPrototype = prototype;
            _precompiledPropSetters = setters;
        }

        /// <summary>The props array for this invocation: the runtime <see cref="PropsBinder"/> when present (dynamic
        /// path, unchanged), else the precompiled carriage — the frozen prototype shared when there are no setters,
        /// otherwise a per-invocation clone with each setter evaluated against the caller view.</summary>
        private object[] BindPropsForRender(in Scope scope)
        {
            if (PropsBinder != null)
                return PropsBinder.Bind(scope);
            if (_precompiledPropsPrototype == null)
                return null;
            if (_precompiledPropSetters == null || _precompiledPropSetters.Length == 0)
                return _precompiledPropsPrototype;

            var props = (object[]) _precompiledPropsPrototype.Clone();
            var callerView = scope.Parent();
            foreach (var setter in _precompiledPropSetters)
                props[setter.Index] = setter.Evaluate(callerView);
            return props;
        }

        /// <summary>True when the invoked definition declares a slot parameter (D11). In slot mode the caller
        /// content is not pre-rendered; a <see cref="SlotContent"/> carrier is installed instead and the
        /// definition body's <c>@out(expr)</c> renders the caller content lazily per projection.</summary>
        internal bool SlotMode { get; set; }

        /// <summary>True when this definition call is a chain consumer — it has a producer to its right in its own
        /// chain (<c>@box():producer()</c>), so its caller content arrives on the chained channel rather than as a
        /// caller body <c>{{...}}</c>. Set structurally by the compiler for every non-tail chain item. When set and
        /// the call carries no caller body, the definition body's <c>@out()</c> must emit the chained value (the
        /// documented <c>@heading():emphasis()</c> pattern); a plain caller body still wins when present. Gated
        /// structurally so ambient chained data (e.g. the <c>@for</c> loop index) never leaks into a lone
        /// <c>@box()</c>.</summary>
        internal bool ReceivesChainedValue { get; set; }

        /// <summary>Renders the invocation-site caller content (the outer extension's own body) to a string.
        /// The slot-projection entry point for <c>OutExtension</c> (D11, phase 3 funnel entry E4).</summary>
        internal string RenderCallerContent(in Scope scope) => GetInnerResult(scope);

        /// <summary>Renders the invocation-site caller content directly into the renderer (the slot-projection
        /// render-path entry for <c>OutExtension</c>).</summary>
        internal void RenderCallerContentInto(in Scope scope) => RenderInnerResult(scope);

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            _maxRecursionCount = initContext.CompileScope.Options.MaxRecursionCount;
            return base.InitStart(initContext, dataType, chainedType, parent);
        }

        public override object ProcessData(in Scope scope)
        {
            if (_recursionCount.Value >= _maxRecursionCount)
                throw new TemplateProcessingException("Recursion hit it's maximum");
            _recursionCount.Value++;
            var props = BindPropsForRender(scope);
            object chained;
            Scope chainedData;
            if (SlotMode)
            {
                chained = null;
                chainedData = scope.Chain(null).WithSlot(new SlotContent(this, scope));
            }
            else
            {
                // A chained-into definition with no caller body takes its content from the chained channel — the
                // producer to its right (@heading():emphasis()). Otherwise (a caller body, or no producer) the
                // caller body is the content, exactly as @box(){{...}} threads it.
                chained = ReceivesChainedValue && !InnerExist ? scope.ChainedData : GetInnerResult(scope);
                chainedData = scope.Chain(chained);
            }

            if (props != null)
                chainedData = chainedData.WithProps(props);
            var result = DefinitionParameterTemplate?.ProcessData(chainedData) ?? chained;
            _recursionCount.Value--;
            return result;
        }

        public override void RenderData(in Scope scope)
        {
            if (_recursionCount.Value >= _maxRecursionCount)
                throw new TemplateProcessingException("Recursion hit it's maximum");
            _recursionCount.Value++;
            if (DefinitionParameterTemplate != null)
            {
                var props = BindPropsForRender(scope);
                Scope chainedData;
                if (SlotMode)
                {
                    chainedData = scope.Chain(null).WithSlot(new SlotContent(this, scope));
                }
                else
                {
                    // Chained-into definition with no caller body: content is the chained producer's output;
                    // otherwise the caller body. Mirrors ProcessData.
                    var chained = ReceivesChainedValue && !InnerExist ? scope.ChainedData : GetInnerResult(scope);
                    chainedData = scope.Chain(chained);
                }

                if (props != null)
                    chainedData = chainedData.WithProps(props);
                DefinitionParameterTemplate.RenderData(chainedData);
            }
            else
            {
                //var chained = GetInnerResult(ref scope);
                //scope.Render(chained);
                RenderInnerResult(scope);
            }

            _recursionCount.Value--;
        }
    }
}