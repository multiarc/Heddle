using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// Import-provenance marker stamped onto diagnostics and parse contexts when
    /// <see cref="Data.TemplateOptions.ProvideLanguageFeatures"/> is on. <see cref="Path"/> is the resolved
    /// absolute path of the deepest origin file; <see cref="Site"/> is the import/partial site span in the
    /// current document. Deliberately mutable — instance sharing enables nested re-anchoring (outer imports
    /// re-anchor <see cref="Site"/> while <see cref="Path"/> keeps the deepest file).
    /// </summary>
    internal sealed class ImportOrigin
    {
        internal ImportOrigin(string path, BlockPosition site)
        {
            Path = path;
            Site = site;
        }

        internal string Path { get; set; }

        internal BlockPosition Site { get; set; }
    }
}
