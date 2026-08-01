using System;
using System.Net;
using System.Text.Encodings.Web;

namespace Heddle.Data
{
    public class HtmlEncodedRenderer : IScopeRenderer, ISpanScopeRenderer, IEncoderCarrier, IBudgetProbe
    {
        private readonly IScopeRenderer _renderer;

        // Pulled from wrapped sink to preserve configured TemplateOptions.Encoder through nested resolution.
        // null selects legacy WebUtility.HtmlEncode path (byte-identical to pre-encoder behavior).
        private readonly TextEncoder _encoder;

        // Forwarded to nested resolution; null makes TickDeadline a no-op to preserve unbudgeted path.
        private readonly IBudgetProbe _probe;

        public HtmlEncodedRenderer(IScopeRenderer renderer)
        {
            _renderer = renderer;
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
#if NET8_0_OR_GREATER
            Render(new string(data));
#else
            Render(data.ToString());
#endif
        }
    }
}
