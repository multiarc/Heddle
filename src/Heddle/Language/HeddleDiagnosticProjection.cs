using System;
using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Language
{
    /// <summary>A drained front-end diagnostic in host-neutral shape: identity, text, fix, severity, and span.
    /// Host policy (re-anchoring, path rendering, Roslyn mapping) stays with the host.</summary>
    internal readonly struct HeddleDiagnosticEntry
    {
        internal HeddleDiagnosticEntry(HeddleCompileError source)
        {
            Source = source;
            Id = source.DiagnosticId;
            Message = source.Error;
            var warning = source as HeddleCompileWarning;
            IsWarning = warning != null;
            Fix = warning?.Fix;
            Offset = source.Position.StartIndex;
            Length = source.Position.Length;
            ImportOrigin = source.ImportOrigin;
        }

        /// <summary>The entry this was projected from — the handle a host needs to apply policy to the front end's objects.</summary>
        public HeddleCompileError Source { get; }

        public string Id { get; }

        public string Message { get; }

        /// <summary>Remediation text; non-null only on warnings that carry one.</summary>
        public string Fix { get; }

        /// <summary>Severity by <b>subtype</b> — <see cref="HeddleCompileWarning"/> is a warning — never by
        /// which collection the entry arrived in.</summary>
        public bool IsWarning { get; }

        public int Offset { get; }

        public int Length { get; }

        /// <summary>Import provenance, when the entry came from an imported/partial file's parse or call-site
        /// compile. Hosts that re-anchor read it; hosts that do not, ignore it.</summary>
        public ImportOrigin ImportOrigin { get; }
    }

    /// <summary>Drains the front end's diagnostic channels to a host-neutral shape, de-duplicating by reference.
    /// Maps <see cref="HeddleCompileWarning"/> to warning severity, everything else to error; carries diagnostic id, fix, and span.
    /// The compile-channel overload lives in <c>HeddleDiagnosticProjection.Runtime.cs</c>.</summary>
    internal static partial class HeddleDiagnosticProjection
    {
        /// <summary>Drains the parse channels; <paramref name="include"/> filters entries before yielding (host-specific pre-filter).</summary>
        internal static List<HeddleDiagnosticEntry> Drain(ParseContext parseContext,
            Func<HeddleCompileError, bool> include = null)
        {
            var result = new List<HeddleDiagnosticEntry>();
            DrainParse(parseContext, new HashSet<HeddleCompileError>(), include, result);
            return result;
        }

        private static void DrainParse(ParseContext parseContext, HashSet<HeddleCompileError> seen,
            Func<HeddleCompileError, bool> include, List<HeddleDiagnosticEntry> result)
        {
            if (parseContext == null)
                return;
            Add(parseContext.Errors, seen, include, result);
            Add(parseContext.Warnings, seen, include, result);
        }

        private static void Add<T>(List<T> entries, HashSet<HeddleCompileError> seen,
            Func<HeddleCompileError, bool> include, List<HeddleDiagnosticEntry> result)
            where T : HeddleCompileError
        {
            if (entries == null)
                return;
            foreach (var entry in entries)
            {
                if (entry == null || !seen.Add(entry))
                    continue;
                if (include != null && !include(entry))
                    continue;
                result.Add(new HeddleDiagnosticEntry(entry));
            }
        }
    }
}
