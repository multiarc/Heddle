using System;
using System.Buffers;
using System.Text;

namespace Heddle.Data
{
    /// <summary>
    /// <see cref="IUtf8ScopeRenderer"/> over a host-supplied <see cref="IBufferWriter{T}"/> of <see cref="byte"/>,
    /// producing UTF-8 (phase 8 D2/D4/D5). Write-through into writer-provided spans; the writer is never completed,
    /// flushed, or disposed — the host owns its lifecycle (e.g. a <c>PipeWriter</c> the host later
    /// <c>FlushAsync</c>es). Single render ownership: not thread-safe; a new instance is constructed per render, so the
    /// lazy <see cref="Encoder"/> is per-render.
    /// </summary>
    public sealed class Utf8ScopeRenderer : IUtf8ScopeRenderer
    {
        // 16 KB (D5): keeps GetSpan requests comfortably inside default pool segment sizes while making the chunked
        // tier rare. Char values up to 5 461 UTF-16 units take the single-call tier (5 461 × 3 = 16 383 ≤ 16 384).
        private const int MaxUtf8SizeHint = 16 * 1024;

        private readonly IBufferWriter<byte> _writer;
        private Encoder _encoder;   // lazily created, per-render (D5/D15); carries a trailing high surrogate between chunks

        public Utf8ScopeRenderer(IBufferWriter<byte> writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
                Render(data.AsSpan());
        }

        public void Render(ReadOnlySpan<char> data)
        {
            if (data.IsEmpty)
                return;
            // 3 is the UTF-8 worst case per UTF-16 code unit (surrogate pairs produce 4 bytes per 2 units, ≤ 3·L), so
            // the single-call tier never splits a surrogate — the whole logical value converts in one call.
            if (data.Length * 3 <= MaxUtf8SizeHint)
                RenderSingle(data);
            else
                RenderChunked(data);
        }

        public void RenderUtf8(ReadOnlySpan<byte> utf8)
        {
            if (utf8.IsEmpty)
                return;
            // Straight copy: loops GetSpan/CopyTo/Advance for segments smaller than the input. No validation — engine
            // callers pass only compiler-validated u8 pieces (D2).
            BuffersExtensions.Write(_writer, utf8);
        }

        private void RenderSingle(ReadOnlySpan<char> chars)
        {
            var span = _writer.GetSpan(chars.Length * 3);
#if NET6_0_OR_GREATER
            int bytesWritten = Encoding.UTF8.GetBytes(chars, span);
            _writer.Advance(bytesWritten);
#else
            unsafe
            {
                fixed (char* c = chars)
                fixed (byte* b = span)
                {
                    int bytesWritten = Encoding.UTF8.GetBytes(c, chars.Length, b, span.Length);
                    _writer.Advance(bytesWritten);
                }
            }
#endif
        }

        private void RenderChunked(ReadOnlySpan<char> chars)
        {
            // The stateful Encoder is the only correct chunking tool: converting slices independently corrupts any
            // surrogate pair straddling a boundary. Each iteration passes the whole remaining tail with flush: true —
            // only the iteration consuming the true end of input acts on flush, so no state ever crosses a Render call.
            _encoder = _encoder ?? Encoding.UTF8.GetEncoder();
#if NET6_0_OR_GREATER
            while (true)
            {
                Span<byte> span = _writer.GetSpan(MaxUtf8SizeHint);   // contract: at least the hint
                _encoder.Convert(chars, span, flush: true,
                    out int charsUsed, out int bytesUsed, out bool completed);
                _writer.Advance(bytesUsed);                            // commit before the next GetSpan
                if (completed)
                    break;
                chars = chars.Slice(charsUsed);                        // remaining tail; loop
            }
#else
            while (true)
            {
                Span<byte> span = _writer.GetSpan(MaxUtf8SizeHint);
                int charsUsed, bytesUsed;
                bool completed;
                unsafe
                {
                    fixed (char* c = chars)
                    fixed (byte* b = span)
                    {
                        _encoder.Convert(c, chars.Length, b, span.Length, flush: true,
                            out charsUsed, out bytesUsed, out completed);
                    }
                }
                _writer.Advance(bytesUsed);
                if (completed)
                    break;
                chars = chars.Slice(charsUsed);
            }
#endif
        }
    }
}
