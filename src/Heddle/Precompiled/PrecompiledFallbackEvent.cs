namespace Heddle.Precompiled
{
    /// <summary>The per-request/registration diagnostic payload delivered to
    /// <see cref="PrecompiledTemplates.OnFallback"/> (phase 7 D7/D8). <see cref="Detail"/> is the pinned per-reason
    /// format string (identity-and-metadata.md § reason and detail strings); <see cref="DiagnosticId"/> is one of
    /// <c>HED7101</c>/<c>HED7102</c>/<c>HED7103</c>/<c>HED7104</c>.
    /// <para><see cref="Key"/> carries the <em>template key</em> for per-request reasons and the <em>assembly
    /// name</em> for the registration-time ones (<c>SchemaVersionUnsupported</c>,
    /// <c>EngineVersionIncompatible</c>, <c>RegisteredNameUnavailable</c>) — the pre-existing convention, kept
    /// rather than widened, because a registration-time event is about an assembly and has no one template to
    /// name. The contested spelling and its owner are in <see cref="Detail"/>.</para></summary>
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
