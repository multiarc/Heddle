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

        /// <summary>Phase 7 slots: puts this pre-constructed carrier in slot-projection mode, reproducing the
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
                // Slot-declaring definition body (D11): every @out must pass a value; a slot-mode @out is
                // bodiless; the value's static type must be assignable to the slot type (rows 1–4, no boxing).
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
                // @out with a value where no slot parameter is declared (D13) — including the formerly
                // accepted-and-ignored @out(X)/@out(true). Two message forms: inside vs outside a definition body.
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
                // Render the chained value's string form. A boxed non-string (e.g. the int index a counted
                // @for(...) threads on the chained channel) must stringify, matching ProcessData which returns
                // the raw object for the process path to stringify — a plain 'as string' silently dropped it.
                var chained = scope.ChainedData;
                scope.Renderer.Render(chained is string chainedString ? chainedString : chained?.ToString());
            }

            var innerScope = scope.Model(scope.ChainedData, scope.ParentModelData);
            RenderInnerResult(innerScope);
        }

        private static bool HasOutValue(CallParameter callParameter)
        {
            if (callParameter.NativeExpression != null)
                return true;
            if (callParameter.ChainParameter != null)
                return true;
            if (!string.IsNullOrEmpty(callParameter.CSharpExpression))
                return true;
            if (callParameter.PropArguments != null)
                return true;
            return callParameter.ModelParameter != null && callParameter.ModelParameter.Length > 0 &&
                   !string.IsNullOrEmpty(callParameter.ModelParameter[0]);
        }
    }
}
