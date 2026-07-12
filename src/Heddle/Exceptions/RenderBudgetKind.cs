namespace Heddle.Exceptions
{
    /// <summary>Which <see cref="Heddle.Data.RenderBudget"/> limit a
    /// <see cref="TemplateRenderBudgetException"/> reports (C1-R5).</summary>
    public enum RenderBudgetKind
    {
        /// <summary><see cref="Heddle.Data.RenderBudget.MaxOutputChars"/> exceeded.</summary>
        OutputChars,

        /// <summary><see cref="Heddle.Data.RenderBudget.MaxRenderOps"/> exceeded.</summary>
        RenderOps,

        /// <summary><see cref="Heddle.Data.RenderBudget.MaxRenderTime"/> exceeded (limit/observed reported in
        /// milliseconds).</summary>
        RenderTime
    }
}
