using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The hosted arms of <see cref="TemplateResolver"/> — <c>View</c>, <c>PartialView</c> and <c>Master</c> — read
    /// from disk. Their candidate locations were written with backslashes and a leading one, which is a path only
    /// Windows reads and, even there, one rooted at the current drive rather than under the resolver's root. Off
    /// Windows a backslash is an ordinary file-name character, so every candidate was a single long file name that
    /// <c>File.Exists</c> never matched and the hosted arms found nothing on disk at all.
    /// <para>Nothing exercised these three arms; the whole class was reachable only through
    /// <c>TemplatePathType.None</c>.</para>
    /// </summary>
    public class HostedTemplateResolverTests : IDisposable
    {
        private readonly string _root;

        public HostedTemplateResolverTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "heddle-hosted-" + Guid.NewGuid().ToString("N"));
            Write("views/home/index.heddle", "[home-index]");
            Write("views/index.heddle", "[shared-index]");
            Write("views/partial/home/bit.heddle", "[home-bit]");
            Write("views/partial/bit.heddle", "[shared-bit]");
            Write("views/base/home/layout.heddle", "[home-layout]");
            Write("views/base/layout.heddle", "[shared-layout]");
        }

        private void Write(string relative, string content)
        {
            var full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>The resolver takes the <b>directory</b> of what it is given, so it is handed an anchor file
        /// inside the root the way a host hands it a config path.</summary>
        private TemplateResolver Resolver() => new TemplateResolver(Path.Combine(_root, "anchor.txt"), false);

        [Theory]
        [InlineData("index", "home", TemplatePathType.View, "views/home/index.heddle")]
        [InlineData("index", "", TemplatePathType.View, "views/index.heddle")]
        [InlineData("index.heddle", "home", TemplatePathType.View, "views/home/index.heddle")]
        [InlineData("~/index", "", TemplatePathType.View, "views/index.heddle")]
        [InlineData("home/index", "", TemplatePathType.View, "views/home/index.heddle")]
        [InlineData("bit", "home", TemplatePathType.PartialView, "views/partial/home/bit.heddle")]
        [InlineData("bit", "", TemplatePathType.PartialView, "views/partial/bit.heddle")]
        [InlineData("layout", "home", TemplatePathType.Master, "views/base/home/layout.heddle")]
        [InlineData("layout", "", TemplatePathType.Master, "views/base/layout.heddle")]
        public void AHostedSearchFindsTheFileOnDisk(string viewName, string controller, TemplatePathType type,
            string expected)
        {
            var found = Resolver().Search(viewName, controller, type, out var searched, out var cached);

            Assert.Null(cached);
            Assert.Null(searched);
            Assert.NotNull(found);
            Assert.Equal(Path.Combine(_root, expected.Replace('/', Path.DirectorySeparatorChar)),
                Path.GetFullPath(found));
        }

        /// <summary>The near neighbour: a name no location holds still finds nothing, so the rows above are about
        /// the candidates being real paths rather than about the search accepting anything.</summary>
        [Fact]
        public void AHostedSearchForANameNoLocationHoldsFindsNothing()
        {
            var found = Resolver().Search("nosuchview", "home", TemplatePathType.View, out var searched, out _);

            Assert.Null(found);
            Assert.NotNull(searched);
            var locations = searched.ToList();
            Assert.NotEmpty(locations);
            // Every reported location is a path that was actually probed, not the pattern it was built from.
            Assert.All(locations, l => Assert.DoesNotContain("{0}", l));
            Assert.All(locations, l => Assert.DoesNotContain("{1}", l));
            Assert.All(locations, l => Assert.Contains("nosuchview.heddle", l));
        }

        /// <summary>The controller-specific location wins over the shared one, which is what having an ordered
        /// list of candidates is for. Both files exist, so this says the order decides rather than the existence.
        /// </summary>
        [Fact]
        public void TheControllerLocationIsPreferredOverTheSharedOne()
        {
            var resolver = Resolver();
            var withController = resolver.Search("index", "home", TemplatePathType.View, out _, out _);
            var withoutController = resolver.Search("index", "", TemplatePathType.View, out _, out _);

            Assert.Equal("[home-index]", File.ReadAllText(withController));
            Assert.Equal("[shared-index]", File.ReadAllText(withoutController));
        }

        /// <summary>A parent-directory specifier is refused before any location is built, on every arm.</summary>
        [Fact]
        public void AParentDirectorySpecifierIsRefused()
        {
            var resolver = Resolver();
            Assert.Throws<ArgumentException>(() =>
                resolver.Search("../index", "home", TemplatePathType.View, out _, out _));
        }

        /// <summary>The reported locations name real candidate paths under the resolver's root.</summary>
        [Fact]
        public void ReportedLocationsAreUnderTheResolversRoot()
        {
            Resolver().Search("nosuchview", "home", TemplatePathType.PartialView, out var searched, out _);

            var locations = (IEnumerable<string>) searched;
            Assert.All(locations, l => Assert.StartsWith(_root, Path.GetFullPath(l), StringComparison.Ordinal));
        }
    }
}
