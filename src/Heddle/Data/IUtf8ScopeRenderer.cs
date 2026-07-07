using System;

namespace Heddle.Data
{
    /// <summary>
    /// Renderer capability (phase 8 D2): accepts pre-encoded UTF-8 bytes (phase 7's <c>"…"u8</c> pieces) verbatim.
    /// Encode proxies deliberately do <b>not</b> implement this interface (<see cref="HtmlEncodedRenderer"/>): under a
    /// proxy the capability test fails and pieces route string → encode → transcode, so pre-encoded bytes can never
    /// bypass an active encode proxy (D9).
    /// </summary>
    public interface IUtf8ScopeRenderer : ISpanScopeRenderer
    {
        /// <summary>Writes pre-encoded UTF-8 bytes verbatim. Callers must pass valid UTF-8 — the engine only calls
        /// this with compiler-validated <c>"…"u8</c> pieces (phase 7 D15).</summary>
        void RenderUtf8(ReadOnlySpan<byte> utf8);
    }
}
