using System.Collections.Generic;

namespace Heddle.Language
{
    /// <summary>
    /// The bookkeeping one parse keeps while it expands <c>@&lt;&lt;</c> imports: which documents are on the import
    /// stack, and how much of each report budget is left.
    /// <para>It lives here rather than on <see cref="ParserSettings"/> because that object is public configuration —
    /// a host is invited to build one and reuse it, and the parse entry point takes it. Holding a parse's state
    /// there made two concurrent parses share one import stack: each saw the other's imports as its own and
    /// reported cycles and depth limits that were not there, and the cycle message, built by joining that stack
    /// while another thread appended to it, threw out of the parse entirely.</para>
    /// <para>A parse — including every nested import parse, which runs in place — is synchronous and stays on the
    /// thread that began it, so the state is per thread. Two parses on one thread are sequential, and the second
    /// starts from a reset.</para>
    /// </summary>
    internal sealed class ImportParseState
    {
        [System.ThreadStatic] private static ImportParseState _current;

        internal static ImportParseState Current => _current ?? (_current = new ImportParseState());

        /// <summary>Set while a host's import reader runs; a parse beginning inside that window is its own.</summary>
        internal bool InHostCallback { get; set; }

        /// <summary>Marks the host-callback window. The flag is restored rather than cleared, because one reader may
        /// legitimately run inside another.</summary>
        internal static bool EnterHostCallback()
        {
            var previous = Current.InHostCallback;
            Current.InHostCallback = true;
            return previous;
        }

        internal static void ExitHostCallback(bool previous)
        {
            Current.InHostCallback = previous;
        }

        /// <summary>Swaps in fresh state for a parse that began inside a host callback, returning the state to put
        /// back. Returns null when this is an ordinary parse and nothing needs swapping.</summary>
        internal static ImportParseState BeginIsolatedParse()
        {
            if (_current == null || !_current.InHostCallback)
                return null;
            var outer = _current;
            _current = new ImportParseState();
            return outer;
        }

        internal static void EndIsolatedParse(ImportParseState outer)
        {
            if (outer != null)
                _current = outer;
        }

        /// <summary>The imports currently being expanded, outermost first. An <c>@&lt;&lt;</c> import parses the
        /// imported document in place, so a document that imports its way back to one already on this list would
        /// recurse until the stack ran out — and a <c>StackOverflowException</c> cannot be caught, so a template
        /// typo took the whole process down instead of producing a compile error.</summary>
        internal List<string> ActiveImports { get; } = new List<string>();

        internal int CycleReportBudget { get; set; } = ParserSettings.DefaultCycleReportBudget;

        internal int ImportExpansionsRemaining { get; set; } = ParserSettings.MaxImportExpansions;

        /// <summary>Whether this parse has already described a fan-out overflow. Every import past the bound is
        /// skipped, and there can be very many of them; the reader needs to be told once.</summary>
        internal bool FanOutReported { get; set; }

        /// <summary>Restores the budgets at the start of a top-level parse — not a nested one, which is part of the
        /// parse already in flight.</summary>
        internal void BeginTopLevelParse()
        {
            if (ActiveImports.Count != 0)
                return;
            CycleReportBudget = ParserSettings.DefaultCycleReportBudget;
            ImportExpansionsRemaining = ParserSettings.MaxImportExpansions;
            FanOutReported = false;
        }
    }
}
