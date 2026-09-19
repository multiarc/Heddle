using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>The parity suite's fallback sentinel (P1-R9): while the guard is armed, every
    /// <see cref="PrecompiledTemplates.OnFallback"/> event is captured; <see cref="Verify"/> fails the test
    /// naming each event that was not declared with <see cref="Expect"/>, and each declaration that never
    /// fired. A parity test that silently rendered dynamically would trip the guard instead of going green on
    /// borrowed bytes. <see cref="GuardedOptions"/> pins the tier the other way round: a copy of the request
    /// options under <see cref="PrecompiledMismatchPolicy.Strict"/>, so a gauntlet refusal throws instead of
    /// recompiling.</summary>
    internal sealed class FallbackGuard : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _saved;
        private readonly List<PrecompiledFallbackEvent> _events = new List<PrecompiledFallbackEvent>();
        private readonly List<KeyValuePair<string, PrecompiledFallbackReason>> _expected =
            new List<KeyValuePair<string, PrecompiledFallbackReason>>();

        internal FallbackGuard()
        {
            _saved = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.OnFallback = e => _events.Add(e);
        }

        /// <summary>Arms a guard on the registry's fallback channel; dispose to restore the previous handler.</summary>
        internal static FallbackGuard Install() => new FallbackGuard();

        internal IReadOnlyList<PrecompiledFallbackEvent> Events => _events;

        /// <summary>A copy of <paramref name="options"/> that renders under
        /// <see cref="PrecompiledMismatchPolicy.Strict"/>: a precompiled entry that cannot be plugged throws
        /// rather than falling back, which is the only way a parity render proves the tier it claims.</summary>
        internal static TemplateOptions GuardedOptions(TemplateOptions options)
        {
            var guarded = new TemplateOptions(options);
            guarded.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict;
            return guarded;
        }

        /// <summary>Declares that one fallback for <paramref name="key"/> with <paramref name="reason"/> is
        /// expected. Declarations are the enumerable set of tests that expect a fallback (testing-standards
        /// § Precompiled-tier posture); <see cref="Verify"/> fails on an undeclared event and on an unfired declaration.</summary>
        internal void Expect(string key, PrecompiledFallbackReason reason)
        {
            _expected.Add(new KeyValuePair<string, PrecompiledFallbackReason>(key, reason));
        }

        /// <summary>Asserts the captured events are exactly the declared ones, key and reason, in any order.</summary>
        internal void Verify()
        {
            var pending = new List<KeyValuePair<string, PrecompiledFallbackReason>>(_expected);
            var undeclared = new List<string>();
            foreach (var e in _events)
            {
                int index = pending.FindIndex(p =>
                    string.Equals(p.Key, e.TemplateKey, StringComparison.Ordinal) && p.Value == e.Reason);
                if (index >= 0)
                    pending.RemoveAt(index);
                else
                    undeclared.Add(e.TemplateKey + " " + e.Reason + ": " + e.Detail);
            }

            var problems = new List<string>();
            if (undeclared.Count != 0)
                problems.Add("undeclared fallback events (" + undeclared.Count + "): " + string.Join("; ", undeclared));
            foreach (var p in pending)
                problems.Add("declared fallback never fired: " + p.Key + " " + p.Value);
            Assert.True(problems.Count == 0, string.Join(" | ", problems) + ".");
        }

        /// <summary>No declarations, no events: the shape every parity pass asserts.</summary>
        internal void AssertQuiet()
        {
            Assert.True(_expected.Count == 0, "AssertQuiet is for a guard with no declarations; use Verify.");
            Verify();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _saved;
        }
    }
}
