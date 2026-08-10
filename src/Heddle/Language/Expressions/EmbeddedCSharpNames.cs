namespace Heddle.Language.Expressions
{
    /// <summary>
    /// Identifiers that user C# expressions may bind in embedded contexts. Declared as parameter lists
    /// in embedded <c>.tcs</c> templates; renaming changes semantics on the dynamic tier silently. A pin test
    /// ensures embedded resources match this contract exactly, in order.
    /// </summary>
    internal static class EmbeddedCSharpNames
    {
        /// <summary>The current model — the only one the precompiled tier can reproduce today.</summary>
        internal const string Model = "model";

        internal const string Chained = "chained";

        internal const string Root = "root";
    }
}
