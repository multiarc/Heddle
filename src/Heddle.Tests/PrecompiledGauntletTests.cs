using System;
using System.IO;
using System.Reflection;
using Heddle.Data;
using Heddle.Extensions;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 7 D7/D8/D9/D21 per-request validation gauntlet: the ordered checks (marker → options → extensions →
    /// functions → staleness) with the pinned reason and detail strings.
    /// </summary>
    public class PrecompiledGauntletTests
    {
        private sealed class FakeStrategy : IProcessStrategy
        {
            public string Execute(in Scope scope) => string.Empty;
            public void Render(in Scope scope) { }
        }

        private static readonly IProcessStrategy Strategy = new FakeStrategy();

        private static string IfBindingType =>
            typeof(IfExtension).FullName + ", " + typeof(IfExtension).Assembly.GetName().Name;

        private static PrecompiledTemplateInfo Entry(
            PrecompiledOptionsFingerprint fingerprint = default,
            PrecompiledExtensionBinding[] extensions = null,
            PrecompiledFunctionBinding[] functions = null,
            PrecompiledImport[] imports = null,
            string contentHash = "0",
            IProcessStrategy strategy = null,
            string key = "views/x.heddle")
        {
            return new PrecompiledTemplateInfo(key, typeof(object), typeof(object), false, contentHash,
                imports, fingerprint, extensions, functions, PrecompiledCapabilities.StringOutput,
                strategy ?? Strategy);
        }

        private static PrecompiledFallbackEvent? Run(PrecompiledTemplateInfo entry, TemplateOptions options)
            => PrecompiledGauntlet.Validate(entry, options, null);

        private static readonly PrecompiledOptionsFingerprint TextNative =
            new PrecompiledOptionsFingerprint(OutputProfile.Text, ExpressionMode.Native, false);

        [Fact]
        public void AllPassReturnsNull()
        {
            var entry = Entry(TextNative,
                new[] { new PrecompiledExtensionBinding("if", IfBindingType) });
            Assert.Null(Run(entry, new TemplateOptions()));
        }

        [Fact]
        public void MarkerEntryShortCircuits()
        {
            var entry = new PrecompiledTemplateInfo("views/x.heddle", null, null, false, "0", null, TextNative,
                null, new[] { new PrecompiledFunctionBinding("titlecase", null, 0) },
                PrecompiledCapabilities.None, strategy: null);
            var evt = Run(entry, new TemplateOptions());
            Assert.NotNull(evt);
            Assert.Equal(PrecompiledFallbackReason.UnsupportedFunction, evt.Value.Reason);
            Assert.Equal("Function 'titlecase': not precompiled (no default or exported binding; build warning HED7014)",
                evt.Value.Detail);
            Assert.Equal("HED7101", evt.Value.DiagnosticId);
        }

        [Fact]
        public void OptionsProfileMismatch()
        {
            var entry = Entry(TextNative);
            var evt = Run(entry, new TemplateOptions { OutputProfile = OutputProfile.Html });
            Assert.NotNull(evt);
            Assert.Equal(PrecompiledFallbackReason.OptionsMismatch, evt.Value.Reason);
            Assert.Equal("OutputProfile: manifest=Text request=Html", evt.Value.Detail);
        }

        [Fact]
        public void OptionsExpressionModeMismatch()
        {
            var entry = Entry(TextNative);
            var evt = Run(entry, new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp });
            Assert.Equal("ExpressionMode: manifest=Native request=FullCSharp", evt.Value.Detail);
        }

        [Fact]
        public void OptionsTrimMismatch()
        {
            var entry = Entry(TextNative);
            var evt = Run(entry, new TemplateOptions { TrimDirectiveLines = true });
            Assert.Equal("TrimDirectiveLines: manifest=false request=true", evt.Value.Detail);
        }

        [Fact]
        public void ExtensionUnresolved()
        {
            var entry = Entry(TextNative,
                new[] { new PrecompiledExtensionBinding("no-such-ext-xyz", "Foo.Bar, Baz") });
            var evt = Run(entry, new TemplateOptions());
            Assert.Equal(PrecompiledFallbackReason.ExtensionBindingMismatch, evt.Value.Reason);
            Assert.Equal("Extension 'no-such-ext-xyz': manifest=Foo.Bar, Baz live=<unresolved>", evt.Value.Detail);
        }

        [Fact]
        public void ExtensionTypeDiverges()
        {
            var entry = Entry(TextNative,
                new[] { new PrecompiledExtensionBinding("if", "Wrong.Type, Heddle") });
            var evt = Run(entry, new TemplateOptions());
            Assert.Equal(PrecompiledFallbackReason.ExtensionBindingMismatch, evt.Value.Reason);
            Assert.Equal($"Extension 'if': manifest=Wrong.Type, Heddle live={IfBindingType}", evt.Value.Detail);
        }

        [Fact]
        public void DefaultFunctionsPassUnderNullRegistry()
        {
            var entry = Entry(TextNative, functions: new[]
            {
                new PrecompiledFunctionBinding("upper", DefaultFunctionTable.ShimTargetTypeName, 1)
            });
            Assert.Null(Run(entry, new TemplateOptions())); // options.Functions == null, all built-in
        }

        [Fact]
        public void ExportBoundRowUnderNullRegistryIsMissing()
        {
            var entry = Entry(TextNative, functions: new[]
            {
                new PrecompiledFunctionBinding("titlecase", "Acme.Web.TemplateFunctions, Acme.Web", 1)
            });
            var evt = Run(entry, new TemplateOptions());
            Assert.Equal(PrecompiledFallbackReason.FunctionBindingMismatch, evt.Value.Reason);
            Assert.Equal("Function 'titlecase': manifest=Acme.Web.TemplateFunctions, Acme.Web live=<missing>",
                evt.Value.Detail);
        }

        [Fact]
        public void DelegateRegistrationUnderBoundNameDiverges()
        {
            var registry = new FunctionRegistry();
            registry.Register("titlecase", (Func<string, string>)(s => s));
            var entry = Entry(TextNative, functions: new[]
            {
                new PrecompiledFunctionBinding("titlecase", "Acme.Web.TemplateFunctions, Acme.Web", 1)
            });
            var evt = Run(entry, new TemplateOptions { Functions = registry });
            Assert.Equal(PrecompiledFallbackReason.FunctionBindingMismatch, evt.Value.Reason);
            Assert.Equal("Function 'titlecase': manifest=Acme.Web.TemplateFunctions, Acme.Web live=<delegate>",
                evt.Value.Detail);
        }

        [Fact]
        public void StaleContentHashMismatch()
        {
            var root = Path.Combine(Path.GetTempPath(), "heddle-stale-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "x.heddle"), "hello");
                var entry = Entry(TextNative, key: "x.heddle", contentHash: "deadbeef");
                var evt = Run(entry, new TemplateOptions { RootPath = root, EnableFileChangeCheck = true });
                Assert.Equal(PrecompiledFallbackReason.StaleContent, evt.Value.Reason);
                Assert.Equal("Content: 'x.heddle' hash mismatch", evt.Value.Detail);
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        [Fact]
        public void StaleContentMissingFile()
        {
            var root = Path.Combine(Path.GetTempPath(), "heddle-stale-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var entry = Entry(TextNative, key: "gone.heddle", contentHash: "deadbeef");
                var evt = Run(entry, new TemplateOptions { RootPath = root, EnableFileChangeCheck = true });
                Assert.Equal(PrecompiledFallbackReason.StaleContent, evt.Value.Reason);
                Assert.Equal("Content: 'gone.heddle' missing", evt.Value.Detail);
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        [Fact]
        public void FreshContentPasses()
        {
            var root = Path.Combine(Path.GetTempPath(), "heddle-stale-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var file = Path.Combine(root, "x.heddle");
                File.WriteAllText(file, "hello world");
                var hash = PrecompiledGauntlet.HashFile(file);
                var entry = Entry(TextNative, key: "x.heddle", contentHash: hash);
                Assert.Null(Run(entry, new TemplateOptions { RootPath = root, EnableFileChangeCheck = true }));
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        [Fact]
        public void StalenessSkippedWhenFileChangeCheckOff()
        {
            // A wrong hash must NOT fail when EnableFileChangeCheck is off.
            var entry = Entry(TextNative, key: "nowhere/x.heddle", contentHash: "deadbeef");
            Assert.Null(Run(entry, new TemplateOptions { RootPath = "/nonexistent" }));
        }
    }
}
