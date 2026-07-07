namespace Heddle.Precompiled
{
    /// <summary>The per-request/registration diagnostic payload delivered to
    /// <see cref="PrecompiledTemplates.OnFallback"/> (phase 7 D7/D8). <see cref="Detail"/> is the pinned per-reason
    /// format string (identity-and-metadata.md § reason and detail strings); <see cref="DiagnosticId"/> is one of
    /// <c>HED7101</c>/<c>HED7102</c>/<c>HED7103</c>.</summary>
    public readonly struct PrecompiledFallbackEvent
    {
        public PrecompiledFallbackEvent(string key, PrecompiledFallbackReason reason, string detail,
            string diagnosticId)
        {
            Key = key;
            Reason = reason;
            Detail = detail;
            DiagnosticId = diagnosticId;
        }

        public string Key { get; }

        public PrecompiledFallbackReason Reason { get; }

        public string Detail { get; }

        public string DiagnosticId { get; }
    }
}
