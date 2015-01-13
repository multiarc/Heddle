namespace Templates.Language {
    /// <summary>
    /// Template configuration (language elements)
    /// </summary>
    public static class ParserConfiguration {
        public const string SequenceLiteral = "%";
        public const string StartTtl = "<%";

        public const string EndTtl = "%>";

        public const string StartExtensionsBlock = "<";

        public const string EndExtensionsBlock = ">";

        public const string StartParameter = "[";

        public const string EndParameter = "]";

        public const string ExtensionDelimeter = ":";
    }
}