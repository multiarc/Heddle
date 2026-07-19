using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>JavaScript string-literal context encoder (C2-R3). <c>@js(value)</c> escapes its value for the
    /// <i>contents</i> of a JS string literal — the author writes the surrounding quotes. Escapes <c>\</c>, <c>"</c>,
    /// <c>'</c>, backtick, U+000A, U+000D, U+2028, U+2029, <c>&lt;</c> (blocks <c>&lt;/script&gt;</c>), <c>&amp;</c>,
    /// and all C0 controls.</para>
    /// <para>Value-call semantics mirror <c>@string</c>: the parameter is the value; a <c>null</c> value renders the
    /// body-as-default in the caller's (parent) scope if a body exists, else empty. Non-string values are stringified
    /// with <see cref="Convert.ToString(object, IFormatProvider)"/> under the invariant culture before escaping.</para>
    /// <para>Emitted raw with respect to the Html profile (it targets a <c>&lt;script&gt;</c>/handler context where
    /// HTML entity encoding would corrupt the value): the same raw-leaf pathway <c>@raw</c> uses — a plain
    /// <see cref="AbstractExtension"/> with no <see cref="EncodeOutputAttribute"/>, so it is never entity-encoded.</para>
    /// </summary>
    [ExtensionName("js")]
    [DataType(typeof(string))]
    public class JsExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            // Step-back body: the default body is compiled against the caller's (parent) scope, mirroring @string.
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
                return GetInnerResult(scope.Parent());

            return ContextEncoders.EscapeJs(ContextEncoders.Stringify(model));
        }

        public override void RenderData(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
            {
                RenderInnerResult(scope.Parent());
                return;
            }

            scope.Renderer.Render(ContextEncoders.EscapeJs(ContextEncoders.Stringify(model)));
        }
    }
}
