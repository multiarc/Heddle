using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Exceptions;

namespace Heddle.Precompiled
{
    /// <summary>How far a faulted <see cref="PrecompiledRuntime.Init"/> reaches.</summary>
    public enum PrecompiledInitFaultScope
    {
        /// <summary>One call site is unusable; the rest of the template stays precompiled and the call renders
        /// through a substitute that compiles its own source text at first render.</summary>
        CallSite = 0,

        /// <summary>The whole template is unusable at the precompiled tier and the request belongs on the dynamic
        /// tier — either the hook reported compile errors the dynamic tier would report too, or the hook's own
        /// answer about body typing contradicts what the build assumed and the emitted casts would render wrong
        /// bytes.</summary>
        Template = 1
    }

    /// <summary>
    /// What a <see cref="PrecompiledRuntime.Init"/> that did not succeed recorded, in place of throwing.
    /// <para>All of this runs from a generated static field initializer, and the manifest touches
    /// <c>strategy:</c>, so it all runs at <b>registration</b>. A throwing type initializer is a
    /// <see cref="TypeInitializationException"/> on every later use of that type, forever — so
    /// <c>Init</c> throws only for a null argument and answers everything else through this record, the same
    /// capture-and-answer shape <see cref="PrecompiledPartialName"/> established.</para>
    /// </summary>
    public sealed class PrecompiledInitFault
    {
        internal PrecompiledInitFault(PrecompiledInitFaultScope scope, string detail, Exception exception,
            IReadOnlyList<HeddleCompileError> errors, PrecompiledFallbackReason? reason)
        {
            Scope = scope;
            Detail = detail;
            Exception = exception;
            Errors = errors ?? EmptyErrors;
            Reason = reason;
        }

        private static readonly HeddleCompileError[] EmptyErrors = new HeddleCompileError[0];

        /// <summary>How far the fault reaches.</summary>
        public PrecompiledInitFaultScope Scope { get; }

        /// <summary>A one-sentence description naming the call and what went wrong.</summary>
        public string Detail { get; }

        /// <summary>The exception the hook or the extension's construction threw, or <c>null</c> when the fault was
        /// reported rather than thrown.</summary>
        public Exception Exception { get; }

        /// <summary>The compile errors the hook added to its compile scope; empty unless the hook reported any.</summary>
        public IReadOnlyList<HeddleCompileError> Errors { get; }

        /// <summary>The fallback reason a <see cref="PrecompiledInitFaultScope.Template"/> fault is reported under;
        /// <c>null</c> for a call-site fault, which costs no tier.</summary>
        public PrecompiledFallbackReason? Reason { get; }
    }
}
