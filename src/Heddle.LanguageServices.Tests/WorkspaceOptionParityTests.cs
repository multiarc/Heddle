using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.LanguageServices;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// <para>Workspace option completeness gate: the editor follows the same configuration surface the runtime
    /// permits. Every public <see cref="TemplateOptions"/> property is either wired to a <c>.heddle-lsp.json</c> key
    /// or named on the exclusion list below <i>with its reason</i>. A future runtime option must be wired or
    /// explicitly excluded — never forgotten.</para>
    /// <para>The keys' names and default values come from the shared <see cref="HeddleBuildOptions"/> table, so the
    /// editor and engine always agree on what the default is.</para>
    /// </summary>
    public class WorkspaceOptionParityTests
    {
        /// <summary>TemplateOptions property → the <c>.heddle-lsp.json</c> key that configures it.</summary>
        public static readonly IReadOnlyDictionary<string, string> Wired = new Dictionary<string, string>
        {
            [nameof(TemplateOptions.RootPath)] = "rootPath",
            [nameof(TemplateOptions.OutputProfile)] = "outputProfile",
            [nameof(TemplateOptions.ExpressionMode)] = "expressionMode",
            [nameof(TemplateOptions.FileNamePostfix)] = "fileNamePostfix",
            [nameof(TemplateOptions.TrimDirectiveLines)] = "trimDirectiveLines",
            [nameof(TemplateOptions.MaxRecursionCount)] = "maxRecursionCount",
        };

        /// <summary>Exclusions documented with reasons why analysis cannot use them.</summary>
        public static readonly IReadOnlyDictionary<string, string> Excluded = new Dictionary<string, string>
        {
            [nameof(TemplateOptions.TemplateName)] =
                "per-document identity — the analyzer derives it from the file being analyzed, so a workspace-wide key would be meaningless.",
            [nameof(TemplateOptions.FullPath)] =
                "computed from RootPath/TemplateName/FileNamePostfix; not an input.",
            [nameof(TemplateOptions.Functions)] =
                "object-valued (a FunctionRegistry) with no literal JSON form — represented by the LSP-specific 'assemblies' key, which the one-shot export scan reads.",
            [nameof(TemplateOptions.Data)] =
                "render input (the model instance); analysis compiles, never renders.",
            [nameof(TemplateOptions.EnableFileChangeCheck)] =
                "render-cache invalidation for the runtime's file watcher; the editor owns document versioning itself.",
            [nameof(TemplateOptions.PrecompiledMismatchPolicy)] =
                "selects run-tier fallback vs throw when a precompiled entry fails the gauntlet; the analyzer never consults the precompiled registry.",
            [nameof(TemplateOptions.RenderBudget)] =
                "per-render resource limits, object-valued; no lint depends on them.",
            [nameof(TemplateOptions.ValidateModelType)] =
                "render-time failure handling for wrong-typed data; analysis has no data.",
            [nameof(TemplateOptions.Encoder)] =
                "render-time output encoding, object-valued (a TextEncoder); it changes rendered bytes, never a compile diagnostic.",
            ["AllowCSharp"] =
                "obsolete bridge over ExpressionMode — the key is 'expressionMode'; wiring both would let a config contradict itself.",
            [nameof(TemplateOptions.ProvideLanguageFeatures)] =
                "always true in the LSP — it is the analyzer's operating mode, not a workspace choice.",
        };

        [Fact]
        public void EveryTemplateOptionsPropertyIsWiredOrNamedAsAnExclusion()
        {
            var properties = typeof(TemplateOptions)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToList();

            var unaccounted = properties.Where(p => !Wired.ContainsKey(p) && !Excluded.ContainsKey(p)).ToList();
            Assert.True(unaccounted.Count == 0,
                "TemplateOptions gained " + string.Join(", ", unaccounted) +
                ". Wire it to a .heddle-lsp.json key in WorkspaceConfig, or add it to the exclusion list in this " +
                "test with the reason analysis cannot use it.");

            foreach (var name in Wired.Keys.Concat(Excluded.Keys))
                Assert.Contains(name, properties);

            Assert.Empty(Wired.Keys.Intersect(Excluded.Keys));
        }

        /// <summary>The key-naming rule is data, not convention: a key is the option's own property name,
        /// camelCased.</summary>
        [Fact]
        public void EveryWiredKeyIsTheCamelCasedOptionName()
        {
            foreach (var pair in Wired)
                Assert.Equal(pair.Value, WorkspaceConfig.ConfigKey(pair.Key));
        }

        /// <summary>Reader keys are exactly the wired set plus LSP-specific 'assemblies'.</summary>
        [Fact]
        public void TheReadersKeysAreExactlyTheWiredSetPlusAssemblies()
        {
            var declared = new[]
            {
                WorkspaceConfig.RootPathKey, WorkspaceConfig.OutputProfileKey, WorkspaceConfig.ExpressionModeKey,
                WorkspaceConfig.FileNamePostfixKey, WorkspaceConfig.TrimDirectiveLinesKey,
                WorkspaceConfig.MaxRecursionCountKey
            };

            Assert.Equal(Wired.Values.OrderBy(v => v, StringComparer.Ordinal).ToList(),
                declared.OrderBy(v => v, StringComparer.Ordinal).ToList());
            Assert.DoesNotContain(WorkspaceConfig.AssembliesKey, declared);
        }

        /// <summary>MSBuild property names agree per the camelCase rule, except <c>rootPath</c> which uses the <see cref="TemplateOptions"/> spelling.</summary>
        [Fact]
        public void KeysAgreeWithTheSharedTablesMsBuildPropertyNames()
        {
            void Agree(string msBuildProperty, string key) =>
                Assert.Equal(key, WorkspaceConfig.ConfigKey(msBuildProperty.Substring("Heddle".Length)));

            Agree(HeddleBuildOptions.OutputProfileProperty, Wired[nameof(TemplateOptions.OutputProfile)]);
            Agree(HeddleBuildOptions.ExpressionModeProperty, Wired[nameof(TemplateOptions.ExpressionMode)]);
            Agree(HeddleBuildOptions.TrimDirectiveLinesProperty, Wired[nameof(TemplateOptions.TrimDirectiveLines)]);
            Agree(HeddleBuildOptions.MaxRecursionCountProperty, Wired[nameof(TemplateOptions.MaxRecursionCount)]);

            Assert.Equal("templateRoot",
                WorkspaceConfig.ConfigKey(HeddleBuildOptions.TemplateRootProperty.Substring("Heddle".Length)));
            Assert.Equal("rootPath", Wired[nameof(TemplateOptions.RootPath)]);
        }

        /// <summary>Absent keys yield runtime defaults.</summary>
        [Fact]
        public void AbsentKeysYieldTheRuntimeDefaults()
        {
            var runtime = new TemplateOptions();
            var options = WorkspaceConfig.Read("/ws", "{}");

            Assert.Equal(runtime.OutputProfile, options.OutputProfile);
            Assert.Equal(runtime.ExpressionMode, options.ExpressionMode);
            Assert.Equal(runtime.FileNamePostfix, options.FileNamePostfix);
            Assert.Equal(runtime.TrimDirectiveLines, options.TrimDirectiveLines);
            Assert.Equal(runtime.MaxRecursionCount, options.MaxRecursionCount);
            Assert.Empty(options.ConfigurationMessages);
        }

        /// <summary>Bare options match runtime defaults via shared table.</summary>
        [Fact]
        public void ABareOptionsObjectCarriesTheSameDefaults()
        {
            var runtime = new TemplateOptions();
            var bare = new HeddleLanguageServiceOptions();

            Assert.Equal(runtime.OutputProfile, bare.OutputProfile);
            Assert.Equal(runtime.ExpressionMode, bare.ExpressionMode);
            Assert.Equal(runtime.TrimDirectiveLines, bare.TrimDirectiveLines);
            Assert.Equal(runtime.MaxRecursionCount, bare.MaxRecursionCount);
        }

        /// <summary>Default profile is Html, matching the engine.</summary>
        [Fact]
        public void TheDefaultProfileIsTheEnginesHtml()
        {
            Assert.Equal(OutputProfile.Html, HeddleBuildOptions.DefaultOutputProfile);
            Assert.Equal(OutputProfile.Html, new HeddleLanguageServiceOptions().OutputProfile);
            Assert.Equal(OutputProfile.Html, WorkspaceConfig.Read("/ws", null).OutputProfile);
            Assert.Equal(OutputProfile.Text, WorkspaceConfig.Read("/ws", "{\"outputProfile\":\"text\"}").OutputProfile);
        }
    }
}
