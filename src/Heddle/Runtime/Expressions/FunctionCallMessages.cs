using System.Collections.Generic;
using System.Linq;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// The function-call compile-error sentences, stated once. Three writers share them: the dynamic
    /// compiler's expression tier (<see cref="NativeExpressionCompiler"/>), its top-level call dispatch
    /// (<c>HeddleCompiler.CompileItem</c>), and the precompiled tier's late-bound call site
    /// (<see cref="Precompiled.PrecompiledFunctionSite"/>), whose failure must be byte-identical to the engine's —
    /// which is only structural while the sentence has one home.
    /// </summary>
    internal static class FunctionCallMessages
    {
        public static string UnknownFunction(string name) =>
            $"Cannot find extension or registered function '{name}'. Register it with TemplateOptions.Functions, or check the name.";

        public static string ExtensionCalledAsFunction(string name) =>
            $"'{name}' is an extension, not a registered function — extensions cannot be called inside a native expression. Use a call chain, or register a function with TemplateOptions.Functions.";

        public static string AmbiguousFunctionCall(string name, IReadOnlyList<FunctionEntry> overloads) =>
            $"The call to function '{name}' is ambiguous between: {Candidates(overloads)}.";

        public static string NoFunctionOverload(string name, string argTypes, IReadOnlyList<FunctionEntry> overloads) =>
            $"No overload of function '{name}' takes ({argTypes}). Candidates: {Candidates(overloads)}.";

        private static string Candidates(IReadOnlyList<FunctionEntry> overloads) =>
            string.Join(", ", overloads.Select(o => o.ToSignatureString()));
    }
}
