using System.Text;

namespace Heddle.Language.Expressions
{
    /// <summary>
    /// The single C# <c>string</c>/<c>char</c> literal escape table. Before this file the same alphabet lived in
    /// three disagreeing copies — the generator's per-char <c>EscapeChar</c> (no <c>\a \b \f \v</c>),
    /// <c>PieceWriter.Escape</c> (no <c>'</c>), and the AST decoder's accept set — neither copy guarded lone
    /// surrogates, so unpaired <c>D800–DFFF</c> code units were written raw into generated source. This unifies the
    /// three and adds the lone-surrogate guard.
    /// <para>Emission always uses the shortest canonical form; the decoder still accepts the full documented set
    /// (<c>\' \" \\ \0 \a \b \e \f \n \r \t \v \xH…H \uHHHH \UHHHHHHHH</c>) and is unchanged. Output is
    /// byte-identical to the previous <c>PieceWriter.Escape</c> for every input containing no lone surrogate.</para>
    /// </summary>
    internal static class CSharpEscape
    {
        /// <summary>Escapes <paramref name="value"/> as a regular (non-verbatim) C# string literal,
        /// <b>including</b> the surrounding quotes.</summary>
        public static string StringLiteral(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    // A well-formed pair is one code point: emit both units verbatim so the "…"u8 twin encodes the
                    // identical text.
                    sb.Append(c).Append(value[i + 1]);
                    i++;
                    continue;
                }

                Append(sb, c, inCharLiteral: false);
            }

            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>Escapes <paramref name="value"/> as a C# char literal, <b>including</b> the surrounding
        /// quotes. A lone surrogate is always escaped here — a single <c>char</c> can never form a pair.</summary>
        public static string CharLiteral(char value)
        {
            var sb = new StringBuilder(8);
            sb.Append('\'');
            Append(sb, value, inCharLiteral: true);
            sb.Append('\'');
            return sb.ToString();
        }

        /// <summary>Appends one escaped code unit. <paramref name="inCharLiteral"/> selects which quote needs
        /// escaping; the other one is legal verbatim in that context and is left alone.</summary>
        private static void Append(StringBuilder sb, char c, bool inCharLiteral)
        {
            switch (c)
            {
                case '"':
                    sb.Append(inCharLiteral ? "\"" : "\\\"");
                    return;
                case '\'':
                    sb.Append(inCharLiteral ? "\\'" : "'");
                    return;
                case '\\': sb.Append("\\\\"); return;
                case '\0': sb.Append("\\0"); return;
                case '\a': sb.Append("\\a"); return;
                case '\b': sb.Append("\\b"); return;
                case '\f': sb.Append("\\f"); return;
                case '\n': sb.Append("\\n"); return;
                case '\r': sb.Append("\\r"); return;
                case '\t': sb.Append("\\t"); return;
                case '\v': sb.Append("\\v"); return;
                default:
                    if (c < 0x20 || IsSurrogate(c))
                        sb.Append("\\u").Append(((int) c).ToString("x4"));
                    else
                        sb.Append(c);
                    return;
            }
        }

        private static bool IsSurrogate(char c) => c >= '\uD800' && c <= '\uDFFF';

        /// <summary>Whether <paramref name="value"/> contains an unpaired surrogate code unit. Such a string has no
        /// UTF-8 encoding, so it is ineligible for a <c>"…"u8</c> twin — its <c>string</c> literal is still emitted,
        /// escaped. Defined as the sign of <see cref="IndexOfLoneSurrogate"/> rather than a second loop to ensure
        /// consistency.</summary>
        public static bool HasLoneSurrogate(string value) => IndexOfLoneSurrogate(value) >= 0;

        /// <summary>The char index of the first unpaired UTF-16 surrogate in <paramref name="value"/>, or -1 —
        /// the position <c>HED7005</c> reports. <c>null</c> scans clear. This centralizes a check that previously
        /// had three separate implementations; the escape table above, the generator's <c>PieceWriter</c>, and the
        /// runtime all now share one answer.</summary>
        public static int IndexOfLoneSurrogate(string value)
        {
            if (value == null)
                return -1;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                        return i;
                    i++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
