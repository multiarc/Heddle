namespace Templates.Core.Data {
    internal enum TokenType {
        StartTTL,
        EndTTL,
        StartExtensionsBlock,
        EndExtensionsBlock,
        StartParameter,
        EndParameter,
        ValidIdentifier,
        Space,
        ExtensionDelimeter,
        TemplateBlock,
        Other
    }
}