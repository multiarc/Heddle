namespace Heddle.Data
{
    /// <summary>
    /// Renderer capability for loops to enforce per-iteration <see cref="RenderBudget.MaxRenderTime"/> deadlines
    /// (needed because zero-output iterations skip render ops and counters never see them). Loops type-test once
    /// before iterating and call <see cref="TickDeadline"/> per iteration; implemented only by
    /// <see cref="BudgetedRenderer"/> when a budget is active.
    /// </summary>
    internal interface IBudgetProbe
    {
        /// <summary>Checks the wall-clock deadline (a no-op when the active budget has no
        /// <see cref="RenderBudget.MaxRenderTime"/>); throws
        /// <see cref="Heddle.Exceptions.TemplateRenderBudgetException"/> when the deadline has passed.</summary>
        void TickDeadline();
    }
}
