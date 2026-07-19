using System.Text;

namespace Heddle.Generator.Emit
{
    /// <summary>The single static-piece emission hook (phase 7 D15). Every static piece is emitted as the string
    /// constant, and — when <c>HeddleEmitUtf8Pieces=true</c> and the language version supports it and the piece has
    /// no unpaired surrogate (HED7005) — the compiler-embedded <c>"…"u8</c> twin. Because both forms come from this
    /// one hook, the same generated type serves the v1 string path and phase 8's byte sink.</summary>
    internal static class PieceWriter
    {
        /// <summary>Emits <c>internal const string Pn = "…";</c> and, when enabled, the u8 twin.</summary>
        public static void EmitPiece(CodeWriter w, int index, string piece, bool emitUtf8, bool utf8Supported)
        {
            var literal = Escape(piece);
            w.Line($"internal const string P{index} = {literal};");
            if (emitUtf8 && utf8Supported && !HasLoneSurrogate(piece))
                w.Line($"internal static global::System.ReadOnlySpan<byte> P{index}U8 => {literal}u8;");
        }

        /// <summary>Escapes a string as a regular (non-verbatim) C# string literal. Quotes, backslashes and control
        /// characters are escaped; the u8 twin reuses the identical text with the <c>u8</c> suffix, so both encode
        /// the same code points.</summary>
        public static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    case '\a': sb.Append("\\a"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\v': sb.Append("\\v"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int) c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        public static bool HasLoneSurrogate(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                        return true;
                    i++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
