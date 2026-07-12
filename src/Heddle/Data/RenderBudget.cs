using System;

namespace Heddle.Data
{
    /// <summary>
    /// <para>Per-render resource limits for a single <c>Generate</c> call (C1). Every limit is nullable; a
    /// <c>null</c> limit is unbounded, and a <c>RenderBudget</c> with all-null limits imposes no cap. Attach it to
    /// <see cref="TemplateOptions.RenderBudget"/> — a <c>null</c> budget (the default) is today's unlimited behavior
    /// with zero render-path cost (no wrapper is created).</para>
    /// <para>Budgets are enforced at the renderer seam, not at the language level: they bound the untrusted-template
    /// availability leg (a well-formed template that would otherwise burn CPU or memory, e.g.
    /// <c>@for(range(0, 1000000000)){{x}}</c>). On breach the render throws
    /// <see cref="Heddle.Exceptions.TemplateRenderBudgetException"/>. Because enforcement lives in the wrapper, the
    /// budget counts *render operations*, not loop iterations — a loop iteration that writes nothing is invisible to
    /// the ops/chars counters but is still bounded by <see cref="MaxRenderTime"/> (checked once per
    /// <c>@list</c>/<c>@for</c> iteration).</para>
    /// </summary>
    public sealed class RenderBudget
    {
        /// <summary>Maximum cumulative UTF-16 characters accepted by the render sink across the whole
        /// <c>Generate</c> call (chars, not bytes — counted before any UTF-8 transcode, uniform across the string,
        /// <c>TextWriter</c>, and <c>IBufferWriter</c> sinks). <c>null</c> = unbounded.</summary>
        public long? MaxOutputChars { get; set; }

        /// <summary>Maximum cumulative render-write operations (each <c>Render(string)</c>/<c>Render(span)</c> call
        /// that reaches the sink counts as one). <c>null</c> = unbounded. Note ops ≠ loop iterations (see the type
        /// remarks); the deadline is the backstop for zero-output loops.</summary>
        public long? MaxRenderOps { get; set; }

        /// <summary>Maximum wall-clock render time, measured from <c>Generate</c> entry via
        /// <c>Stopwatch.GetTimestamp()</c> deltas (no timer thread). Checked on every render op and at least once per
        /// <c>@list</c>/<c>@for</c> iteration, so an empty (zero-output) loop still terminates. <c>null</c> =
        /// unbounded.</summary>
        public TimeSpan? MaxRenderTime { get; set; }
    }
}
