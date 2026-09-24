using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    // Top-level model types so ':: LayoutCompPageModel' etc. resolve by short name through the
    // configured test-assembly namespaces (same pattern as RegionModels / FileWatcherConcreteModel).
    public class LayoutCompItem
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }

    public class LayoutCompPageModel
    {
        public string Title { get; set; }
        public int Year { get; set; }
        public List<LayoutCompItem> Items { get; set; }
    }

    /// <summary>A layout-chrome extension in the shape of the benchmark's composed-page components
    /// (assets/scripts fragments). Implements both value-position and render-position paths, the same
    /// contract the benchmark's <c>AbstractExtension</c> components hold.</summary>
    [ExtensionName("layoutcomp_assets")]
    public class LayoutCompAssetsExtension : AbstractExtension
    {
        public const string Fragment = "<link rel=\"stylesheet\" href=\"/assets/site.css\"><script defer src=\"/assets/site.js\"></script>";

        public override object ProcessData(in Scope scope) => Fragment;

        public override void RenderData(in Scope scope) => scope.Renderer.Render(Fragment);
    }

    /// <summary>
    /// Engine-level proof that the DOCUMENTED layout-composition idiom works at benchmark scale:
    /// a definition-only imported layout file (multi-KB literal chrome around a bare <c>@out()</c>),
    /// a page that imports it, overrides sections, fills public regions, and calls
    /// <c>@layout(){{ body }}</c>. Pins the semantics the benchmark's <c>composed-page</c> redesign
    /// (layout-as-definition instead of top-level chrome) will rely on, including the exact options
    /// the benchmark harness compiles with (FullCSharp, untyped model, file-resolved imports).
    /// </summary>
    public class LayoutDefinitionCompositionTests : IDisposable
    {
        private readonly string _dir;

        public LayoutDefinitionCompositionTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-layoutcomp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
            catch (IOException) { /* best-effort teardown */ }
            catch (UnauthorizedAccessException) { }
        }

        private void WriteFile(string name, string content)
            => File.WriteAllText(Path.Combine(_dir, name), content);

        private HeddleTemplate CompilePage(string pageSource, Type modelType,
            ExpressionMode mode = ExpressionMode.Native)
        {
            HeddleTemplate.Configure(typeof(LayoutDefinitionCompositionTests).GetTypeInfo().Assembly);
            RegisterExtensions();
            var options = new TemplateOptions
            {
                RootPath = _dir,
                FileNamePostfix = ".heddle",
                ExpressionMode = mode,
            };
            var context = modelType == null
                ? new CompileContext(options)
                : new CompileContext(options, modelType);
            return new HeddleTemplate(pageSource, context);
        }

        private static readonly object ExtensionGate = new object();
        private static bool _extensionsRegistered;

        /// <summary>Registered through <see cref="TemplateFactory.AddExtensions"/> (the
        /// BranchTestExtensions precedent) to avoid assembly-scan timing.</summary>
        private static void RegisterExtensions()
        {
            lock (ExtensionGate)
            {
                if (_extensionsRegistered) return;
                if (!TemplateFactory.Exists("layoutcomp_assets"))
                    TemplateFactory.AddExtensions(new[]
                        { new ExtensionType("layoutcomp_assets", typeof(LayoutCompAssetsExtension), false) });
                _extensionsRegistered = true;
            }
        }

        private static LayoutCompPageModel Model() => new LayoutCompPageModel
        {
            Title = "Composed Page",
            Year = 2026,
            Items = new List<LayoutCompItem>
            {
                new LayoutCompItem { Name = "Alpha", Id = 1 },
                new LayoutCompItem { Name = "Beta", Id = 2 },
                new LayoutCompItem { Name = "Gamma", Id = 3 },
            }
        };

        // ------------------------------------------------------------------ chrome builders

        /// <summary>Full-page chrome before the body slot: doctype, head with many meta tags, an inline
        /// style block with single CSS braces (spaced so no literal <c>{{</c>/<c>}}</c> pair forms —
        /// a doubled brace would open/close a subtemplate), header and nav. Benchmark-scale literal text.</summary>
        private static string ChromeBefore()
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>\n<html class=\"no-js\" lang=\"en\">\n<head>\n");
            sb.Append("    <meta charset=\"utf-8\">\n");
            sb.Append("    <meta http-equiv=\"x-ua-compatible\" content=\"ie=edge\">\n");
            sb.Append("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
            for (var i = 0; i < 12; i++)
                sb.Append("    <meta name=\"bench-meta-").Append(i)
                  .Append("\" content=\"benchmark-chrome-value-").Append(i).Append("-padding-0123456789\">\n");
            sb.Append("    <link rel=\"stylesheet\" href=\"/css/site.css\">\n");
            sb.Append("    <link rel=\"icon\" href=\"/favicon.ico\" type=\"image/x-icon\">\n");
            sb.Append("    <style>.wrap { margin: 0 auto; max-width: 960px; } .nav-item { display: inline-block; padding: 4px; }</style>\n");
            sb.Append("</head>\n<body>\n<header id=\"chrome-header\">\n    <nav class=\"wrap\">\n");
            for (var i = 0; i < 24; i++)
                sb.Append("        <a class=\"nav-item\" href=\"/section/").Append(i)
                  .Append("\">Section ").Append(i).Append(" listing page</a>\n");
            sb.Append("    </nav>\n</header>\n<main id=\"chrome-main\" class=\"wrap\">\n");
            return sb.ToString();
        }

        /// <summary>Chrome after the body slot: three footer columns of links, script tags, closing tags.</summary>
        private static string ChromeAfter()
        {
            var sb = new StringBuilder();
            sb.Append("\n</main>\n<footer id=\"chrome-footer\">\n");
            for (var col = 0; col < 3; col++)
            {
                sb.Append("    <ul class=\"footer-col-").Append(col).Append("\">\n");
                for (var i = 0; i < 8; i++)
                    sb.Append("        <li><a href=\"/footer/").Append(col).Append('/').Append(i)
                      .Append("\">Footer column ").Append(col).Append(" link ").Append(i).Append("</a></li>\n");
                sb.Append("    </ul>\n");
            }
            sb.Append("    <p class=\"legal\">All rights reserved. Served by the benchmark chrome.</p>\n");
            sb.Append("</footer>\n<script src=\"/js/vendor.js\"></script>\n<script src=\"/js/site.js\"></script>\n");
            sb.Append("</body>\n</html>\n");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ (a) large literal chrome

        [Fact]
        public void LargeChromeLayoutDefinition_RendersFullPage_ImportContributesNoStrayText()
        {
            var before = ChromeBefore();
            var after = ChromeAfter();
            Assert.True(before.Length + after.Length >= 4000,
                "chrome fixture must be benchmark-scale (>= 4 KB), got " + (before.Length + after.Length));

            WriteFile("layout.heddle",
                "IMPORTED-STATIC-MUST-NOT-RENDER\n" +
                "@%<layout>{{" + before + "@out()" + after + "}}%@\n");

            const string body = "<div id=\"page-body\">Body content</div>";
            var t = CompilePage("@<<{{layout.heddle}}\n@layout(){{" + body + "}}", typeof(object));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            var actual = t.Generate(null);
            Assert.DoesNotContain("IMPORTED-STATIC-MUST-NOT-RENDER", actual);
            // The import line is a directive line (trimmed under the default TrimDirectiveLines=true),
            // the imported file's static text never transfers, and @out() splices the caller body
            // verbatim — so the page is exactly chrome-before + body + chrome-after.
            Assert.Equal(before + body + after, actual);
        }

        // ------------------------------------------------------------------ (b) section defaults + override

        private const string SectionLayout =
            "@%\n" +
            "<meta_section>{{<title>Default Title</title>}}\n" +
            "<scripts_section>{{<script src=\"/app.js\"></script>}}\n" +
            "<layout>{{<head>@meta_section()</head><body><main>@out()</main>@scripts_section()</body>}}\n" +
            "%@\n";

        [Fact]
        public void SectionDefaults_PageOverrideWins_NonOverriddenDefaultsRender()
        {
            WriteFile("layout.heddle", SectionLayout);
            var t = CompilePage(
                "@<<{{layout.heddle}}\n" +
                "@%\n<meta_section:meta_section>{{<title>Overridden Title</title>}}\n%@\n" +
                "@layout(){{BODY}}",
                typeof(object));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(
                "<head><title>Overridden Title</title></head><body><main>BODY</main><script src=\"/app.js\"></script></body>",
                t.Generate(null));
        }

        [Fact]
        public void SectionDefaults_NoOverride_DefaultsRender()
        {
            WriteFile("layout.heddle", SectionLayout);
            var t = CompilePage("@<<{{layout.heddle}}\n@layout(){{BODY}}", typeof(object));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(
                "<head><title>Default Title</title></head><body><main>BODY</main><script src=\"/app.js\"></script></body>",
                t.Generate(null));
        }

        // ------------------------------------------------------------------ (c) public regions through import

        private const string RegionLayout =
            "@%<layout>{{" +
            "@%<:hero>{{<div class=\"hero-default\">Default hero</div>}}%@" +
            "<header>@hero()</header><main>@out()</main>" +
            "}}%@\n";

        [Fact]
        public void PublicRegion_CallBodyFillWins_OtherCallRendersDefault()
        {
            WriteFile("layout.heddle", RegionLayout);
            var t = CompilePage(
                "@<<{{layout.heddle}}\n" +
                "@layout(){{@%<hero:hero>{{<div class=\"hero-filled\">Filled hero</div>}}%@FIRST}}" +
                "|@layout(){{SECOND}}",
                typeof(object));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var parts = t.Generate(null).Split('|');
            Assert.Equal("<header><div class=\"hero-filled\">Filled hero</div></header><main>FIRST</main>", parts[0]);
            // The fill is call-scoped: the second document-scope call renders the region default.
            Assert.Equal("<header><div class=\"hero-default\">Default hero</div></header><main>SECOND</main>", parts[1]);
        }

        // ------------------------------------------------------------------ (d) typed model in chrome + body slot

        [Fact]
        public void TypedLayoutModel_ChromeReadsModelMembers_AroundSplicedBody()
        {
            WriteFile("layout.heddle",
                "@%<layout>{{<head><title>@(Title)</title></head><body>@out()" +
                "<footer>(c) @(Year) @(Title)</footer></body>}} :: LayoutCompPageModel%@\n");
            var t = CompilePage(
                "@<<{{layout.heddle}}\n@layout(){{<h1>@(Title)</h1>}}",
                typeof(LayoutCompPageModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(
                "<head><title>Composed Page</title></head><body><h1>Composed Page</h1>" +
                "<footer>(c) 2026 Composed Page</footer></body>",
                t.Generate(Model()));
        }

        // ------------------------------------------------------------------ (e) loop in the page body

        [Fact]
        public void LoopInPageBody_CallingImportedPartial_SplicesThroughOut()
        {
            WriteFile("layout.heddle",
                "@%\n" +
                "<item_partial>{{<li>@(Name)#@(Id)</li>}}\n" +           // abstract: binds to LayoutCompItem per call site
                "<layout>{{<main><ul>@out()</ul></main>}} :: LayoutCompPageModel\n" +
                "%@\n");
            var t = CompilePage(
                "@<<{{layout.heddle}}\n@layout(){{@list(Items){{@item_partial()}}}}",
                typeof(LayoutCompPageModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(
                "<main><ul><li>Alpha#1</li><li>Beta#2</li><li>Gamma#3</li></ul></main>",
                t.Generate(Model()));
        }

        // ------------------------------------------------------------------ (f) nested import chain

        [Fact]
        public void NestedImportChain_BaseLibDefinitionsFlowThroughLayoutLib()
        {
            WriteFile("baselib.heddle",
                "@%<base_badge>{{<b>base</b>}}%@\n");
            WriteFile("layoutlib.heddle",
                "@<<{{baselib.heddle}}\n" +
                "@%<layout>{{<header>@base_badge()</header><main>@out()</main>}}%@\n");
            var t = CompilePage(
                "@<<{{layoutlib.heddle}}\n@layout(){{body @base_badge()}}",
                typeof(object));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            // base_badge is callable both from the layout's chrome and directly from the page body.
            Assert.Equal("<header><b>base</b></header><main>body <b>base</b></main>", t.Generate(null));
        }

        // ------------------------------------------------------------------ (g) extension call inside layout chrome

        [Fact]
        public void ExtensionCallInsideLayoutDefinition_RendersRegisteredFragment()
        {
            WriteFile("layout.heddle",
                "@%<layout>{{<head>@layoutcomp_assets()</head><body>@out()</body>}}%@\n");
            var t = CompilePage("@<<{{layout.heddle}}\n@layout(){{BODY}}", typeof(object));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(
                "<head>" + LayoutCompAssetsExtension.Fragment + "</head><body>BODY</body>",
                t.Generate(null));
        }

        // ------------------------------------------------------------------ (h) the benchmark harness shape

        /// <summary>
        /// The exact compile shape of the benchmark's composed-page workload
        /// (<c>benchmarks/dotnet/src/Engines/HeddleEngine.cs</c>): template resolved by NAME from
        /// <see cref="TemplateOptions.RootPath"/> with <c>FileNamePostfix</c>, untyped model,
        /// <see cref="ExpressionMode.FullCSharp"/>, <see cref="OutputProfile.Text"/>,
        /// <c>ProvideLanguageFeatures = false</c> — but with the layout restructured as a
        /// definition-only import, which is what the redesign will do.
        /// </summary>
        [Fact]
        public void BenchmarkShapedCompile_FullCSharp_UntypedModel_FileResolvedLayoutDefinition()
        {
            var before = ChromeBefore();
            var after = ChromeAfter();
            WriteFile("layout.heddle",
                "@%\n" +
                "<meta_section>{{<title>Default Title</title>}}\n" +
                "<layout>{{" + before + "@meta_section()@layoutcomp_assets()@out()" + after + "}}\n" +
                "%@\n");
            WriteFile("home.heddle",
                "@<<{{layout.heddle}}\n" +
                "@%\n<meta_section:meta_section>{{<title>Home</title>}}\n%@\n" +
                "@layout(){{<div class=\"home-content\">Welcome</div>}}");

            HeddleTemplate.Configure(typeof(LayoutDefinitionCompositionTests).GetTypeInfo().Assembly);
            RegisterExtensions();
            var options = new TemplateOptions("home")
            {
                FileNamePostfix = ".heddle",
                RootPath = _dir,
                OutputProfile = OutputProfile.Text,
                ExpressionMode = ExpressionMode.FullCSharp,
                ProvideLanguageFeatures = false,
            };
            var t = new HeddleTemplate(new CompileContext(options));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            var actual = t.Generate(new object());
            Assert.Equal(
                before + "<title>Home</title>" + LayoutCompAssetsExtension.Fragment +
                "<div class=\"home-content\">Welcome</div>" + after,
                actual);
        }

        // ------------------------------------------------------------------ chrome-authoring hazard pins

        /// <summary>Single CSS/JS braces inside a multi-KB definition body are plain literal text —
        /// the subtemplate delimiters are strictly the two-character <c>{{</c>/<c>}}</c> tokens.</summary>
        [Fact]
        public void SingleBracesInChrome_AreLiteralText()
        {
            WriteFile("layout.heddle",
                "@%<layout>{{<style>.a { color: red; } .b { color: blue; }</style>@out()}}%@\n");
            var t = CompilePage("@<<{{layout.heddle}}\n@layout(){{X}}", typeof(object));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("<style>.a { color: red; } .b { color: blue; }</style>X", t.Generate(null));
        }

        /// <summary>The hazard the benchmark chrome rewrite must respect: an adjacent <c>}}</c> pair
        /// (e.g. minified CSS <c>.a{.b{…}}</c>) is the subtemplate CLOSE token. Inside a definition
        /// body it terminates the body early and the compile fails — it does not silently truncate
        /// the page. Minified nested closers must be spaced (<c>} }</c>) or the CSS left unminified.</summary>
        [Fact]
        public void AdjacentClosingBracesInChrome_TerminateTheDefinitionBody_CompileFails()
        {
            WriteFile("layout.heddle",
                "@%<layout>{{<style>@media print{.a{display:none}}</style>@out()}}%@\n");
            var t = CompilePage("@<<{{layout.heddle}}\n@layout(){{X}}", typeof(object));
            Assert.False(t.CompileResult.Success,
                "an unescaped '}}' inside a definition body must not compile silently; rendered: "
                + (t.CompileResult.Success ? t.Generate(null) : "<no render>"));
        }

        /// <summary>The flagship shape once more under the default Native tier, proving the idiom is
        /// mode-independent (the other facts above already run Native; this is the FullCSharp twin of
        /// <see cref="LargeChromeLayoutDefinition_RendersFullPage_ImportContributesNoStrayText"/>).</summary>
        [Fact]
        public void LargeChromeLayoutDefinition_FullCSharpMode_SameBytes()
        {
            var before = ChromeBefore();
            var after = ChromeAfter();
            WriteFile("layout.heddle", "@%<layout>{{" + before + "@out()" + after + "}}%@\n");

            const string page = "@<<{{layout.heddle}}\n@layout(){{<div id=\"page-body\">Body content</div>}}";
            var native = CompilePage(page, typeof(object), ExpressionMode.Native);
            Assert.True(native.CompileResult.Success, native.CompileResult.ToString());
            var full = CompilePage(page, typeof(object), ExpressionMode.FullCSharp);
            Assert.True(full.CompileResult.Success, full.CompileResult.ToString());
            Assert.Equal(native.Generate(null), full.Generate(null));
        }
    }
}
