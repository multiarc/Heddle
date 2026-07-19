using System.Collections.Generic;

namespace Heddle.Tests
{
    // Top-level model types so definition ':: PropArticle' etc. resolve by short name.

    public class PropArticle
    {
        public string Title { get; set; }
        public string Summary { get; set; }
    }

    public class PropMenuOption
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    public class PropMenu
    {
        public IEnumerable<PropMenuOption> Options { get; set; }
    }

    public class PropSite
    {
        public string DefaultCardStyle { get; set; }
    }

    /// <summary>Root model for the props/slot fixtures: carries the article, the menu, the site (for
    /// <c>::Site.*</c> root refs), and a <c>style</c> member (to exercise the HED5011 shadowing warning).</summary>
    public class PropRoot
    {
        public PropSite Site { get; set; }
        public PropArticle Article { get; set; }
        public PropMenu Menu { get; set; }
        public string style { get; set; }
    }

    public interface IContent { string Body { get; } }

    public class ArticleBody : IContent { public string Body { get; set; } }

    public class PropWidening
    {
        public byte B { get; set; }
        public long Count { get; set; }
    }
}
