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
    /// Hooks <see cref="PrecompiledTemplates.OnFallback"/> to record <see cref="PrecompiledFallbackEvent"/>s
    /// raised during a guarded region. Any undeclared event fails the test at <see cref="Verify"/>.
    /// Does not throw from the callback to preserve <see cref="PrecompiledMismatchException"/> under
    /// <see cref="PrecompiledMismatchPolicy.Strict"/> mode.
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

        /// <summary>Returns options with <see cref="PrecompiledMismatchPolicy.Strict"/> policy, preserving caller settings from <paramref name="baseOptions"/>.</summary>
        public static TemplateOptions GuardedOptions(TemplateOptions baseOptions = null)
        {
            var options = baseOptions == null ? new TemplateOptions() : new TemplateOptions(baseOptions);
            options.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict;
            return options;
        }

        /// <summary>Declares one expected fallback; each expectation matches one event at <see cref="Verify"/>.</summary>
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

        /// <summary>Returns the event's subject: the TemplateKey or AssemblyName depending on the reason.</summary>
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
