using System;
using System.Collections.Generic;

namespace Heddle.Demo.Models
{
    // The published blog corpus, EXACTLY as docs/language-reference.md defines it. The docs and the demo must
    // never drift: completion/hover in the browser reflect over these types, so member names here are the demo's
    // contract. POCOs only — the assembly is fully trim-rooted by the WASM host.

    public class Blog
    {
        public string Title { get; set; }
        public int Year { get; set; }
        public ICollection<Article> Articles { get; set; }
    }

    public class Article
    {
        public string Title { get; set; }
        public Author Author { get; set; }
        public DateTime PublishedOn { get; set; }
        public bool IsFeatured { get; set; }
        public IList<string> Tags { get; set; }
        public ICollection<Comment> Comments { get; set; }
    }

    public class Author
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class Comment
    {
        public string Author { get; set; }
        public string Text { get; set; }
        public ICollection<Comment> Replies { get; set; }
    }
}
