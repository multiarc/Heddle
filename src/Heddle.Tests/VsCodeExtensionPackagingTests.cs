using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Packaging pins for the VS Code extension. The failure mode is not a missing file — it is a manifest
    /// naming an entry point the packaging no longer produces in a usable form, which installs cleanly and
    /// throws at activation on a user's machine. That chain crosses four files, and any link breaking ships a
    /// broken extension, so each link is pinned here.
    /// </summary>
    public class VsCodeExtensionPackagingTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(BuildSurfaceContractTests.FindRepoFile(relative.Replace('/', Path.DirectorySeparatorChar)))
                .Replace("\r\n", "\n");

        /// <summary>Link 1: the dependency tree is not shipped as loose files beside the bundle.</summary>
        [Fact]
        public void TheVscodeignoreExcludesNodeModules()
        {
            var ignore = Read("editors/vscode/.vscodeignore");
            Assert.Contains("node_modules/**", ignore, StringComparison.Ordinal);
        }

        /// <summary>Link 2: <c>main</c> names the bundle, and the only hook <c>vsce</c> runs produces it.
        /// A bundle step outside <c>vscode:prepublish</c> never runs in CI.</summary>
        [Fact]
        public void TheManifestNamesTheBundleAndPrepublishTypeChecksAndBundlesIt()
        {
            var manifest = Read("editors/vscode/package.json");

            Assert.Contains("\"main\": \"./dist/extension.js\"", manifest, StringComparison.Ordinal);

            var prepublish = Regex.Match(manifest, @"""vscode:prepublish"":\s*""(?<c>[^""]*)""");
            Assert.True(prepublish.Success, "vscode:prepublish not found in editors/vscode/package.json");
            var command = prepublish.Groups["c"].Value;
            Assert.True(command.Contains("npm run check-types", StringComparison.Ordinal),
                "vscode:prepublish must run check-types (esbuild does not type-check): " + command);
            Assert.True(command.Contains("npm run bundle", StringComparison.Ordinal),
                "vscode:prepublish must run bundle (vsce runs no other hook): " + command);
        }

        /// <summary>Link 3: the bundler produces a single node/CJS bundle at that path, with only the host's
        /// own <c>vscode</c> module left external.</summary>
        [Fact]
        public void TheBundlerProducesOneNodeCjsBundleAtTheManifestEntryPoint()
        {
            var bundler = Read("editors/vscode/build/bundle.js");

            Assert.Contains("bundle: true", bundler, StringComparison.Ordinal);
            Assert.Contains("platform: 'node'", bundler, StringComparison.Ordinal);
            Assert.Contains("format: 'cjs'", bundler, StringComparison.Ordinal);
            Assert.Contains("external: ['vscode']", bundler, StringComparison.Ordinal);
            Assert.Contains("outfile: path.join(root, 'dist', 'extension.js')", bundler, StringComparison.Ordinal);
        }

        /// <summary>Link 4: <c>tsc</c> cannot write that path, so no invocation in the directory can replace
        /// the bundle with unbundled output.</summary>
        [Fact]
        public void TheTypeScriptConfigEmitsNothing()
        {
            var config = Read("editors/vscode/tsconfig.json");

            Assert.Contains("\"noEmit\": true", config, StringComparison.Ordinal);
            Assert.DoesNotContain("\"outDir\"", config, StringComparison.Ordinal);
        }
    }
}
