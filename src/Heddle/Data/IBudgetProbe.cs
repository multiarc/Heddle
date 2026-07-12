namespace Heddle.Data
{
    /// <summary>
    /// Internal renderer capability (C1-R4, closed decision #2 option (a)): lets the looping extensions
    /// (<c>@list</c>/<c>@for</c>) enforce a <see cref="RenderBudget.MaxRenderTime"/> deadline once per iteration even
    /// when the loop body writes nothing (a zero-output iteration performs no render op, so the ops/chars counters
    /// never see it). The loops type-test their held renderer for this interface **once before the loop** and call
    /// <see cref="TickDeadline"/> per iteration — no per-iteration dictionary access, no <see cref="Scope"/> layout
    /// change. Implemented only by <see cref="BudgetedRenderer"/>, so the capability is present exactly when a budget
    /// is active. Never public — budget enforcement is an engine-internal concern.
    /// </summary>
    internal interface IBudgetProbe
    {
        /// <summary>Checks the wall-clock deadline (a no-op when the active budget has no
        /// <see cref="RenderBudget.MaxRenderTime"/>); throws
        /// <see cref="Heddle.Exceptions.TemplateRenderBudgetException"/> when the deadline has passed.</summary>
        void TickDeadline();
    }
}
