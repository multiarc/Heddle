using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>HTML-attribute encoder: escapes <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>"</c>, <c>'</c> for safe attribute use.
    /// Mirrors <c>@string</c> semantics and null-handling. Marked <see cref="EncodeOutputAttribute"/> so the compiler
    /// skips HED2003 re-encoding.</summary>
    [ExtensionName("attr")]
    [DataType(typeof(string))]
    [EncodeOutput]
    public class AttrExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            // Default body compiles against parent scope, like @string.
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
                return GetInnerResult(scope.Parent());

            return ContextEncoders.EscapeAttribute(ContextEncoders.Stringify(model));
        }

        public override void RenderData(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
            {
                RenderInnerResult(scope.Parent());
                return;
            }

            scope.Renderer.Render(ContextEncoders.EscapeAttribute(ContextEncoders.Stringify(model)));
        }
    }
}
