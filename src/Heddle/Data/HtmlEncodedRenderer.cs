using System;
using System.Net;
using System.Text.Encodings.Web;

namespace Heddle.Data
{
    public class HtmlEncodedRenderer : IScopeRenderer, ISpanScopeRenderer, IEncoderCarrier, IBudgetProbe
    {
        private readonly IScopeRenderer _renderer;

        // The effective output encoder for this render (B2): pulled from the wrapped sink's IEncoderCarrier so the
        // configured TemplateOptions.Encoder flows to the proxy without a new public ctor. null selects the legacy
        // WebUtility.HtmlEncode path (byte-identical to pre-B2). Held so nested resolution (this proxy re-read as an
        // IEncoderCarrier) returns the same encoder.
        private readonly TextEncoder _encoder;

        // C1-R4 / Obs-1: the deadline probe of the wrapped renderer, forwarded exactly as the encoder is. When this
        // proxy is the renderer a loop holds (a @list/@for nested inside a DirectRender value-extension body), the
        // loop's one-time `scope.Renderer as IBudgetProbe` type-test lands on this proxy; delegating to the inner
        // probe keeps the empty-loop MaxRenderTime backstop universal regardless of proxy nesting. null (the inner
        // renderer isn't budgeted) makes TickDeadline a no-op, so the unbudgeted path is unaffected.
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
        /// Phase 8 D9 / B2 — the string bridge for span writes under an encode proxy. Encoding happens on chars,
        /// before any byte transcode (encode → transcode, never reversed): a span materializes one string and routes
        /// through the effective encoder — the configured <see cref="System.Text.Encodings.Web.TextEncoder"/>
        /// (<c>TemplateOptions.Encoder</c>) when set, else the legacy <c>WebUtility.HtmlEncode</c> path. Deliberately
        /// <b>not</b> an <see cref="IUtf8ScopeRenderer"/>, so pre-encoded bytes can never bypass the proxy (D9): the
        /// UTF-8 sink only sees post-encode chars.
        /// </summary>
        public void Render(ReadOnlySpan<char> data)
        {
            if (data.IsEmpty)
                return;
#if NET6_0_OR_GREATER
            Render(new string(data));
#else
            Render(data.ToString());
#endif
        }
    }
}
