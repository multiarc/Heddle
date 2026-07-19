namespace Heddle.Data
{
    /// <summary>
    /// Selects which expression tier is available inside call parentheses.
    /// </summary>
    public enum ExpressionMode
    {
        /// <summary>Strict pre-phase-1 surface: member paths, nested chains, and empty parameters only.</summary>
        MemberPathsOnly = 0,

        /// <summary>Default. Adds the sandbox-safe native expression tier (operators, literals, registered functions).</summary>
        Native = 1,

        /// <summary>Implies <see cref="Native"/>; additionally enables the inner-<c>@</c> Roslyn C# tier.</summary>
        FullCSharp = 2
    }
}
