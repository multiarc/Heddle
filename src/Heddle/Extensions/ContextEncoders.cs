using System;
using System.Globalization;
using System.Text;

namespace Heddle.Extensions
{
    /// <summary>
    /// C2 — allocation-conscious escapers for the context-encoding extensions (<c>@attr</c>, <c>@js</c>,
    /// <c>@url</c>). Every method scans first and returns the original string reference unchanged when no character
    /// needs escaping; a <see cref="StringBuilder"/> is allocated only when at least one character must be rewritten
    /// (C2-R1). netstandard2.0-compatible (no span APIs).
    /// </summary>
    internal static class ContextEncoders
    {
        private const string HexUpper = "0123456789ABCDEF";

        // JS line terminators that terminate a string literal / statement in a browser but are not C0 controls.
        private const char LineSeparator = (char)0x2028;
        private const char ParagraphSeparator = (char)0x2029;

        /// <summary>Stringifies a non-null value for a context encoder: a string passes through unchanged, anything
        /// else is rendered with <see cref="Convert.ToString(object, IFormatProvider)"/> under the invariant culture
        /// (C2-R1, matching <c>@string</c>).</summary>
        internal static string Stringify(object model)
            => model as string ?? Convert.ToString(model, CultureInfo.InvariantCulture);

        /// <summary>
        /// HTML-attribute context (C2-R2). Escapes exactly <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>"</c>,
        /// <c>'</c> — the attribute-significant characters including both quote styles, so the output is safe in
        /// single- and double-quoted attribute values alike. (Not a strict superset of the default HTML encoder,
        /// which also escapes <c>'</c> and the Latin-1 160-255 range.)
        /// </summary>
        internal static string EscapeAttribute(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            int i = FindFirstAttribute(value);
            if (i < 0)
                return value;

            var sb = new StringBuilder(value.Length + 16);
            sb.Append(value, 0, i);
            for (; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&#39;"); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }

        private static int FindFirstAttribute(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '&' || c == '<' || c == '>' || c == '"' || c == '\'')
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// JS string-literal contents (C2-R3). Produces the <i>contents</i> of a string literal — the caller writes
        /// the surrounding quotes. Escapes <c>\</c>, <c>"</c>, <c>'</c>, backtick, U+000A→<c>\n</c>, U+000D→<c>\r</c>,
        /// U+2028/U+2029 (JS line terminators), <c>&lt;</c>→<c><</c> (blocks <c>&lt;/script&gt;</c>),
        /// <c>&amp;</c>→<c>&</c>, and every remaining C0 control as <c>\u00XX</c>.
        /// </summary>
        internal static string EscapeJs(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            int i = FindFirstJs(value);
            if (i < 0)
                return value;

            var sb = new StringBuilder(value.Length + 16);
            sb.Append(value, 0, i);
            for (; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\'': sb.Append("\\'"); break;
                    case '`': sb.Append("\\`"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '<': sb.Append("\\u003C"); break;
                    case '&': sb.Append("\\u0026"); break;
                    case LineSeparator: sb.Append("\\u2028"); break;
                    case ParagraphSeparator: sb.Append("\\u2029"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u00");
                            sb.Append(HexUpper[(c >> 4) & 0xF]);
                            sb.Append(HexUpper[c & 0xF]);
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            return sb.ToString();
        }

        private static int FindFirstJs(string value)
        {
            for (int i = 0; i < value.Length; i++)
                if (JsNeedsEscape(value[i]))
                    return i;

            return -1;
        }

        private static bool JsNeedsEscape(char c)
        {
            switch (c)
            {
                case '\\':
                case '"':
                case '\'':
                case '`':
                case '\n':
                case '\r':
                case '<':
                case '&':
                case LineSeparator:
                case ParagraphSeparator:
                    return true;
                default:
                    return c < 0x20;
            }
        }

        /// <summary>
        /// URL component context (C2-R4): the <see cref="Uri.EscapeDataString(string)"/> semantics (RFC 3986 —
        /// unreserved characters kept, everything else percent-encoded, UTF-8 based). Encodes a single component
        /// (a query value or path segment), not a whole URL. Returns the original reference when every character is
        /// unreserved (the case in which <see cref="Uri.EscapeDataString(string)"/> is the identity).
        /// </summary>
        internal static string EscapeUrl(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            for (int i = 0; i < value.Length; i++)
                if (!IsUnreserved(value[i]))
                    return Uri.EscapeDataString(value);

            return value;
        }

        private static bool IsUnreserved(char c)
            => (c >= 'A' && c <= 'Z')
               || (c >= 'a' && c <= 'z')
               || (c >= '0' && c <= '9')
               || c == '-' || c == '.' || c == '_' || c == '~';
    }
}
