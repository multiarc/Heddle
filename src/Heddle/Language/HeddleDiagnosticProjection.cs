using System;
using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Language
{
    /// <summary>One drained front-end diagnostic in a host-neutral shape: identity, text, remediation, severity
    /// and span. Host <i>policy</i> — the language server's import re-anchoring and path rendering, the
    /// generator's region-fill retract filter and Roslyn location mapping, <c>HeddleCompileResult</c>'s
    /// <c>"[{pos}]{id}: {msg}"</c> line — stays with the host; this is what all three drain.</summary>
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

        /// <summary>The entry this was projected from — the handle a host needs to apply a policy stated over
        /// the front end's own objects (the generator's retract filter matches captured error instances).</summary>
        public HeddleCompileError Source { get; }

        /// <summary>The stable <c>HEDxxxx</c> id, or <c>null</c> for a diagnostic not yet assigned one.</summary>
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

    /// <summary>
    /// <para>The one drain rule (generator plan phase 6 D5). A host surfaces Heddle diagnostics by draining the
    /// front end's four channels — <c>CompileContext.CompileErrors</c>/<c>CompileWarnings</c> and
    /// <c>ParseContext.Errors</c>/<c>Warnings</c> — mapping <see cref="HeddleCompileWarning"/> to warning
    /// severity and everything else to error, carrying the diagnostic id and the fix, positioned at the entry's
    /// own span, with entries de-duplicated by reference (the same object is reachable from more than one
    /// context in the nested-parse case).</para>
    /// <para>The rule used to exist completely in the language server, partially in the generator (parse
    /// channels only, severity by collection membership, no fix, warning ids dropped) and as a third rendering
    /// in <c>HeddleCompileResult</c> — so a diagnostic channel added to the front end was picked up by one host
    /// and silently missed by another. Draining through one function makes channel-completeness structural.</para>
    /// <para>The compile-channel overload lives in the runtime-only half of this partial
    /// (<c>HeddleDiagnosticProjection.Runtime.cs</c>): <c>CompileContext</c> is not part of the linked front-end
    /// closure, and the source generator has no compile-channel stages to drain yet.</para>
    /// </summary>
    internal static partial class HeddleDiagnosticProjection
    {
        /// <summary>Drains the parse channels. <paramref name="include"/>, when supplied, filters entries before
        /// they are yielded — the shape a host-specific pre-filter takes (the generator's emit-then-retract rule
        /// for region-fill candidates).</summary>
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
