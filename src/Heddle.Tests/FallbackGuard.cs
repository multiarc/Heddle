using System;
using System.Collections.Generic;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Proves the parity suite cannot pass through the dynamic fallback: while the guard is
    /// armed, every fallback event is captured, and <see cref="AssertQuiet"/> fails the test naming
    /// each event. A parity test that silently rendered dynamically would trip the guard instead of
    /// going green on borrowed bytes.</summary>
    internal sealed class FallbackGuard : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _saved;
        private readonly List<PrecompiledFallbackEvent> _events = new List<PrecompiledFallbackEvent>();

        internal FallbackGuard()
        {
            _saved = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.OnFallback = e => _events.Add(e);
        }

        internal IReadOnlyList<PrecompiledFallbackEvent> Events => _events;

        internal void AssertQuiet()
        {
            if (_events.Count == 0)
                return;
            var parts = new List<string>();
            foreach (var e in _events)
                parts.Add(e.TemplateKey + " " + e.Reason + ": " + e.Detail);
            Assert.True(false, "Expected no fallback events, captured " + _events.Count + ": " +
                string.Join("; ", parts) + ".");
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _saved;
        }
    }
}
