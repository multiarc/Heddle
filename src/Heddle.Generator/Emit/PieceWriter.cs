using System.Text;
using Heddle.Language.Expressions;

namespace Heddle.Generator.Emit
{
    /// <summary>Emits static string pieces and optional UTF8 twins. States <i>what</i> a piece emits, never <i>how</i> — escaping and surrogate detection are <see cref="CSharpEscape"/>'s duties.</summary>
    internal static class PieceWriter
    {
        public static void EmitPiece(CodeWriter w, int index, string piece, bool emitUtf8, bool utf8Supported,
            bool utf8LiteralSyntax)
        {
            var literal = CSharpEscape.StringLiteral(piece);
            w.Line($"internal const string P{index} = {literal};");
            if (!emitUtf8 || !utf8Supported || CSharpEscape.HasLoneSurrogate(piece))
                return;

            if (utf8LiteralSyntax)
            {
                w.Line($"internal static global::System.ReadOnlySpan<byte> P{index}U8 => {literal}u8;");
                return;
            }

            // A consumer whose language version predates C# 11 cannot parse a u8 suffix. The constant-array
            // ReadOnlySpan property below compiles to the very same metadata blob (Roslyn has emitted constant
            // byte arrays in span position that way since C# 7.3), so only the SOURCE spelling is older — the
            // IL and the render path are identical to the u8 twin.
            var bytes = Encoding.UTF8.GetBytes(piece);
            var line = new StringBuilder();
            line.Append("internal static global::System.ReadOnlySpan<byte> P").Append(index)
                .Append("U8 => new byte[] { ");
            for (var i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                    line.Append(", ");
                line.Append(bytes[i]);
            }

            line.Append(" };");
            w.Line(line.ToString());
        }
    }
}
