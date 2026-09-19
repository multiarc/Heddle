namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>Bounds the writer enforces and the reader re-checks, so an artifact the build wrote
    /// always loads and a malformed one is refused instead of exhausting the stack.</summary>
    internal static class CompiledFormLimits
    {
        /// <summary>How deeply expressions and chains (a chain inside a chain parameter) may nest. The
        /// recursive readers spend one frame per level, and an artifact is untrusted bytes at load:
        /// without a bound a deep enough tree ends the process with a <c>StackOverflowException</c>,
        /// which nothing can catch. The value is far above any template's real depth (the parser
        /// bounds its own nesting at 250; a left-associative operator chain yields one level per
        /// operator) and far below what a 1 MB thread can hold.</summary>
        internal const int MaxNesting = 1024;
    }
}
