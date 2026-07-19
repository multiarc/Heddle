using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// A call-body <c>&lt;x:x&gt;</c> override whose base is unresolved at the override — the phase 7 D5/D12
    /// region-fill candidate. Captured at parse alongside the emitted base-not-found error (emit-then-retract):
    /// a compile-time match against a callee's <b>public</b> region retracts the error and materializes the fill;
    /// a <b>private</b> match retracts and raises HED5019; no match keeps the error byte-identical to today.
    /// Parse-model-pure (shared with the generator's compile-linked sources).
    /// </summary>
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

        /// <summary>The parsed override item — its <see cref="DefinitionItem.ParameterTemplate"/> /
        /// <see cref="DefinitionItem.Context"/> carry the override body (the context is attached at
        /// <c>ExitSubtemplate</c>, after this candidate is captured).</summary>
        internal DefinitionItem Item { get; }

        /// <summary>The optional narrowing <c>:: Type</c> declared on the override, or <c>null</c> to inherit the
        /// region's declared type.</summary>
        internal string NarrowingTypeName { get; }

        /// <summary>The override declaration's absolute span (HED5019 / narrowing-error anchoring, D4 step 4).</summary>
        internal BlockPosition Position { get; }

        /// <summary>The base-not-found error object emitted at parse. The D5 retract removes this exact reference
        /// from BOTH <c>CompileContext.CompileErrors</c> and <see cref="Origin"/>.<c>Errors</c>.</summary>
        internal HeddleCompileError Error { get; }

        /// <summary>The caller-content <see cref="ParseContext"/> identity the candidate was parsed in
        /// (<see cref="ParseContext.OriginIdentity"/> — stable across isolation copies). The compile-time fill
        /// step matches candidates whose origin equals the call site's caller-content context identity.</summary>
        internal ParseContext Origin { get; }

        /// <summary>True once a compile-time fill step raised HED5019 for this candidate — the diagnostic fires
        /// once per candidate even when the enclosing body is compiled at several outer call sites.</summary>
        internal bool PrivateOverrideReported { get; set; }
    }
}
