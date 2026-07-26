using System;

namespace Heddle.Language
{
    internal enum CallTargetKind
    {
        Fill,
        Definition,
        Extension,
        Function,
        /// <summary>A registered function whose call shape the function tier cannot accept — a chain parameter or
        /// a C# expression. An error on both tiers rather than a fall-through.</summary>
        FunctionShapeUnsupported,
        Unknown
    }

    /// <summary>Name-resolution precedence for standalone calls: ambient fill → definitions → extension → function → unknown.
    /// Extension wins a name collision; the registry invariant (no default-function/extension-name collision) is now an executable lockstep test.</summary>
    internal static class CallTargetRules
    {
        /// <summary>
        /// Classifies <paramref name="name"/> at a call site carrying <paramref name="callParameter"/>.
        /// </summary>
        /// <param name="name">The called name; empty means the unnamed carrier and is never classified here.</param>
        /// <param name="callParameter">The call's parameter, for the function-shape gate.</param>
        /// <param name="fillsContains">Whether an ambient region fill overrides the name here.</param>
        /// <param name="definitionExists">Whether the enclosing parse context resolves the name to a definition.</param>
        /// <param name="isExtension">Whether the name is a registered extension.</param>
        /// <param name="isFunction">Whether the name is a registered function.</param>
        internal static CallTargetKind ResolveCallTarget(string name, CallParameter callParameter,
            Func<string, bool> fillsContains, Func<string, bool> definitionExists, Func<string, bool> isExtension,
            Func<string, bool> isFunction)
        {
            if (string.IsNullOrEmpty(name))
                return CallTargetKind.Unknown;

            if (fillsContains != null && fillsContains(name))
                return CallTargetKind.Fill;
            if (definitionExists != null && definitionExists(name))
                return CallTargetKind.Definition;

            bool nameIsExtension = isExtension != null && isExtension(name);
            bool nameIsFunction = isFunction != null && isFunction(name);

            // Extension beats function: a standalone '@name(...)' resolves to the extension, and the runtime warns
            // (HED-shadowed-by-extension) rather than picking the function.
            if (nameIsExtension)
                return CallTargetKind.Extension;

            if (nameIsFunction)
                return IsFunctionCompatibleShape(callParameter)
                    ? CallTargetKind.Function
                    : CallTargetKind.FunctionShapeUnsupported;

            return CallTargetKind.Unknown;
        }

        /// <summary>The function tier takes native-expression arguments only: a chained extension call or a C#
        /// expression in the parameter is not a shape a registered function can be invoked with.</summary>
        internal static bool IsFunctionCompatibleShape(CallParameter callParameter)
            => callParameter == null ||
               (callParameter.ChainParameter == null && string.IsNullOrEmpty(callParameter.CSharpExpression));
    }
}
