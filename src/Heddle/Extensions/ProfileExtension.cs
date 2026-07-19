using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>Compile-time directive: <c>@profile(){{html}}</c> / <c>@profile(){{text}}</c> sets the effective
    /// output profile for output compiled after it in document order (bodies, partials, and imports created
    /// afterwards inherit it via the <see cref="Heddle.Runtime.CompileContext"/> lineage).</para>
    /// <para>The value is read from the body, trimmed, and matched ordinal-case-insensitively against
    /// <c>text</c>/<c>html</c>. Emits nothing; the block is removed from the document. Stateless — safe for
    /// concurrent renders like every directive extension.</para>
    /// </summary>
    [ExtensionName("profile")]
    public class ProfileExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.CompileScope == null)
                throw new ArgumentNullException(nameof(initContext.CompileScope));
            base.InitStart(initContext, dataType, chainedType, parent);
            var value = (GetInnerResult(Scope.Null) ?? string.Empty).Trim();

            OutputProfile profile;
            if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase))
                profile = OutputProfile.Text;
            else if (string.Equals(value, "html", StringComparison.OrdinalIgnoreCase))
                profile = OutputProfile.Html;
            else
            {
                initContext.CompileScope.CompileErrors.Add(
                    $"Unknown output profile '{value}'. Valid values: text, html.".ToError(Position,
                        HeddleDiagnosticIds.UnknownOutputProfile));
                return null;
            }

            var context = initContext.CompileScope.CompileContext;
            if (context.UnnamedOutputCompiled)
            {
                initContext.CompileScope.CompileWarnings.Add(new HeddleCompileWarning
                {
                    Error = "@profile() appears after output has already been compiled; earlier output keeps the previous profile.",
                    Fix = "Move @profile() to the top of the template.",
                    Position = Position,
                    DiagnosticId = HeddleDiagnosticIds.ProfileDirectiveAfterOutput
                });
            }

            context.OutputProfile = profile;
            return null;
        }

        public override object ProcessData(in Scope scope)
        {
            return null;
        }

        public override void RenderData(in Scope scope)
        {
        }
    }
}
