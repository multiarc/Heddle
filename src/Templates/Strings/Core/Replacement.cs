namespace Templates.Strings.Core {
    /// <summary>
    /// Represents parsed result data to replace source template template
    /// </summary>
    public struct Replacement
    {
        public BlockPosition BlockPosition;

        /// <summary>
        /// Replacement string
        /// </summary>
        public string ReplacementValue;
    }
}