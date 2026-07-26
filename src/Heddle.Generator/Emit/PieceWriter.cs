using Heddle.Language.Expressions;

namespace Heddle.Generator.Emit
{
    /// <summary>The single static-piece emission hook (phase 7 D15). Every static piece is emitted as the string
    /// constant, and — when <c>HeddleEmitUtf8Pieces=true</c> and the language version supports it and the piece has
    /// no unpaired surrogate (HED7005) — the compiler-embedded <c>"…"u8</c> twin. Because both forms come from this
    /// one hook, the same generated type serves the v1 string path and phase 8's byte sink.
    /// <para>Phase 6 D11/WI11: this file states <i>what</i> a piece emits, never <i>how</i> a literal is escaped or
    /// how a surrogate is found — both are <see cref="CSharpEscape"/>'s, the one core phase 4 owns. The local
    /// <c>Escape</c>/<c>HasLoneSurrogate</c>/<c>IndexOfLoneSurrogate</c> members were the last generator-side
    /// spellings of that knowledge and are gone.</para></summary>
    internal static class PieceWriter
    {
        /// <summary>Emits <c>internal const string Pn = "…";</c> and, when enabled, the u8 twin.</summary>
        public static void EmitPiece(CodeWriter w, int index, string piece, bool emitUtf8, bool utf8Supported)
        {
            var literal = CSharpEscape.StringLiteral(piece);
            w.Line($"internal const string P{index} = {literal};");
            if (emitUtf8 && utf8Supported && !CSharpEscape.HasLoneSurrogate(piece))
                w.Line($"internal static global::System.ReadOnlySpan<byte> P{index}U8 => {literal}u8;");
        }
    }
}
