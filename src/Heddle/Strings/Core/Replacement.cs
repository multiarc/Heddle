namespace Heddle.Strings.Core {
    /// <summary>
    /// Represents parsed result data to replace source template
    /// </summary>
    public struct Replacement
    {
        /// <summary>The span of source text being replaced.</summary>
        public BlockPosition BlockPosition;

        /// <summary>The text that takes the span's place; null is treated as empty text.</summary>
        public string ReplacementValue;
    }
}