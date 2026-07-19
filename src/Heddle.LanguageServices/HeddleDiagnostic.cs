namespace Heddle.LanguageServices
{
    /// <summary>Severity of a projected diagnostic; the protocol layer maps it 1:1 to LSP DiagnosticSeverity
    /// (Error → 1, Warning → 2).</summary>
    public enum HeddleDiagnosticSeverity
    {
        Error,
        Warning
    }

    /// <summary>
    /// Positioned diagnostic projected from <c>HeddleCompileError</c>/<c>HeddleCompileWarning</c> (phase 6 D10).
    /// <see cref="Id"/> is the stable <c>HEDxxxx</c> code or null (legacy) — the LSP <c>Diagnostic.code</c>;
    /// <see cref="Fix"/> carries the warning's remediation or null. Import-attributed entries arrive re-anchored
    /// per D25 (zero <see cref="Length"/> at the import site, prefixed <see cref="Message"/>,
    /// <see cref="ImportedFrom"/> set).
    /// </summary>
    public sealed class HeddleDiagnostic
    {
        internal HeddleDiagnostic(string id, string message, string fix, HeddleDiagnosticSeverity severity,
            int offset, int length, string importedFrom)
        {
            Id = id;
            Message = message;
            Fix = fix;
            Severity = severity;
            Offset = offset;
            Length = length;
            ImportedFrom = importedFrom;
        }

        public string Id { get; }
        public string Message { get; }
        public string Fix { get; }
        public HeddleDiagnosticSeverity Severity { get; }
        public int Offset { get; }
        public int Length { get; }

        /// <summary>Workspace-relative (or absolute, when outside RootPath) path of the imported/partial file whose
        /// analysis produced this entry (D25); null for diagnostics of the analyzed document itself.</summary>
        public string ImportedFrom { get; }
    }
}
