using System;
using Heddle.Language;
using Heddle.Strings.Core;

namespace Heddle.Data {
    public class HeddleCompileError {
        public LinePosition LinePosition { get; internal set; }

        public BlockPosition Position { get; set; }

        public string Error { get; set; }

        /// <summary>
        /// The phase 6 D25 import-provenance marker; non-null (only when <c>ProvideLanguageFeatures</c> is on)
        /// when this entry was produced by an imported/partial file's parse or call-site compile. Internal — the
        /// public shape (<see cref="ToString()"/>, positions) is untouched; the LSP facade reads it to re-anchor.
        /// </summary>
        internal ImportOrigin ImportOrigin { get; set; }

        /// <summary>
        /// <para>Stable diagnostic ID (e.g. <c>"HED1003"</c>); <c>null</c> for diagnostics not yet assigned one.</para>
        /// <para>Surfaced by <see cref="ToString()"/> and, in later tooling phases, as the LSP
        /// <c>Diagnostic.code</c>. See <see cref="HeddleDiagnosticIds"/>.</para>
        /// </summary>
        public string DiagnosticId { get; set; }

        public Exception Exception { get; set; }

        private string ErrorWithId => DiagnosticId != null ? $"{DiagnosticId}: {Error}" : Error;

        public override string ToString()
        {
            if (LinePosition != null)
            {
                return $"[{LinePosition}]{ErrorWithId}\r\n{Exception}";
            }
            return $"[{Position}]{ErrorWithId}\r\n{Exception}";
        }

        public string ToString(int line, int position, int length)
        {
            return $"[{line},{position}:{length}]{ErrorWithId}\r\n{Exception}";
        }
    }
}