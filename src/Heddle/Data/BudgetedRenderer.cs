using System.Diagnostics;
using System.Text.Encodings.Web;
using Heddle.Exceptions;

namespace Heddle.Data
{
    /// <summary>Wraps the innermost sink, installed only when TemplateOptions.RenderBudget is non-null.
    /// Positioned inside HtmlEncodedRenderer proxies to count post-encoding characters. Deliberately implements only
    /// IScopeRenderer (not ISpanScopeRenderer/IUtf8ScopeRenderer) to force all engine writes through its counting
    /// Render(string) method. Per-render state: counters live on this instance, not shared across concurrent calls.</summary>
    internal sealed class BudgetedRenderer : IScopeRenderer, IEncoderCarrier, IBudgetProbe
    {
        private readonly IScopeRenderer _inner;
        private readonly TextEncoder _encoder;   // forwarded from the wrapped sink so the encode proxy still discovers it

        private readonly long _maxChars;
        private readonly bool _hasCharBudget;
        private readonly long _maxOps;
        private readonly bool _hasOpBudget;

        private readonly bool _hasTimeBudget;
        private readonly long _startTimestamp;
        private readonly long _deadlineTimestamp;
        private readonly long _timeLimitMs;

        private long _chars;
        private long _ops;

        internal BudgetedRenderer(IScopeRenderer inner, RenderBudget budget)
        {
            _inner = inner;
            // Forward the encoder so HtmlEncodedRenderer proxies can resolve TemplateOptions.Encoder.
            _encoder = (inner as IEncoderCarrier)?.Encoder;

            if (budget.MaxOutputChars.HasValue)
            {
                _hasCharBudget = true;
                _maxChars = budget.MaxOutputChars.Value;
            }

            if (budget.MaxRenderOps.HasValue)
            {
                _hasOpBudget = true;
                _maxOps = budget.MaxRenderOps.Value;
            }

            if (budget.MaxRenderTime.HasValue)
            {
                _hasTimeBudget = true;
                _startTimestamp = Stopwatch.GetTimestamp();
                var span = budget.MaxRenderTime.Value;
                // Convert to Stopwatch ticks; clamp negatives to fire immediately.
                double ticks = span.TotalSeconds * Stopwatch.Frequency;
                _deadlineTimestamp = _startTimestamp + (ticks > 0 ? (long)ticks : 0);
                _timeLimitMs = (long)span.TotalMilliseconds;
            }
        }

        TextEncoder IEncoderCarrier.Encoder => _encoder;

        public void Render(string data)
        {
            // Count every op per contract, including empty strings (which add zero chars).
            _ops++;
            if (_hasOpBudget && _ops > _maxOps)
                throw new TemplateRenderBudgetException(RenderBudgetKind.RenderOps, _maxOps, _ops);

            if (!string.IsNullOrEmpty(data))
            {
                _chars += data.Length;
                if (_hasCharBudget && _chars > _maxChars)
                    throw new TemplateRenderBudgetException(RenderBudgetKind.OutputChars, _maxChars, _chars);
            }

            if (_hasTimeBudget)
                CheckDeadline();

            _inner.Render(data);
        }

        public void TickDeadline()
        {
            if (_hasTimeBudget)
                CheckDeadline();
        }

        private void CheckDeadline()
        {
            long now = Stopwatch.GetTimestamp();
            if (now >= _deadlineTimestamp)
            {
                long elapsedMs = (now - _startTimestamp) * 1000 / Stopwatch.Frequency;
                throw new TemplateRenderBudgetException(RenderBudgetKind.RenderTime, _timeLimitMs, elapsedMs);
            }
        }

        // Wrapped sink owns ToString; present only to satisfy IScopeRenderer.
        public override string ToString() => _inner.ToString();
    }
}
