using System.Globalization;

namespace Heddle.Exceptions
{
    /// <summary>
    /// <para>Thrown when a render exceeds a <see cref="Heddle.Data.RenderBudget"/> limit configured on
    /// <c>TemplateOptions.RenderBudget</c> (C1-R5). Enforced at the renderer seam, which has no template position, so
    /// unlike compile diagnostics this exception carries **no source location** — the accepted deviation is that per-op
    /// position tracking would tax the render hot path. <see cref="Kind"/>, <see cref="Limit"/>, and
    /// <see cref="Observed"/> identify the breach; for <see cref="RenderBudgetKind.RenderTime"/> both values are in
    /// milliseconds.</para>
    /// <para>On a streaming sink (<c>TextWriter</c>/<c>IBufferWriter</c>) any output already written stays written —
    /// the caller owns the sink and MUST treat this exception as "abort the response" (C1-R6).</para>
    /// </summary>
    public class TemplateRenderBudgetException : TemplateProcessingException
    {
        /// <summary>The budget dimension that was breached.</summary>
        public RenderBudgetKind Kind { get; }

        /// <summary>The configured limit (chars, ops, or milliseconds per <see cref="Kind"/>).</summary>
        public long Limit { get; }

        /// <summary>The observed value that crossed the limit (chars, ops, or milliseconds per <see cref="Kind"/>).</summary>
        public long Observed { get; }

        public TemplateRenderBudgetException(RenderBudgetKind kind, long limit, long observed)
            : base(string.Format(CultureInfo.InvariantCulture,
                "Render budget exceeded: {0} limit {1}, observed {2}.", kind, limit, observed))
        {
            Kind = kind;
            Limit = limit;
            Observed = observed;
        }
    }
}
