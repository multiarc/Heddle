using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>HTML-attribute context encoder (C2-R2). <c>@attr(value)</c> escapes its value for use inside an HTML
    /// attribute (<c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>"</c>, <c>'</c>): it escapes the attribute-significant
    /// characters including both quote styles, so the result is safe in single- and double-quoted attributes alike.
    /// It is not a strict superset of the default HTML encoder — that encoder also escapes <c>'</c> and the Latin-1
    /// 160-255 range (which <c>@attr</c> leaves alone); <c>@attr</c> is simply the encoder tuned for the attribute
    /// context.</para>
    /// <para>Value-call semantics mirror <c>@string</c>: the parameter is the value; a <c>null</c> value renders the
    /// body-as-default in the caller's (parent) scope if a body exists, else empty — that default body renders in the
    /// parent context and is <b>not</b> attribute-escaped (route untrusted values through <c>@attr</c> explicitly).
    /// Non-string values are stringified with <see cref="Convert.ToString(object, IFormatProvider)"/> under the
    /// invariant culture before escaping (dynamic tier; a statically non-string parameter is a compile-time type
    /// error, mirroring <c>@string</c>).</para>
    /// <para>Marked <see cref="EncodeOutputAttribute"/> as an encoding leaf: it emits already-escaped output, so the
    /// compiler recognizes it as an encoder (HED2003 double-encode warning) and its named-call output is never
    /// re-encoded by the Html profile.</para>
    /// </summary>
    [ExtensionName("attr")]
    [DataType(typeof(string))]
    [EncodeOutput]
    public class AttrExtension : AbstractExtension
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
