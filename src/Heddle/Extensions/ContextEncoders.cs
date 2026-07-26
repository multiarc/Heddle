using System;
using System.Globalization;
using System.Text;

namespace Heddle.Extensions
{
    /// <summary>
    /// Allocation-conscious escapers for context encodings. Returns original reference when no escaping needed
    /// (netstandard2.0 compatible).
    /// </summary>
    internal static class ContextEncoders
    {
        private const string HexUpper = "0123456789ABCDEF";

        // JS line terminators (not C0 controls).
        private const char LineSeparator = (char)0x2028;
        private const char ParagraphSeparator = (char)0x2029;

        /// <summary>Stringifies a value: strings pass through; others use Convert.ToString with invariant culture (matching @string).</summary>
        internal static string Stringify(object model)
            => model as string ?? Convert.ToString(model, CultureInfo.InvariantCulture);

        /// <summary>
        /// HTML-attribute context: escapes &amp;, &lt;, &gt;, both quotes (safe in single/double-quoted attributes).
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
        /// Escapes contents of JS string literals (caller writes quotes): quotes, backslash, backtick,
        /// controls, line terminators, &lt; for &lt;/script&gt;.
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
        /// URL component context (RFC 3986): percent-encodes non-unreserved UTF-8, returns original when unreserved.
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
