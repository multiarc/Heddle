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
    /// broken extension, so each link is pinned here — and, since text cannot prove a bundle loads, so is the
    /// CI step that loads the packaged one.
    /// </summary>
    public class VsCodeExtensionPackagingTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(BuildSurfaceContractTests.FindRepoFile(relative.Replace('/', Path.DirectorySeparatorChar)))
                .Replace("\r\n", "\n");

        /// <summary>Link 1: the dependency tree is not shipped as loose files beside the bundle. A whole,
        /// uncommented line: <c># node_modules/**</c> contains the pattern and excludes nothing.</summary>
        [Fact]
        public void TheVscodeignoreExcludesNodeModules()
        {
            var ignore = Read("editors/vscode/.vscodeignore");
            Assert.Matches(@"(?m)^node_modules/\*\*[ \t]*$", ignore);
        }

        /// <summary>Link 2: <c>main</c> names the bundle, and the only hook <c>vsce</c> runs produces it.
        /// A bundle step outside <c>vscode:prepublish</c> never runs in CI.</summary>
        [Fact]
        public void TheManifestNamesTheBundleAndPrepublishTypeChecksAndBundlesIt()
        {
            var manifest = Read("editors/vscode/package.json");

            Assert.Contains("\"main\": \"./dist/extension.js\"", manifest, StringComparison.Ordinal);

            // Each script's whole body: a name alone survives `"check-types": "echo skipped"`, and `||` in the
            // chain skips the bundle whenever the types check.
            Assert.Equal("npm run copy-grammar && npm run check-types && npm run bundle",
                Script(manifest, "vscode:prepublish"));
            Assert.Equal("tsc -p ./", Script(manifest, "check-types"));
            Assert.Equal("node build/bundle.js", Script(manifest, "bundle"));
        }

        private static string Script(string manifest, string name)
        {
            // Exactly one: with a duplicate key npm runs the last, and a pin reading the first pins nothing.
            var scripts = Regex.Matches(manifest, @"""" + Regex.Escape(name) + @"""\s*:\s*""(?<c>[^""]*)""");
            Assert.True(scripts.Count == 1,
                name + " must appear exactly once in editors/vscode/package.json; found " + scripts.Count);
            return scripts[0].Groups["c"].Value;
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

        /// <summary>Link 5: the links above are source text, and a bundler option none of them names can still
        /// leave a require the VSIX cannot satisfy. Every packaged VSIX is loaded and inspected before
        /// upload.</summary>
        [Fact]
        public void EveryPackagedVsixIsLoadedAndInspectedBeforeUpload()
        {
            // Comment lines dropped: one that names `vsce package` is not a packaging step.
            var workflow = Regex.Replace(Read(".github/workflows/lsp.yml"), @"(?m)^[ \t]*#.*$", "");

            var step = Regex.Match(workflow,
                @"(?m)^ {6}- name: Verify VSIX contents[ \t]*\n(?<body>(?: {8}.*\n|[ \t]*\n)*)");
            Assert.True(step.Success, "lsp.yml has no 'Verify VSIX contents' step");
            var body = step.Groups["body"].Value;

            // The command is the step's last line, on its own, so the step's exit status is the command's:
            // commented out, or with `|| true` after it, it gates nothing. So does a step that is skipped or allowed
            // to fail, or a shell that is not the default fail-fast bash.
            Assert.Matches(@"\n +node build/verify-vsix\.js ""\$RUNNER_TEMP/vsix-check/extension""[ \t]*\n\s*$", body);
            Assert.DoesNotMatch(@"(?m)^ {8}(if|continue-on-error|shell):", body);

            // And it checks the bytes that ship: no package after it, before the upload.
            var upload = workflow.IndexOf("path: editors/vscode/*.vsix", StringComparison.Ordinal);
            var package = upload < 0 ? -1 : workflow.LastIndexOf("vsce package", upload, StringComparison.Ordinal);
            Assert.True(package >= 0 && step.Index > package && upload > step.Index,
                "lsp.yml must run build/verify-vsix.js after the last vsce package and before the VSIX upload");
        }
    }
}
