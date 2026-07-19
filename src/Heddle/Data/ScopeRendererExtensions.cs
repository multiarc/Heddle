using System;

namespace Heddle.Data
{
    /// <summary>
    /// Sink-agnostic write helpers for extension authors (phase 8 D10). The single funnel through which the formatter
    /// built-ins (and custom extensions formatting values) reach the renderer, so the per-TFM span/UTF-8 ladder lives
    /// in exactly one place. Stateless.
    /// </summary>
    public static class ScopeRendererExtensions
    {
        /// <summary>Span write against any renderer: dispatches to <see cref="ISpanScopeRenderer"/> when implemented,
        /// otherwise materializes the string (the documented downlevel cost for external renderers that never learned
        /// spans — every engine renderer implements <see cref="ISpanScopeRenderer"/> after phase 8).</summary>
        public static void Render(this IScopeRenderer renderer, ReadOnlySpan<char> data)
        {
            if (renderer is ISpanScopeRenderer s)
                s.Render(data);
            else
                renderer.Render(data.ToString());
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Formats <paramref name="value"/> directly into the renderer with no intermediate string on the fast tiers:
        /// <c>IUtf8SpanFormattable</c> into a UTF-8 sink (net8+ builds), else <c>ISpanFormattable</c> into a stackalloc
        /// char span, else <c>ToString(format, provider)</c>. Output characters are identical on every tier — only the
        /// allocation profile differs. <c>where T : struct, ISpanFormattable</c> guarantees JIT specialization (the
        /// capability test devirtualizes for the concrete value type) and forbids null flowing into the format path.
        /// </summary>
        public static void Render<T>(this IScopeRenderer renderer, T value, string format,
            IFormatProvider formatProvider) where T : struct, ISpanFormattable
        {
#if NET8_0_OR_GREATER
            // UTF-8 tier: format straight to bytes into the sink, no string, no char round-trip. The `is` test's box is
            // eliminated by the JIT's specialized generic instantiation for a value-type T, so this allocates zero.
            if (renderer is IUtf8ScopeRenderer u8 && value is System.IUtf8SpanFormattable u8Formattable)
            {
                Span<byte> utf8Buffer = stackalloc byte[256];
                if (u8Formattable.TryFormat(utf8Buffer, out int u8Written, format.AsSpan(), formatProvider))
                {
                    u8.RenderUtf8(utf8Buffer.Slice(0, u8Written));
                    return;
                }
                // Pathological (destination too small) — fall through to the char-span tier.
            }
#endif
            // Char-span tier: all NET6_0_OR_GREATER builds.
            Span<char> buffer = stackalloc char[256];
            if (value.TryFormat(buffer, out int written, format.AsSpan(), formatProvider))
            {
                renderer.Render(buffer.Slice(0, written));
                return;
            }

            // String tier: the exact pre-phase call, the semantic definition of the other two tiers.
            renderer.Render(value.ToString(format, formatProvider));
        }
#endif
    }
}
