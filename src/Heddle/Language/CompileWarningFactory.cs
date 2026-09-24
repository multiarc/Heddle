using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// The compile-channel warnings, whose <i>text</i> must not be decided twice. Each method below is the one
    /// place a given warning's id, message and fix are written.
    /// <para>The conditions stay at the sites that detect them; only what is said about them lives here.</para>
    /// </summary>
    internal static class CompileWarningFactory
    {
        internal static HeddleCompileWarning ProfileDirectiveAfterOutput(BlockPosition position) =>
            new HeddleCompileWarning
            {
                Error = "@profile() appears after output has already been compiled; earlier output keeps the previous profile.",
                Fix = "Move @profile() to the top of the template.",
                Position = position,
                DiagnosticId = HeddleDiagnosticIds.ProfileDirectiveAfterOutput
            };

        internal static HeddleCompileWarning RedundantEncodingExtension(string producerName, BlockPosition position) =>
            new HeddleCompileWarning
            {
                Error =
                    $"'{producerName}()' output feeds the unnamed @(...) output, which already HTML-encodes under the Html profile — the value is encoded twice.",
                Fix = $"Remove '{producerName}()', or output the trusted value through @raw(...).",
                Position = position,
                DiagnosticId = HeddleDiagnosticIds.RedundantEncodingExtension
            };

        /// <param name="declaredAt">The definition's own span, rendered into the message so the reader can find the
        /// declaration from the call the warning points at.</param>
        internal static HeddleCompileWarning DefinitionRendersTwice(string name, BlockPosition declaredAt,
            BlockPosition position) =>
            new HeddleCompileWarning
            {
                Error =
                    $"Definition '{name}' (declared at {declaredAt}) has a default output ('->') and is also called by name — it renders twice.",
                Fix = "Remove the '->' from the definition, or remove this call.",
                Position = position,
                DiagnosticId = HeddleDiagnosticIds.DefinitionRendersTwice
            };

        internal static HeddleCompileWarning FunctionShadowedByExtension(string name, BlockPosition position) =>
            new HeddleCompileWarning
            {
                Error =
                    $"Registered function '{name}' is shadowed by the extension with the same name; standalone calls '@{name}(...)' resolve to the extension.",
                Fix = $"Rename the function, or invoke it inside an expression: '@( {name}(...) )'.",
                Position = position,
                DiagnosticId = HeddleDiagnosticIds.FunctionShadowedByExtension
            };

        /// <param name="scopeTypeName">The model type as the CLR spells it (<c>Type.ToString()</c>).</param>
        internal static HeddleCompileWarning PropShadowsModelMember(string name, string scopeTypeName,
            BlockPosition position) =>
            new HeddleCompileWarning
            {
                Error = $"Prop '{name}' hides the model member '{scopeTypeName}.{name}' — '{name}' reads the prop.",
                Fix = $"Rename the prop, or read the member explicitly with 'this.{name}' in an expression.",
                Position = position,
                DiagnosticId = HeddleDiagnosticIds.PropShadowsModelMember
            };
    }
}
