using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>A region-fill candidate: a call-body override whose base is unresolved at parse, captured alongside
    /// a base-not-found error; matching against a public region retracts the error, private match raises HED5019.</summary>
    internal sealed class RegionFillCandidate
    {
        internal RegionFillCandidate(string name, DefinitionItem item, string narrowingTypeName,
            BlockPosition position, HeddleCompileError error, ParseContext origin)
        {
            Name = name;
            Item = item;
            NarrowingTypeName = narrowingTypeName;
            Position = position;
            Error = error;
            Origin = origin;
        }

        /// <summary>The overridden region name (the shared <c>x</c> of <c>&lt;x:x&gt;</c>).</summary>
        internal string Name { get; }

        /// <summary>The parsed override item carrying the override body.</summary>
        internal DefinitionItem Item { get; }

        /// <summary>The optional narrowing <c>:: Type</c> declared on the override, or <c>null</c> to inherit the
        /// region's declared type.</summary>
        internal string NarrowingTypeName { get; }

        /// <summary>The override declaration's absolute span (HED5019 / narrowing-error anchoring).</summary>
        internal BlockPosition Position { get; }

        /// <summary>The base-not-found error emitted at parse; retraction removes it from both CompileContext and Origin.</summary>
        internal HeddleCompileError Error { get; }

        /// <summary>The caller-content context this candidate was parsed in; used to match at call sites.</summary>
        internal ParseContext Origin { get; }

        /// <summary>True once HED5019 has been raised for this candidate; fires once per candidate across all call sites.</summary>
        internal bool PrivateOverrideReported { get; set; }
    }
}
