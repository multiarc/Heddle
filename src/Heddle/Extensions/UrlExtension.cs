using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>URL-component context encoder (C2-R4). <c>@url(value)</c> percent-encodes its value with
    /// <see cref="Uri.EscapeDataString(string)"/> semantics (RFC 3986). It encodes a single <i>component</i> — a query
    /// value or path segment — not a whole URL.</para>
    /// <para>Value-call semantics mirror <c>@string</c>: the parameter is the value; a <c>null</c> value renders the
    /// body-as-default in the caller's (parent) scope if a body exists, else empty. Non-string values are stringified
    /// with <see cref="Convert.ToString(object, IFormatProvider)"/> under the invariant culture before escaping.</para>
    /// <para>Its output contains no HTML-special characters by construction; it is marked
    /// <see cref="EncodeOutputAttribute"/> as an encoding leaf like <c>@attr</c>, for uniformity — its named-call
    /// output is never re-encoded by the Html profile.</para>
    /// </summary>
    [ExtensionName("url")]
    [DataType(typeof(string))]
    [EncodeOutput]
    public class UrlExtension : AbstractExtension
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

            return ContextEncoders.EscapeUrl(ContextEncoders.Stringify(model));
        }

        public override void RenderData(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
            {
                RenderInnerResult(scope.Parent());
                return;
            }

            scope.Renderer.Render(ContextEncoders.EscapeUrl(ContextEncoders.Stringify(model)));
        }
    }
}
