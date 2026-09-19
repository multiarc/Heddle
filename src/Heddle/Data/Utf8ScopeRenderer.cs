using System;
using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;

namespace Heddle.Data
{
    /// <summary>
    /// <see cref="IUtf8ScopeRenderer"/> over a host-supplied <see cref="IBufferWriter{T}"/> of <see cref="byte"/>,
    /// producing UTF-8. Write-through into writer-provided spans; the writer is never completed,
    /// flushed, or disposed — the host owns its lifecycle (e.g. a <c>PipeWriter</c> the host later
    /// <c>FlushAsync</c>es). Single render ownership: not thread-safe; a new instance is constructed per render, so the
    /// lazy <see cref="Encoder"/> is per-render.
    /// </summary>
    public sealed class Utf8ScopeRenderer : IUtf8ScopeRenderer, IEncoderCarrier
    {
        // 16 KB buffer keeps single-call tier for typical inputs; 5461+ UTF-16 units use chunked conversion.
        private const int MaxUtf8SizeHint = 16 * 1024;

        private readonly IBufferWriter<byte> _writer;
        private Encoder _encoder;   // Carries trailing high surrogate between chunks in chunked mode.
        private TextEncoder _outputEncoder;   // HTML encoder from render entry point (legacy: null).

        public Utf8ScopeRenderer(IBufferWriter<byte> writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        // Surfaces configured encoder to encode proxy; encode happens before transcoding to UTF-8.
        internal void SetOutputEncoder(TextEncoder encoder) => _outputEncoder = encoder;
        TextEncoder IEncoderCarrier.Encoder => _outputEncoder;

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
                Render(data.AsSpan());
        }

        public void Render(ReadOnlySpan<char> data)
        {
            if (data.IsEmpty)
                return;
            // UTF-8 worst case: 3 bytes per UTF-16 unit; single call handles surrogates atomically.
            if (data.Length * 3 <= MaxUtf8SizeHint)
                RenderSingle(data);
            else
                RenderChunked(data);
        }

        public void RenderUtf8(ReadOnlySpan<byte> utf8)
        {
            if (utf8.IsEmpty)
                return;
            // Direct copy: compiler guarantees valid UTF-8, no validation needed.
            BuffersExtensions.Write(_writer, utf8);
        }

        private void RenderSingle(ReadOnlySpan<char> chars)
        {
            var span = _writer.GetSpan(chars.Length * 3);
#if NET8_0_OR_GREATER
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
            // Stateful Encoder prevents surrogate-pair corruption at boundaries; state resets per Render call.
            _encoder = _encoder ?? Encoding.UTF8.GetEncoder();
#if NET8_0_OR_GREATER
            while (true)
            {
                Span<byte> span = _writer.GetSpan(MaxUtf8SizeHint);   // GetSpan contract: ≥ hint bytes.
                _encoder.Convert(chars, span, flush: true,
                    out int charsUsed, out int bytesUsed, out bool completed);
                _writer.Advance(bytesUsed);                            // Advance before next GetSpan.
                if (completed)
                    break;
                chars = chars.Slice(charsUsed);
            }
#else
            while (true)
            {
                Span<byte> span = _writer.GetSpan(MaxUtf8SizeHint);   // GetSpan contract: ≥ hint bytes.
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
                _writer.Advance(bytesUsed);                            // Advance before next GetSpan.
                if (completed)
                    break;
                chars = chars.Slice(charsUsed);
            }
#endif
        }
    }
}
