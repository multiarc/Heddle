using System;
using System.Collections.Generic;
using System.Net;

namespace Heddle.Data
{
    public class HtmlEncodedRenderer : IScopeRenderer, ISpanScopeRenderer
    {
        private readonly IScopeRenderer _renderer;

        public HtmlEncodedRenderer(IScopeRenderer renderer)
        {
            _renderer = renderer;
        }

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _renderer.Render(WebUtility.HtmlEncode(data));
            }
        }

        /// <summary>
        /// Phase 8 D9 — the 1.x string bridge: <c>WebUtility.HtmlEncode</c> has only <c>(string)</c> and
        /// <c>(string, TextWriter)</c> overloads (no span input), so a span write under an encode proxy materializes
        /// one string and calls the existing seam. Encoding therefore happens on chars, before any byte transcode
        /// (encode → transcode, never reversed). The 2.0 encoder swap upgrades exactly this method body to a span
        /// <c>Encode</c>/<c>EncodeUtf8</c> loop; deliberately <b>not</b> an <see cref="IUtf8ScopeRenderer"/>, so
        /// pre-encoded bytes can never bypass the proxy.
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
