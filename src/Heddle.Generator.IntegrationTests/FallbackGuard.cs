using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Precompiled;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>Raised by <see cref="FallbackGuard.Verify"/> when a precompiled→dynamic fallback was raised during a
    /// guarded region without having been declared with <see cref="FallbackGuard.Expect"/>.</summary>
    internal sealed class FallbackGuardException : Exception
    {
        public FallbackGuardException(string message) : base(message) { }
    }

    /// <summary>
    /// The fallback sentinel. Hooks <see cref="PrecompiledTemplates.OnFallback"/> for the lifetime of a guarded
    /// region, records every raised <see cref="PrecompiledFallbackEvent"/>, and restores the previous hook on dispose
    /// (the save/restore pattern the existing <c>OnFallback</c> tests already prove). Any event whose (subject,
    /// reason) pair was not declared through <see cref="Expect"/> fails the test at <see cref="Verify"/>.
    /// <para>The subject is whichever of the event's two carriers is populated: a template key for the per-request
    /// reasons, an assembly name for the registration-time ones. The guard matches on both because it polices
    /// <em>every</em> fallback, and collapsing them here is a test-side display choice — the engine keeps them
    /// apart, which is the whole point of the split.</para>
    /// <para>The sentinel deliberately does <b>not</b> throw from inside the callback: <c>TryResolve</c> raises the
    /// event *before* the <see cref="PrecompiledMismatchPolicy.Strict"/> throw, so a throwing callback would replace
    /// the sharper typed <see cref="PrecompiledMismatchException"/> that <see cref="GuardedOptions"/> exists to
    /// produce. Under Strict the typed exception escapes the render and <c>Verify</c> is never reached; under the
    /// default <c>Fallback</c> policy the render silently degrades and <c>Verify</c> is what fails the test.</para>
    /// </summary>
    internal sealed class FallbackGuard : IDisposable
    {
        private readonly object _sync = new object();
        private readonly Action<PrecompiledFallbackEvent> _previous;
        private readonly List<(string key, PrecompiledFallbackReason reason)> _expected =
            new List<(string, PrecompiledFallbackReason)>();
        private readonly List<PrecompiledFallbackEvent> _events = new List<PrecompiledFallbackEvent>();
        private bool _disposed;

        private FallbackGuard()
        {
            _previous = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.OnFallback = Record;
        }

        /// <summary>Installs the sentinel. Dispose restores the previously-installed hook.</summary>
        public static FallbackGuard Install() => new FallbackGuard();

        /// <summary>The guarded request options: <see cref="PrecompiledMismatchPolicy.Strict"/> so a gauntlet failure
        /// throws a typed <see cref="PrecompiledMismatchException"/> at the primary resolve, over a copy of
        /// <paramref name="baseOptions"/> (a fresh <see cref="TemplateOptions"/> when null) so callers keep their
        /// profile/trim/encoder settings.</summary>
        public static TemplateOptions GuardedOptions(TemplateOptions baseOptions = null)
        {
            var options = baseOptions == null ? new TemplateOptions() : new TemplateOptions(baseOptions);
            options.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict;
            return options;
        }

        /// <summary>Declares one fallback as expected — the only way an event passes <see cref="Verify"/>. An
        /// expectation is consumed once per matching event.</summary>
        public FallbackGuard Expect(string key, PrecompiledFallbackReason reason)
        {
            lock (_sync)
                _expected.Add((key, reason));
            return this;
        }

        /// <summary>Every event raised so far, in order.</summary>
        public IReadOnlyList<PrecompiledFallbackEvent> Events
        {
            get { lock (_sync) return _events.ToArray(); }
        }

        /// <summary>Fails when any recorded event was not declared, or when a declared event never fired.</summary>
        public void Verify()
        {
            lock (_sync)
            {
                var outstanding = new List<(string key, PrecompiledFallbackReason reason)>(_expected);
                var unexpected = new List<PrecompiledFallbackEvent>();
                foreach (var evt in _events)
                {
                    var at = outstanding.FindIndex(e => e.key == Subject(evt) && e.reason == evt.Reason);
                    if (at < 0)
                        unexpected.Add(evt);
                    else
                        outstanding.RemoveAt(at);
                }

                if (unexpected.Count != 0)
                    throw new FallbackGuardException(
                        "Undeclared precompiled→dynamic fallback (the render did not stay on the precompiled tier): " +
                        string.Join("; ",
                            unexpected.Select(e => $"{Subject(e)} [{e.Reason}] {e.Detail} ({e.DiagnosticId})")));
                if (outstanding.Count != 0)
                    throw new FallbackGuardException(
                        "Declared fallback never fired: " +
                        string.Join("; ", outstanding.Select(e => $"{e.key} [{e.reason}]")));
            }
        }

        /// <summary>Whichever carrier the event's reason populates — see the class remark.</summary>
        private static string Subject(PrecompiledFallbackEvent evt) => evt.TemplateKey ?? evt.AssemblyName;

        private void Record(PrecompiledFallbackEvent evt)
        {
            lock (_sync)
                _events.Add(evt);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            PrecompiledTemplates.OnFallback = _previous;
        }
    }
}
