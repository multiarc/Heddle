namespace Templates.Core.CompilerServices {
    /// <summary>
    /// Template configuration (language elements)
    /// </summary>
    internal static class ParserConfiguration {
        public const string SequenceLiteral = "%";
        public const string StartTTL = "<%";

        public const string EndTTL = "%>";

        public const string StartExtensionsBlock = "<";

        public const string EndExtensionsBlock = ">";

        public const string StartParameter = "[";

        public const string EndParameter = "]";

        public const string ExtensionDelimeter = ":";
    }
}