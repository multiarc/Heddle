using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// Which <c>@&lt;&lt;</c> composition import a chain came from: where the expansion was written, and
    /// — on the recording build alone — the text it expanded.
    /// <para>An import expands inline: its chains compile into the importing document and get no document
    /// of their own, while their positions stay absolute in <see cref="Text"/> — the file the importing
    /// document does not contain. <see cref="Text"/> is that file's text, so a position is indexable
    /// again; <see cref="RootSite"/> is where a reader of the compiled template can act on it.</para>
    /// <para>Immutable and linked outward rather than re-anchored in place: a nested import is expanded
    /// while its importer's marker is the current one, so the chain of <see cref="Outer"/> links already
    /// spells the nesting. <see cref="Text"/> is the innermost file (the one the positions belong to) and
    /// <see cref="RootSite"/> the outermost import block (the one the root document contains), which is the
    /// same pairing <see cref="ImportOrigin"/> keeps by mutating a shared instance.</para>
    /// </summary>
    internal sealed class ImportSource
    {
        internal ImportSource(string text, BlockPosition site, ImportSource outer)
        {
            Text = text;
            Site = site;
            Outer = outer;
        }

        /// <summary>The imported file's text exactly as the parse consumed it — never a re-read, which
        /// could answer with a different byte sequence than the one the positions were taken from. Null unless
        /// <see cref="ParserSettings.CaptureImportSource"/> was on: only the recording build slices out of it,
        /// while every tier needs <see cref="RootSite"/> to tell one import's sites from another's.
        /// <para>Released once the record has sliced what it needs — see
        /// <c>FormRecord.ReleaseImportTexts</c> — so a partial expanded many times does not pin its text for
        /// the life of the build.</para></summary>
        internal string Text { get; set; }

        /// <summary>The <c>@&lt;&lt;</c> block's absolute position in the document that wrote it.</summary>
        internal BlockPosition Site { get; }

        /// <summary>The import this one was expanded inside, or null when the importer is the root
        /// document.</summary>
        internal ImportSource Outer { get; }

        /// <summary>The outermost import block: a position in the root document, whatever the nesting
        /// depth. The only site a reader of the root template can act on.</summary>
        internal BlockPosition RootSite
        {
            get
            {
                var source = this;
                while (source.Outer != null)
                    source = source.Outer;
                return source.Site;
            }
        }
    }
}
