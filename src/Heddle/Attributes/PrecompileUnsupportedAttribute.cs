using System;

namespace Heddle.Attributes {
    /// <summary>
    /// Declares that an extension's compile-time behaviour cannot be reproduced from a static initializer, so a
    /// precompiled template must bind this extension's call sites dynamically instead of running its
    /// <c>InitStart</c> through <see cref="Heddle.Precompiled.PrecompiledRuntime.Init"/>.
    /// <para>The cost is <b>one call site</b>: the rest of the template still precompiles, and the declaring call
    /// renders by compiling its own source text at first render — the same substitute a hook that threw would have
    /// earned. Output is unchanged either way; the two tiers are parity-checked.</para>
    /// <para><b>What it is for.</b> A hook that walks the enclosing document through
    /// <c>InitContext.ParseContext.Tokens</c>/<c>SubContexts</c> is the primary case: the token stream <i>is</i> the
    /// parse tree of the enclosing document, and no call site can carry one, so a synthesized parse context is the
    /// one place the binding seam presents an empty member rather than an absent one. A hook that reads host state
    /// the build has no way to establish is the other. A hook that merely re-types its body needs nothing declared —
    /// that is exactly what the seam runs for real.</para>
    /// <para>Read on <b>both</b> sides. The build reads it off the extension's symbol and reports it as
    /// <c>HED7033</c>, carrying <see cref="Reason"/> verbatim; the runtime reads it off the live type, so an
    /// extension package that adds the declaration <i>after</i> a consumer's build still falls back rather than
    /// binding through a seam its author has disowned.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class PrecompileUnsupportedAttribute: Attribute {
        /// <summary>Declares the extension unsupported by the precompiled binding seam.</summary>
        /// <param name="reason">One sentence naming what the hook does that a static initializer cannot reproduce.
        /// Carried verbatim into the <c>HED7033</c> build warning, so it is read by the template author rather than
        /// by the extension's own author.</param>
        public PrecompileUnsupportedAttribute(string reason)
        {
            Reason = reason;
        }

        /// <summary>Why the hook cannot be reproduced; may be null or empty, which reports as unstated.</summary>
        public string Reason { get; }
    }
}
