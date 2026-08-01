using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// A hosted arm must be able to <b>load</b> the file its own search just found. The search returns
    /// <c>&lt;root&gt;/views/{controller}/{view}.heddle</c>, but the options a hosted arm built kept only the file
    /// name, so <see cref="TemplateOptions.FullPath"/> composed <c>&lt;root&gt;/{view}.heddle</c> — the
    /// <c>views/{controller}/</c> segment the search itself had inserted was dropped, and the reader looked for a
    /// file nobody had probed.
    /// <para>The sibling suite stops at <c>Search</c>, which is why this survived: every hosted assertion there
    /// reads the returned path with <c>File.ReadAllText</c> rather than through the resolver's own compile.</para>
    /// <para>The meanings this pins: <c>RootPath</c> is the resolver root, and <c>TemplateName</c> is the
    /// template's path <b>relative to it</b>, without the extension — the same reading the precompiled partial
    /// path already used.</para>
    /// </summary>
    public class HostedTemplateLoadTests : IDisposable
    {
        private readonly string _root;

        public HostedTemplateLoadTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "heddle-hosted-load-" + Guid.NewGuid().ToString("N"));
            Write("views/home/index.heddle", "[home-index]");
            Write("views/index.heddle", "[shared-index]");
            Write("views/partial/home/bit.heddle", "[home-bit]");
            Write("views/partial/bit.heddle", "[shared-bit]");
        }

        private void Write(string relativePath, string content)
        {
            var full = Path.Combine(_root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        // The ctor takes a file *inside* the root and resolves to its directory.
        private TemplateResolver Resolver() => new TemplateResolver(Path.Combine(_root, "anchor.txt"), false);

        [Theory]
        [InlineData("index", "home", TemplatePathType.View, "[home-index]")]
        [InlineData("index", "", TemplatePathType.View, "[shared-index]")]
        [InlineData("index.heddle", "home", TemplatePathType.View, "[home-index]")]
        [InlineData("~/index", "home", TemplatePathType.View, "[home-index]")]
        [InlineData("bit", "home", TemplatePathType.PartialView, "[home-bit]")]
        [InlineData("bit", "", TemplatePathType.PartialView, "[shared-bit]")]
        public void AHostedGetTemplateLoadsTheFileTheSearchFound(string viewName, string controllerName,
            TemplatePathType searchType, string expected)
        {
            var template = Resolver().GetTemplate(viewName, controllerName, out var searched, null, searchType);

            Assert.NotNull(template);
            Assert.Null(searched);
            Assert.True(template.CompileResult.Success,
                viewName + " => " + template.CompileResult);
            Assert.Equal(expected, template.Generate(null));
        }

        /// <summary>The composition itself: the options a hosted arm builds must name the file on disk, with the
        /// root still the root — the staleness check re-derives a path from <c>RootPath</c> and the manifest key,
        /// so pushing the directory into the root instead would double the segment.</summary>
        [Theory]
        [InlineData("index", "home", TemplatePathType.View, "views/home/index.heddle")]
        [InlineData("bit", "", TemplatePathType.PartialView, "views/partial/bit.heddle")]
        public void TheComposedFullPathIsThePathThatWasProbed(string viewName, string controllerName,
            TemplatePathType searchType, string expectedRelative)
        {
            var resolver = Resolver();
            var found = resolver.Search(viewName, controllerName, searchType, out _, out _);
            var template = resolver.GetTemplate(viewName, controllerName, out _, null, searchType);

            Assert.NotNull(found);
            Assert.Equal(Path.GetFullPath(Path.Combine(_root, expectedRelative)), Path.GetFullPath(found));
            Assert.True(template.CompileResult.Success);
            // What the reader actually opened is the file the search returned.
            Assert.Equal(File.ReadAllText(found), template.Generate(null));
        }

        /// <summary>The near-neighbour that must keep working: the non-hosted arm, whose caller supplies the
        /// root-relative path itself and which was never broken.</summary>
        [Fact]
        public void TheNonHostedArmStillLoads()
        {
            var template = Resolver().GetTemplate("views/home/index.heddle", string.Empty, out var searched);

            Assert.NotNull(template);
            Assert.Null(searched);
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Equal("[home-index]", template.Generate(null));
        }

        /// <summary>A miss still reports the locations it probed and returns nothing, rather than throwing on the
        /// way to composing options for a path that was never found.</summary>
        [Fact]
        public void AMissStillReportsTheProbedLocations()
        {
            var resolver = Resolver();
            var found = resolver.Search("nosuchview", "home", TemplatePathType.View, out var searched, out _);

            Assert.Null(found);
            var locations = new List<string>(searched);
            Assert.NotEmpty(locations);
            foreach (var location in locations)
            {
                Assert.DoesNotContain("{0}", location);
                Assert.DoesNotContain("{1}", location);
                Assert.Contains("nosuchview.heddle", location);
            }
        }
    }
}
