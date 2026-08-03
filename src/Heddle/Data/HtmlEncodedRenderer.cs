using System;
using System.Net;
using System.Text.Encodings.Web;

namespace Heddle.Data
{
    public class HtmlEncodedRenderer : IScopeRenderer, ISpanScopeRenderer, IEncoderCarrier, IBudgetProbe
    {
        private const int EncodeBufferLength = 256;

        private readonly IScopeRenderer _renderer;

        // Non-null only when the wrapped sink accepts spans; string-only sinks (the budget counter
        // deliberately among them) keep the materializing path.
        private readonly ISpanScopeRenderer _spanSink;

        // Pulled from wrapped sink to preserve configured TemplateOptions.Encoder through nested resolution.
        // null selects legacy WebUtility.HtmlEncode path (byte-identical to pre-encoder behavior).
        private readonly TextEncoder _encoder;

        // Forwarded to nested resolution; null makes TickDeadline a no-op to preserve unbudgeted path.
        private readonly IBudgetProbe _probe;

        public HtmlEncodedRenderer(IScopeRenderer renderer)
        {
            _renderer = renderer;
            _spanSink = renderer as ISpanScopeRenderer;
            _encoder = (renderer as IEncoderCarrier)?.Encoder;
            _probe = renderer as IBudgetProbe;
        }

        TextEncoder IEncoderCarrier.Encoder => _encoder;

        void IBudgetProbe.TickDeadline() => _probe?.TickDeadline();

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _renderer.Render(_encoder == null ? WebUtility.HtmlEncode(data) : _encoder.Encode(data));
            }
        }

        /// <summary>
        /// Encodes span writes through the configured <see cref="System.Text.Encodings.Web.TextEncoder"/> (or legacy
        /// <c>WebUtility.HtmlEncode</c>). Deliberately not <see cref="IUtf8ScopeRenderer"/> so pre-encoded bytes
        /// cannot bypass encoding.
        /// </summary>
        public void Render(ReadOnlySpan<char> data)
        {
            if (data.IsEmpty)
                return;

            if (_spanSink == null)
            {
                Render(Materialize(data));
                return;
            }

            int first = _encoder == null ? IndexOfLegacyEncodingChar(data) : FindFirstConfiguredEncodingChar(data);
            if (first < 0)
            {
                _spanSink.Render(data);
                return;
            }

            if (first > 0)
                _spanSink.Render(data.Slice(0, first));

            if (_encoder == null)
                _renderer.Render(WebUtility.HtmlEncode(Materialize(data.Slice(first))));
            else
                EncodeChunked(data.Slice(first));
        }

        private void EncodeChunked(ReadOnlySpan<char> data)
        {
            Span<char> buffer = stackalloc char[EncodeBufferLength];
            while (!data.IsEmpty)
            {
                _encoder.Encode(data, buffer, out int consumed, out int written);
                if (consumed == 0)
                {
                    // A scalar whose encoded form exceeds the buffer can make no chunked progress; the string
                    // path preserves that encoder's exact bytes.
                    _renderer.Render(_encoder.Encode(Materialize(data)));
                    return;
                }

                _spanSink.Render(buffer.Slice(0, written));
                data = data.Slice(consumed);
            }
        }

        private unsafe int FindFirstConfiguredEncodingChar(ReadOnlySpan<char> data)
        {
            fixed (char* text = data)
            {
                return _encoder.FindFirstCharacterToEncode(text, data.Length);
            }
        }

        // Must mirror WebUtility.HtmlEncode's trigger set exactly: the five markup characters,
        // U+00A0..U+00FF (numeric character references), and every surrogate (pairs encode the astral
        // scalar; lone halves become U+FFFD, so both change bytes).
        private static int IndexOfLegacyEncodingChar(ReadOnlySpan<char> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                char ch = data[i];
                if (ch <= '>')
                {
                    if (ch == '<' || ch == '>' || ch == '"' || ch == '\'' || ch == '&')
                        return i;
                }
                else if ((ch >= '\u00A0' && ch <= '\u00FF') || char.IsSurrogate(ch))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string Materialize(ReadOnlySpan<char> data)
        {
#if NET8_0_OR_GREATER
            return new string(data);
#else
            return data.ToString();
#endif
        }
    }
}
