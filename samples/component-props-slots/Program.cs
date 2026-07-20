using System;
using System.Collections.Generic;
using System.Globalization;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Samples.ComponentPropsSlots
{
    // Sample 7 — a small component library: typed props (defaults, overrides, required) and parameterized slots.
    // A <card> component with three props and an @out() slot, and a <picker> declaring an `out:: MenuOption` slot
    // parameter it projects per item, driven by a page template with named arguments.
    public sealed class Article
    {
        public string Title { get; set; }
        public string Summary { get; set; }
    }

    public sealed class MenuOption
    {
        public string Id { get; set; }
        public string Label { get; set; }
    }

    public sealed class Menu
    {
        public IList<MenuOption> Options { get; set; }
    }

    public sealed class Page
    {
        public Article Featured { get; set; }
        public Menu Nav { get; set; }
    }

    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            HeddleTemplate.Configure(typeof(Program).Assembly);

            const string template = """
                @%
                <card(style: string = "plain", compact: bool = false, badge: string)>{{
                  <article class="card @(style)">
                    <h2>@(Title)</h2>
                    @ifnot(compact){{<p>@(Summary)</p>}}
                    <span class="badge">@(badge)</span>
                    <div class="slot">@out()</div>
                  </article>
                }} :: Article
                <picker(out:: MenuOption)>{{
                  <ul class="picker">@list(Options){{<li>@out(this)</li>}}</ul>
                }} :: Menu
                %@
                @model(){{Page}}
                <main>
                  @card(Featured, badge: "hot"){{<em>read more</em>}}
                  @card(Featured, style: "wide", compact: true, badge: "sale")
                  @picker(Nav){{<a href="/go/@(Id)">@(Label)</a>}}
                </main>
                """;

            var model = new Page
            {
                Featured = new Article { Title = "Native Expressions Ship", Summary = "The sandbox-safe tier is here." },
                Nav = new Menu
                {
                    Options = new List<MenuOption>
                    {
                        new MenuOption { Id = "home", Label = "Home" },
                        new MenuOption { Id = "docs", Label = "Docs" }
                    }
                }
            };

            using var t = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), typeof(Page)));
            if (!t.CompileResult.Success)
                throw new InvalidOperationException("compile failed: " + t.CompileResult);
            var output = t.Generate(model);

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                SampleCapture.Write(capture, "components-page.html", output);
                Console.WriteLine("captured components-page.html");
                return 0;
            }

            Console.WriteLine(output);
            return 0;
        }
    }
}
