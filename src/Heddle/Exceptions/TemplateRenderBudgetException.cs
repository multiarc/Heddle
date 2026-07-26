using System.Globalization;

namespace Heddle.Exceptions
{
    /// <summary>
    /// Thrown when a render exceeds <see cref="Heddle.Data.RenderBudget"/> limits. No source location
    /// (enforced at renderer seam without per-op position tracking). <see cref="Kind"/>, <see cref="Limit"/>,
    /// and <see cref="Observed"/> identify the breach; for <see cref="RenderBudgetKind.RenderTime"/>, values
    /// are milliseconds. On streaming sinks, output already written stays written — treat this exception as
    /// "abort the response".
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
