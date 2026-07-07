using System;
using System.Collections.Generic;

namespace Heddle.Demo.Models
{
    /// <summary>One selectable demo model set: a root type, a deterministic canned instance, and the starter
    /// template shown when the set is selected (the template carries its own <c>@model</c> directive so analysis
    /// and render agree on the root type with no facade change).</summary>
    public sealed class DemoModelSet
    {
        private readonly Func<object> _factory;

        internal DemoModelSet(string id, string displayName, Type rootType, string starterTemplate, Func<object> factory)
        {
            Id = id;
            DisplayName = displayName;
            RootType = rootType;
            StarterTemplate = starterTemplate;
            _factory = factory;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public Type RootType { get; }
        public string StarterTemplate { get; }

        /// <summary>A fresh deterministic instance of <see cref="RootType"/> (fixed dates/order — the render golden
        /// depends on it).</summary>
        public object CreateInstance() => _factory();
    }

    /// <summary>The model-set registry the demo page's picker binds to (phase 9 D5). v1 ships two sets.</summary>
    public static class DemoCatalog
    {
        // The blog starter: @model pins the root type (resolvable because the host registers this assembly via
        // HeddleTemplate.Configure). Native expressions + Html profile only — no C# tier in the browser.
        private const string BlogStarter =
            "@using(){{Heddle.Demo.Models}}\n" +
            "@model(){{Blog}}\n" +
            "<section class=\"blog\">\n" +
            "  <h1>@(Title)</h1>\n" +
            "  <p class=\"year\">Year @(Year)</p>\n" +
            "@list(Articles){{  <article>\n" +
            "    <h2>@(Title)</h2>\n" +
            "    <p class=\"byline\">by @(Author.Name)</p>\n" +
            "@if(IsFeatured){{    <p class=\"badge\">Featured</p>\n}}  </article>\n}}" +
            "</section>\n";

        // The article-card starter pairs an Article root with a <card> component definition (props + a slot).
        private const string ArticleCardStarter =
            "@using(){{Heddle.Demo.Models}}\n" +
            "@model(){{Article}}\n" +
            "@%\n" +
            "<card(style: string = \"plain\")>{{<article class=\"card @(style)\"><h2>@(Title)</h2>" +
            "<p class=\"byline\">by @(Author.Name)</p>@out()</article>}} :: Article\n" +
            "%@\n" +
            "@card(this, style: \"wide\"){{<p class=\"tags\">@list(Tags){{<span>@(this) </span>}}</p>}}\n";

        private static Blog CreateBlog()
        {
            return new Blog
            {
                Title = "Heddle Weekly",
                Year = 2026,
                Articles = new List<Article>
                {
                    CreateFeaturedArticle(),
                    new Article
                    {
                        Title = "Streaming SSR",
                        Author = new Author { Name = "Alan Turing", Email = "alan@example.com" },
                        PublishedOn = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
                        IsFeatured = false,
                        Tags = new List<string> { "async", "streaming" },
                        Comments = new List<Comment>()
                    }
                }
            };
        }

        private static Article CreateFeaturedArticle()
        {
            return new Article
            {
                Title = "Native Expressions Ship",
                Author = new Author { Name = "Ada Lovelace", Email = "ada@example.com" },
                PublishedOn = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                IsFeatured = true,
                Tags = new List<string> { "release", "engine" },
                Comments = new List<Comment>
                {
                    new Comment { Author = "Grace", Text = "Great work!", Replies = new List<Comment>() }
                }
            };
        }

        private static readonly DemoModelSet BlogSet =
            new DemoModelSet("blog", "Blog", typeof(Blog), BlogStarter, CreateBlog);

        private static readonly DemoModelSet ArticleCardSet =
            new DemoModelSet("article-card", "Article card", typeof(Article), ArticleCardStarter, CreateFeaturedArticle);

        public static IReadOnlyList<DemoModelSet> Sets { get; } = new[] { BlogSet, ArticleCardSet };

        /// <summary>The set with this id; throws on an unknown id (a host bug — ids come from <see cref="Sets"/>).</summary>
        public static DemoModelSet Get(string id)
        {
            foreach (var set in Sets)
                if (string.Equals(set.Id, id, StringComparison.Ordinal))
                    return set;
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown demo model set id.");
        }
    }
}
