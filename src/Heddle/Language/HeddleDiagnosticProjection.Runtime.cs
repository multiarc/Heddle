using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Language
{
    /// <summary>The compile-channel half of the drain, split out of the shared file because
    /// <see cref="CompileContext"/> lives in the runtime rather than in the linked front-end closure — the same
    /// split <c>DocumentParser.Runtime.cs</c> uses, so the source generator links the parse-channel rule and
    /// nothing it cannot compile.</summary>
    internal static partial class HeddleDiagnosticProjection
    {
        /// <summary>Drains all four channels: compile errors, compile warnings, parse errors, parse warnings —
        /// the order the language server has always used, kept so its diagnostic ordering is unchanged.</summary>
        internal static List<HeddleDiagnosticEntry> Drain(CompileContext compileContext, ParseContext parseContext,
            Func<HeddleCompileError, bool> include = null)
        {
            var result = new List<HeddleDiagnosticEntry>();
            var seen = new HashSet<HeddleCompileError>();
            if (compileContext != null)
            {
                Add(compileContext.CompileErrors, seen, include, result);
                Add(compileContext.CompileWarnings, seen, include, result);
            }

            DrainParse(parseContext, seen, include, result);
            return result;
        }
    }
}
