namespace Heddle.LanguageServices
{
    /// <summary>Kind of an <see cref="ImportLink"/> (D16): an <c>@&lt;&lt;</c> import or an <c>@partial</c> target.</summary>
    public enum ImportLinkKind
    {
        Import,
        Partial
    }

    /// <summary>
    /// An <c>@&lt;&lt;</c> import or <c>@partial</c> target resolved with engine rules (D16);
    /// <see cref="ResolvedPath"/> is null when the file does not exist.
    /// </summary>
    public sealed class ImportLink
    {
        internal ImportLink(ImportLinkKind kind, int offset, int length, string rawPath, string resolvedPath)
        {
            Kind = kind;
            Offset = offset;
            Length = length;
            RawPath = rawPath;
            ResolvedPath = resolvedPath;
        }

        public ImportLinkKind Kind { get; }
        public int Offset { get; }
        public int Length { get; }
        public string RawPath { get; }
        public string ResolvedPath { get; }
    }
}
