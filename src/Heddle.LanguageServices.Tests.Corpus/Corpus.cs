using System.Collections.Generic;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

// The corpus export assembly (phase 6 D23/D24): one [ExportExtensions] extension and one [ExportFunctions]
// container. Loaded by the facade's one-shot scan into the default ALC; also loaded (again) as a model assembly
// into the collectible ALC for typed completion/hover.
[assembly: ExportExtensions(typeof(Corpus.BadgeExtension))]
[assembly: ExportFunctions(typeof(Corpus.CorpusFunctions))]

namespace Corpus
{
    public class Author
    {
        public string Name { get; set; }
    }

    public class Article
    {
        public string Title { get; set; }
        public string Summary { get; set; }
        public Author Author { get; set; }
        public int Rating { get; set; }
    }

    public class Menu
    {
        public string Title { get; set; }
        public IReadOnlyList<Article> Items { get; set; }
    }

    public class Blog
    {
        public List<Article> Articles { get; set; }
        public string Title { get; set; }
    }

    public class Page
    {
        public Article Article { get; set; }
        public Menu Menu { get; set; }
    }

    /// <summary>A public static container whose <c>TitleCase</c> method exports the function <c>titlecase</c> (D24).</summary>
    public static class CorpusFunctions
    {
        public static string TitleCase(string value) => value;
    }

    /// <summary>A trivial extension exported under the name <c>badge</c> (D23).</summary>
    [ExtensionName("badge")]
    public class BadgeExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => null;

        public override void RenderData(in Scope scope)
        {
        }
    }
}
