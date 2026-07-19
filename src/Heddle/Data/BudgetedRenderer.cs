using System.Diagnostics;
using System.Text.Encodings.Web;
using Heddle.Exceptions;

namespace Heddle.Data
{
    /// <summary>
    /// <para>The render-budget enforcement seam (C1-R2/R3). Wraps the innermost sink and is installed as the render's
    /// renderer <b>only when <c>TemplateOptions.RenderBudget</c> is non-null</b>, so the unbudgeted path allocates no
    /// wrapper and pays nothing (C1-R11). Positioned <i>inside</i> any <see cref="HtmlEncodedRenderer"/> proxy (the
    /// proxy wraps the scope's renderer, which is this), so the budget counts post-encoding characters — what actually
    /// lands in output.</para>
    /// <para><b>Bypass-proofing (C1-R3).</b> This deliberately implements only <see cref="IScopeRenderer"/> (plus the
    /// internal carrier/probe), <b>not</b> <see cref="ISpanScopeRenderer"/> or <see cref="IUtf8ScopeRenderer"/>. Every
    /// engine write path capability-tests the renderer and falls back to <c>Render(string)</c> when those interfaces
    /// are absent — including generated <c>WritePiece</c>, whose <c>is IUtf8ScopeRenderer</c> test fails here so
    /// pre-encoded u8 pieces route through the counted string method instead of bypassing it. One counting site,
    /// uniform UTF-16-char accounting across all three sinks, and no path reaches the wrapped sink without passing the
    /// check.</para>
    /// <para><b>Per-render state (C1-R7).</b> Counters live on this instance, which is constructed per
    /// <c>Generate</c> call and never shared; concurrent renders each get their own.</para>
    /// </summary>
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
            // Forward the wrapped sink's effective encoder (B2) so an HtmlEncodedRenderer proxy wrapping this instance
            // resolves the configured TemplateOptions.Encoder through IEncoderCarrier exactly as it would off the sink.
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
                // Ticks-of-the-Stopwatch equivalent of the requested duration; clamp negatives to fire immediately.
                double ticks = span.TotalSeconds * Stopwatch.Frequency;
                _deadlineTimestamp = _startTimestamp + (ticks > 0 ? (long)ticks : 0);
                _timeLimitMs = (long)span.TotalMilliseconds;
            }
        }

        TextEncoder IEncoderCarrier.Encoder => _encoder;

        public void Render(string data)
        {
            // Count every render-write op (C1-R2). Empty strings are the sink's own no-op, but still count as an op
            // per the literal contract; they add zero chars.
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

        // Never materialized as full output — the wrapped sink owns ToString(). Present only to satisfy IScopeRenderer.
        public override string ToString() => _inner.ToString();
    }
}
