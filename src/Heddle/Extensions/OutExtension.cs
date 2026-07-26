using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Language;
using Heddle.Runtime.Expressions;
using Heddle.Strings.Core;

namespace Heddle.Extensions
{
    [ExtensionName("out")]
    public class OutExtension : AbstractExtension
    {
        private bool _slotMode;
        private bool _composedGuard;

        /// <summary>Puts this pre-constructed carrier in slot-projection mode, reproducing the
        /// <c>_slotMode</c> flag <see cref="InitStart"/> derives from <c>CompileContext.SlotParameterType</c> (the
        /// InitStart <see cref="Heddle.Precompiled.PrecompiledRuntime.BindDefinition"/> bypasses). Called only from a
        /// generated static initializer via <c>PrecompiledRuntime.BindOut</c>; never mutated after.</summary>
        internal void SetPrecompiledSlotMode() => _slotMode = true;

        private const string GuardMessage =
            "'@out' expected the definition's projected content on the chained channel; it cannot take a value after a chained call.";

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            var compileContext = initContext.CompileScope.CompileContext;
            var slotType = compileContext.SlotParameterType;
            var source = initContext.SourceItem;
            bool hasValue = source != null && HasOutValue(source.CallParameter);
            bool hasBody = !string.IsNullOrEmpty(initContext.ParameterTemplate);

            if (slotType != null)
            {
                // In slot-declaring contexts, every @out must pass a value; its type must be assignable to the slot type.
                _slotMode = true;
                _composedGuard = source != null && source.IsChainedConsumer;

                if (!hasValue)
                {
                    compileContext.CompileErrors.Add(
                        $"A definition with a slot parameter (out:: {slotType}) requires every '@out' in its body to pass a value: '@out(expr)'."
                            .ToError(source?.Position ?? Position, HeddleDiagnosticIds.SlotValueRequired));
                }
                else
                {
                    if (hasBody)
                    {
                        compileContext.CompileErrors.Add(
                            "'@out' cannot take both a slot value and a body."
                                .ToError(source.Position, HeddleDiagnosticIds.SlotValueWithBody));
                    }

                    if (dataType == null || dataType.IsDynamic)
                    {
                        compileContext.CompileErrors.Add(
                            "The slot value must have a static type, but it is 'dynamic' here."
                                .ToError(source.Position, HeddleDiagnosticIds.SlotValueTypeMismatch));
                    }
                    else if (!PropConversion.CanConvert(dataType, slotType, allowBoxToObject: false))
                    {
                        compileContext.CompileErrors.Add(
                            $"The slot value type {dataType.Type} is not assignable to the declared slot parameter type {slotType.Type}."
                                .ToError(source.Position, HeddleDiagnosticIds.SlotValueTypeMismatch));
                    }
                }

                base.InitStart(initContext, chainedType, parent, null);
                return typeof(string);
            }

            if (hasValue)
            {
                // @out(value) requires a slot parameter; different error message inside vs outside definition body.
                bool insideDefinition = initContext.ParseContext != null && initContext.ParseContext.InDefintionContext;
                var message = insideDefinition
                    ? "'@out' with a value requires the enclosing definition to declare a slot parameter: '<name(out:: Type)>'."
                    : "'@out' with a value is only valid inside a definition body.";
                compileContext.CompileErrors.Add(
                    message.ToError(source.Position, HeddleDiagnosticIds.SlotValueWithoutSlot));
            }

            base.InitStart(initContext, chainedType, parent, null);
            return chainedType;
        }

        public override object ProcessData(in Scope scope)
        {
            if (_slotMode)
            {
                if (_composedGuard || !(scope.SlotCarrier is SlotContent carrier))
                    throw new TemplateProcessingException(GuardMessage);
                var projectionScope = carrier.InvocationScope.Model(scope.ModelData);
                return carrier.Outer.RenderCallerContent(projectionScope);
            }

            if (!InnerExist)
                return scope.ChainedData;

            var innerScope = scope.Model(scope.ChainedData, scope.ParentModelData);
            return GetInnerResult(innerScope);
        }

        public override void RenderData(in Scope scope)
        {
            if (_slotMode)
            {
                if (_composedGuard || !(scope.SlotCarrier is SlotContent carrier))
                    throw new TemplateProcessingException(GuardMessage);
                var projectionScope = carrier.InvocationScope.Model(scope.ModelData);
                carrier.Outer.RenderCallerContentInto(projectionScope);
                return;
            }

            if (!InnerExist)
            {
                // Static-only body is inert, so emit chained value; stringify non-strings to avoid silent drops.
                var chained = scope.ChainedData;
                scope.Renderer.Render(chained is string chainedString ? chainedString : chained?.ToString());
                return;
            }

            var innerScope = scope.Model(scope.ChainedData, scope.ParentModelData);
            RenderInnerResult(innerScope);
        }

        /// <summary>The canonical five-way test lives in the shared <see cref="SlotRules"/> so the build tier and the
        /// runtime cannot drift apart; this stays as the extension's own vocabulary.</summary>
        private static bool HasOutValue(CallParameter callParameter) => SlotRules.HasOutValue(callParameter);
    }
}
