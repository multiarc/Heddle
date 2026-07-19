namespace Heddle.Data
{
    /// <summary>
    /// <para>The public branch-set protocol state, published under <see cref="ReservedKey"/> by
    /// <c>@if</c>/<c>@ifnot</c>/<c>@elif</c> and consumed/cleared by <c>@else</c>. Custom extensions may
    /// read it to render alongside a set or publish it to drive one (e.g. a matcher publishing
    /// <c>new BranchState(true)</c> so a following <c>@else</c> stays silent).</para>
    /// <para>Immutable value type — safe to pass across the render lineage; a frame belongs to a single
    /// body execution of a single render invocation.</para>
    /// </summary>
    public readonly struct BranchState
    {
        /// <summary>The reserved channel key of the branch protocol: <c>"heddle.branch"</c>.</summary>
        public const string ReservedKey = "heddle.branch";

        /// <summary>
        /// Creates a state with the given satisfaction. Publish it under <see cref="ReservedKey"/> to drive
        /// a set — e.g. a custom matcher publishing <c>new BranchState(true)</c> so a following
        /// <c>@else</c> renders nothing.
        /// </summary>
        /// <param name="satisfied">Whether some branch of the current set has already fired.</param>
        public BranchState(bool satisfied)
        {
            Satisfied = satisfied;
        }

        /// <summary>
        /// True when some branch of the current set has already fired (condition semantics — an
        /// <c>@if</c> whose condition was true but whose body rendered empty still satisfies the set).
        /// </summary>
        public bool Satisfied { get; }
    }
}
