namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Positions an extension within a branch set.</para>
    /// <para>The numeric values are a pinned contract (source generators decode them from Roslyn metadata);
    /// renumbering any member or reordering is a breaking change.</para>
    /// </summary>
    public enum BranchRole
    {
        /// <summary>Opens a set and publishes the initial <see cref="Heddle.Data.BranchState"/>; needs no predecessor.
        /// Built-ins: <c>@if</c>, <c>@ifnot</c>.</summary>
        Opener = 0,

        /// <summary>Requires a preceding opener and may republish <see cref="Heddle.Data.BranchState"/>.
        /// Built-in: <c>@elif</c>/<c>@elseif</c>.</summary>
        Continuation = 1,

        /// <summary>Closes a set and clears its state; optional and takes no condition.
        /// Built-in: <c>@else</c>.</summary>
        Terminal = 2
    }
}
