using Heddle.Data;
using Heddle.Language;
using Heddle.Precompiled;
using Heddle.Runtime;

namespace Heddle.Core
{
    /// <summary>
    /// <para>The render-time decorator of a parameter-declaring extension. Binds the call
    /// site's <c>[Prop]</c> values (the dynamic tier's <see cref="PropsBinder"/>, or the precompiled frozen
    /// prototype + <see cref="PrecompiledPropSetter"/>s) and installs the parameter frame
    /// (<see cref="Scope.WithExtensionParameters"/>) before delegating to the inner user extension.</para>
    /// <para><b>Attribute-transparent (security-sensitive):</b> the carrier carries neither
    /// <c>[EncodeOutput]</c> nor <c>[NotEncode]</c>, so it must never be the type the compiler's attribute
    /// reflection observes — the inner is fully initialized (render type, <c>InitStart</c>, deferred
    /// <c>CompleteInit</c> registration) <i>before</i> it is wrapped, and the reflection sites
    /// (<c>WarnOnRedundantEncoding</c>, <c>RuntimeDocument.ItemNeedsLocals</c>) unwrap through
    /// <see cref="Inner"/>. The hook forwards below are defensive only.</para>
    /// <para>Thread-safety: all state is written once at compile and never mutated; per-render state rides the
    /// <see cref="Scope"/> lineage. Concurrent renders share the immutable map and, for all-constant sites, the
    /// frozen values array.</para>
    /// </summary>
    internal sealed class ExtensionParameterCarrier : AbstractExtension
    {
        private readonly IExtension _inner;
        private readonly ExtensionParameterMap _map;
        private readonly PropsBinder _binder;
        private readonly object[] _prototype;
        private readonly PrecompiledPropSetter[] _setters;

        internal ExtensionParameterCarrier(IExtension inner, PropsBinder binder, ExtensionParameterMap map)
        {
            _inner = inner;
            _binder = binder;
            _map = map;
            Position = (inner as AbstractExtension)?.Position ?? Position;
        }

        internal ExtensionParameterCarrier(IExtension inner, object[] prototype, PrecompiledPropSetter[] setters,
            ExtensionParameterMap map)
        {
            _inner = inner;
            _prototype = prototype;
            _setters = setters;
            _map = map;
            Position = (inner as AbstractExtension)?.Position ?? Position;
        }

        /// <summary>The wrapped user extension; the carrier stays transparent to attribute reflection.</summary>
        internal IExtension Inner => _inner;

        private object[] BindValues(in Scope scope)
        {
            if (_binder != null)
                return _binder.Bind(scope);
            if (_prototype == null)
                return null;
            if (_setters == null || _setters.Length == 0)
                return _prototype;

            var values = (object[]) _prototype.Clone();
            var callerView = scope.Parent();
            foreach (var setter in _setters)
                values[setter.Index] = setter.Evaluate(callerView);
            return values;
        }

        public override object ProcessData(in Scope scope)
        {
            var values = BindValues(scope);
            if (values == null)
                return _inner.ProcessData(scope);
            return _inner.ProcessData(scope.WithExtensionParameters(values, _map));
        }

        public override void RenderData(in Scope scope)
        {
            var values = BindValues(scope);
            if (values == null)
            {
                _inner.RenderData(scope);
                return;
            }

            _inner.RenderData(scope.WithExtensionParameters(values, _map));
        }

        public override void SetUpRenderType(RenderType renderType) => _inner.SetUpRenderType(renderType);

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
            => _inner.InitStart(initContext, dataType, chainedType, parent);

        public override void CompleteInit(CompileScope newScope, ParseContext parseContext)
            => _inner.CompleteInit(newScope, parseContext);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
