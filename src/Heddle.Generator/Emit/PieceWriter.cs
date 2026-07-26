using Heddle.Language.Expressions;

namespace Heddle.Generator.Emit
{
    /// <summary>Emits static string pieces and optional UTF8 twins. States <i>what</i> a piece emits, never <i>how</i> — escaping and surrogate detection are <see cref="CSharpEscape"/>'s duties.</summary>
    internal static class PieceWriter
    {
        public static void EmitPiece(CodeWriter w, int index, string piece, bool emitUtf8, bool utf8Supported)
        {
            var literal = CSharpEscape.StringLiteral(piece);
            w.Line($"internal const string P{index} = {literal};");
            if (emitUtf8 && utf8Supported && !CSharpEscape.HasLoneSurrogate(piece))
                w.Line($"internal static global::System.ReadOnlySpan<byte> P{index}U8 => {literal}u8;");
        }
    }
}
