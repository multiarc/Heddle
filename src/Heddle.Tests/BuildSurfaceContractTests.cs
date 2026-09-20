using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Heddle.Data;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Contract pins for the <c>Heddle.Build</c> surface: the shared key↔path pair, the schema/engine
    /// version constants, the options fingerprint's arity, the props↔code↔runtime defaults lockstep, and the
    /// item-metadata wiring from <c>Heddle.Build.targets</c> through the <c>HeddleCompile</c> task into the
    /// <c>heddle compile</c> response file. Each of these rules used to exist two-to-four times; these tests
    /// are what makes a future divergence a red build instead of a silent precompiled→dynamic fallback.
    /// </summary>
    public class BuildSurfaceContractTests
    {
        public static IEnumerable<object[]> RoundTripVectors => new[]
        {
            new object[] { "index.heddle" },
            new object[] { "views/home/index.heddle" },
            new object[] { "Views/Home/Index.heddle" },
            new object[] { "a/b/c/deep.heddle" },
        };

        /// <summary>The build side derives key = f(path, root); the runtime staleness check reconstitutes
        /// path = f⁻¹(key, root). They are inverses by construction now that both live on <see cref="TemplateKey"/>.</summary>
        [Theory]
        [MemberData(nameof(RoundTripVectors))]
        public void ToPathAndTryMakeRelativeRoundTripForInRootVectors(string key)
        {
            var root = Path.Combine(Path.GetTempPath(), "heddle-root");
            var path = TemplateKey.ToPath(key, root);

            Assert.True(TemplateKey.TryMakeRelative(path, root, out var derived));
            Assert.Equal(key, derived);
        }

        /// <summary>The shared helper never invents a flattened filename key for a path outside
        /// the root. The build registers one only when asked explicitly, which is where HED7018 is raised.</summary>
        [Fact]
        public void TryMakeRelativeRefusesOutOfRootPathsInsteadOfFlatteningThem()
        {
            var root = Path.Combine(Path.GetTempPath(), "heddle-root");
            var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "shared", "banner.heddle");

            Assert.False(TemplateKey.TryMakeRelative(outside, root, out var key));
            Assert.Null(key);
        }

        [Fact]
        public void TryMakeRelativeRefusesAnEmptyRootRatherThanTreatingEverythingAsOutOfRoot()
        {
            Assert.False(TemplateKey.TryMakeRelative("/anywhere/x.heddle", string.Empty, out _));
            Assert.False(TemplateKey.TryMakeRelative("/anywhere/x.heddle", null, out _));
        }

        /// <summary>The two case domains: the root <i>prefix</i> test is case-insensitive because it compares
        /// filesystem paths, while the key it yields preserves case exactly.</summary>
        [Theory]
        [InlineData("/repo/Templates", "/repo/templates/Views/Home.heddle", "Views/Home.heddle")]
        [InlineData("/repo/templates", "/repo/Templates/Views/Home.heddle", "Views/Home.heddle")]
        [InlineData("/repo/templates/", "/repo/templates/Views/Home.heddle", "Views/Home.heddle")]
        [InlineData("/repo/templates", "/repo/templates\\Views\\Home.heddle", "Views/Home.heddle")]
        public void RootPrefixIsCaseInsensitiveWhileTheKeyPreservesCase(string root, string path, string expected)
        {
            Assert.True(TemplateKey.TryMakeRelative(path, root, out var key));
            Assert.Equal(expected, key);
        }

        [Theory]
        [InlineData("a.heddle", true)]
        [InlineData("a.HEDDLE", true)]
        [InlineData("a.txt", false)]
        [InlineData("heddle", false)]
        [InlineData(null, false)]
        public void ExtensionMatchingIsCaseInsensitive(string value, bool expected)
        {
            Assert.Equal(expected, TemplateKey.HasTemplateExtension(value));
        }

        [Theory]
        [InlineData("views/home.heddle", "views/home")]
        [InlineData("views/home.HEDDLE", "views/home")]
        [InlineData("views/home.txt", "views/home.txt")]
        public void StripTemplateExtensionYieldsTheTemplateName(string key, string expected)
        {
            Assert.Equal(expected, TemplateKey.StripTemplateExtension(key));
        }

        [Fact]
        public void NormalizeAppendsTheSharedExtensionConst()
        {
            Assert.Equal("views/home" + TemplateKey.TemplateExtension, TemplateKey.Normalize("views/home"));
        }

        /// <summary>The default glob is the XML twin of <see cref="TemplateKey.TemplateExtension"/>, which is
        /// normative: a template the glob does not collect has no key under either tier's rule.</summary>
        [Fact]
        public void DefaultGlobMatchesTheSharedExtensionConst()
        {
            var targetsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "build", "Heddle.Build.targets")));
            Assert.Contains("**\\*" + TemplateKey.TemplateExtension, targetsXml, StringComparison.Ordinal);
        }

        /// <summary>The invariant that makes the all-or-nothing hazard structurally impossible: the version the
        /// build host emits is always inside the window the runtime accepts.</summary>
        [Fact]
        public void SchemaWindowContainsTheEmittedVersion()
        {
            Assert.True(PrecompiledSchema.MinSupportedSchemaVersion <= PrecompiledSchema.CurrentSchemaVersion);
            Assert.True(PrecompiledSchema.CurrentSchemaVersion <= PrecompiledSchema.MaxSupportedSchemaVersion);
            Assert.True(PrecompiledSchema.IsSupported(PrecompiledSchema.CurrentSchemaVersion));
        }

        /// <summary>The schema window restarted at the compiled form: exactly one readable shape, so
        /// the window is a point. Schemas 1–2 are released 2.x shapes the engine refuses; 3 is an unreleased
        /// number no artifact may carry.</summary>
        [Fact]
        public void SchemaConstantsPinTheCompiledFormWindow()
        {
            Assert.Equal(3, PrecompiledSchema.CompiledFormSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.MinSupportedSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.MaxSupportedSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.CurrentSchemaVersion);

            Assert.False(PrecompiledSchema.IsSupported(0));
            Assert.False(PrecompiledSchema.IsSupported(1));
            Assert.False(PrecompiledSchema.IsSupported(2));
            Assert.False(PrecompiledSchema.IsSupported(3));
            Assert.True(PrecompiledSchema.IsSupported(4));
            Assert.False(PrecompiledSchema.IsSupported(5));
        }

        [Theory]
        [InlineData("2.0.0.0", "2.0.0")]
        [InlineData("2.1.7.9", "2.1.7")]
        [InlineData("10.20.30", "10.20.30")]
        public void EngineVersionFormatIsMajorMinorBuild(string version, string expected)
        {
            Assert.Equal(expected, PrecompiledSchema.FormatEngineVersion(Version.Parse(version)));
        }

        [Theory]
        [InlineData("2.0.0", "2.0.0", true)]
        [InlineData("2.0.0", "2.1.0", true)]   // manifest older than the running engine: fine
        [InlineData("2.1.0", "2.0.0", false)]  // manifest newer than the running engine: refused
        [InlineData("1.9.0", "2.0.0", false)]  // different major: refused
        public void EngineCompatibilityIsSameMajorAndNotNewer(string manifest, string runtime, bool expected)
        {
            Assert.Equal(expected,
                PrecompiledSchema.IsEngineCompatible(Version.Parse(manifest), Version.Parse(runtime)));
        }

        /// <summary>The guard behind fingerprint arity (the first is that the build constructs a real
        /// <see cref="PrecompiledOptionsFingerprint"/>, so a new field breaks its build): the host's formatter
        /// writes exactly as many arguments as the struct takes. A fourth identity-bearing option can never be
        /// silently omitted from the recorded fingerprint.</summary>
        [Fact]
        public void FingerprintFormatterArityMatchesTheConstructorArity()
        {
            var ctor = Assert.Single(typeof(PrecompiledOptionsFingerprint).GetConstructors());
            var arity = ctor.GetParameters().Length;
            Assert.Equal(3, arity);

            var emitted = FormatLikeTheHost(new PrecompiledOptionsFingerprint(
                OutputProfile.Html, ExpressionMode.Native, true));
            Assert.Equal(arity, emitted.Split(',').Length);

            // Every member of the struct is carried by the emitted expression.
            foreach (var property in typeof(PrecompiledOptionsFingerprint).GetProperties())
                Assert.Contains(property.GetValue(new PrecompiledOptionsFingerprint(
                    OutputProfile.Html, ExpressionMode.Native, true)).ToString().ToLowerInvariant(),
                    emitted.ToLowerInvariant());
        }

        private static string FormatLikeTheHost(PrecompiledOptionsFingerprint fingerprint) =>
            $"global::Heddle.Data.OutputProfile.{fingerprint.Profile}," +
            $"global::Heddle.Data.ExpressionMode.{fingerprint.ExpressionMode}," +
            $"trimDirectiveLines: {(fingerprint.TrimDirectiveLines ? "true" : "false")}";

        /// <summary>The props↔code↔runtime lockstep gate. <c>Heddle.Build.props</c> is a second physical
        /// statement of the scalar defaults — unavoidably, because <c>HeddleTemplateRoot</c>'s default is only
        /// expressible in MSBuild — so its literals are asserted against <see cref="HeddleBuildOptions"/> and
        /// against a default-constructed <see cref="TemplateOptions"/>. A one-sided default change flips every
        /// template to <c>OptionsMismatch</c>; this test is the reason that cannot happen quietly.</summary>
        [Fact]
        public void PropsDefaultsMatchTheSharedTableAndTheRuntimeOptions()
        {
            var props = ReadPropsDefaults();

            Assert.Equal(HeddleBuildOptions.DefaultOutputProfile.ToString(),
                props[HeddleBuildOptions.OutputProfileProperty]);
            Assert.Equal(HeddleBuildOptions.DefaultExpressionMode.ToString(),
                props[HeddleBuildOptions.ExpressionModeProperty]);
            Assert.Equal(HeddleBuildOptions.DefaultTrimDirectiveLines ? "true" : "false",
                props[HeddleBuildOptions.TrimDirectiveLinesProperty]);
            Assert.Equal(HeddleBuildOptions.DefaultMaxRecursionCount.ToString(),
                props[HeddleBuildOptions.MaxRecursionCountProperty]);

            // HeddleTemplateRoot's default is MSBuild-only; assert it is still stated, not its C# twin.
            Assert.Equal("$(MSBuildProjectDirectory)", props[HeddleBuildOptions.TemplateRootProperty]);

            var options = new TemplateOptions();
            Assert.Equal(HeddleBuildOptions.DefaultOutputProfile, options.OutputProfile);
            Assert.Equal(HeddleBuildOptions.DefaultExpressionMode, options.ExpressionMode);
            Assert.Equal(HeddleBuildOptions.DefaultTrimDirectiveLines, options.TrimDirectiveLines);
            Assert.Equal(HeddleBuildOptions.DefaultMaxRecursionCount, options.MaxRecursionCount);
        }

        /// <summary>The values themselves, spelled out — so a change to <see cref="HeddleBuildOptions"/> that also
        /// updated the props file cannot slip through the lockstep test above. Defaults are window-governed.</summary>
        [Fact]
        public void DefaultsAreTheShippedValues()
        {
            Assert.Equal(OutputProfile.Html, HeddleBuildOptions.DefaultOutputProfile);
            Assert.Equal(ExpressionMode.Native, HeddleBuildOptions.DefaultExpressionMode);
            Assert.True(HeddleBuildOptions.DefaultTrimDirectiveLines);
            Assert.Equal(100, HeddleBuildOptions.DefaultMaxRecursionCount);
        }

        /// <summary>Both <see cref="TemplateOptions"/> constructors used to state the defaults independently; the
        /// parameterless one now chains, so they cannot drift from each other either.</summary>
        [Fact]
        public void BothTemplateOptionsConstructorsProduceTheSameDefaults()
        {
            var bare = new TemplateOptions();
            var named = new TemplateOptions("x");
            Assert.Equal(bare.OutputProfile, named.OutputProfile);
            Assert.Equal(bare.ExpressionMode, named.ExpressionMode);
            Assert.Equal(bare.TrimDirectiveLines, named.TrimDirectiveLines);
            Assert.Equal(bare.MaxRecursionCount, named.MaxRecursionCount);
            Assert.Equal(string.Empty, bare.TemplateName);
            Assert.Equal("x", named.TemplateName);
        }

        /// <summary>The HED7009 "expected" text is built from the linked enums, not a hand-copied allow-list — and
        /// must still read exactly as it shipped.</summary>
        [Fact]
        public void ExpectedValueTextsAreReproducedVerbatim()
        {
            Assert.Equal("Text|Html", HeddleBuildOptions.ExpectedValues<OutputProfile>());
            Assert.Equal("MemberPathsOnly|Native|FullCSharp", HeddleBuildOptions.ExpectedValues<ExpressionMode>());
            Assert.Equal("true|false", HeddleBuildOptions.ExpectedBool);
            Assert.Equal("a positive integer", HeddleBuildOptions.ExpectedPositiveInt);
        }

        [Theory]
        [InlineData("Html", true, OutputProfile.Html)]
        [InlineData("html", true, OutputProfile.Html)]
        [InlineData("TEXT", true, OutputProfile.Text)]
        [InlineData("", true, OutputProfile.Html)]
        [InlineData(null, true, OutputProfile.Html)]
        [InlineData("WebForms", false, OutputProfile.Html)]
        [InlineData("1", false, OutputProfile.Html)]   // a numeric spelling is not a member name
        public void EnumOptionParsingMatchesTheShippedAllowList(string raw, bool ok, OutputProfile expected)
        {
            Assert.Equal(ok, HeddleBuildOptions.TryReadEnum(raw, OutputProfile.Html, out var value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData("0", false, 100)]
        [InlineData("-1", false, 100)]
        [InlineData("nope", false, 100)]
        [InlineData("7", true, 7)]
        [InlineData("", true, 100)]
        public void PositiveIntParsingRejectsNonPositiveValues(string raw, bool ok, int expected)
        {
            Assert.Equal(ok, HeddleBuildOptions.TryReadPositiveInt(raw, 100, out var value));
            Assert.Equal(expected, value);
        }

        /// <summary>
        /// <para>The structural gate. The per-item metadata names live in three physical places — the
        /// <c>HeddleCompile</c> task's parameter docs declare them, the response-file fields carry them, and the
        /// targets split on <c>Precompile</c> — and a name absent from any one of the three is silently inert.
        /// That is what happened to 2.x's <c>Name</c>: declared in props since 2.0, never read, so a sample's
        /// <c>Name="BuildReport"</c> did nothing for a whole release.</para>
        /// <para>Asserted as <b>set equality</b>, not as a count: a count is made green by editing one digit,
        /// whereas set equality can only be made green by naming the metadatum whose wiring changed.</para>
        /// </summary>
        [Fact]
        public void EveryDeclaredItemMetadataFlowsIntoTheResponseFile()
        {
            var taskCs = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "Tasks", "HeddleCompile.cs")));
            var responseCs = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Tool", "Compile", "ResponseFile.cs")));
            var targetsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "build", "Heddle.Build.targets")));

            // The task declares the metadata it serializes: Key, Name, ModelType, OutputProfile on compile
            // items; Key, Name on import-only items.
            Assert.Contains("Metadata: Key, Name, ModelType, OutputProfile", taskCs, StringComparison.Ordinal);
            Assert.Contains("Metadata: Key, Name.", taskCs, StringComparison.Ordinal);

            // The response file carries exactly those fields, in task order: path|key|name|modelType|outputProfile
            // plus path|key|name for import-only items.
            Assert.Contains("path|key|name|modelType|outputProfile", responseCs, StringComparison.Ordinal);
            Assert.Contains("path|key|name</c>", responseCs, StringComparison.Ordinal);

            // The targets split compile from import-only on Precompile and restate nothing: outside a target a
            // cross-item %(HeddleTemplate.X) reference evaluates to the empty string and would delete what the
            // declaration copied.
            Assert.Contains("%(HeddleTemplate.Precompile)", targetsXml, StringComparison.Ordinal);
            var withoutComments = Regex.Replace(targetsXml, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
            var restated = Regex.Matches(withoutComments, @"%\(HeddleTemplate\.(?<name>\w+)\)")
                .Cast<Match>()
                .Select(m => m.Groups["name"].Value)
                .Where(name => !string.Equals(name, "Precompile", StringComparison.Ordinal))
                .ToList();
            Assert.True(restated.Count == 0,
                "Heddle.Build.targets restates item metadata the @(HeddleTemplate) transform already carries; " +
                "outside a target that evaluates to \"\" and deletes the value: " + string.Join(", ", restated));
        }

        /// <summary>The out-of-process half of the surface: the host reads plain MSBuild items and properties,
        /// so no <c>CompilerVisible</c> declaration may appear — one would hand the compiler a path to read,
        /// which is exactly the determinism the response-file route preserves. For the same reason the build
        /// declares no <c>AdditionalFiles</c> item of its own and lets no Heddle item flow into one.
        /// <para>What is allowed, and only this: the throwaway intermediate model compile is handed the
        /// <b>project's own</b> <c>@(AdditionalFiles)</c>, unchanged, so the project's source generators see
        /// there what they will see in the real compile. That is one <c>Csc</c> attribute whose whole value
        /// is <c>@(AdditionalFiles)</c>; the real compile is never invoked or altered by these targets.</para></summary>
        [Fact]
        public void BuildSurfaceDeclaresNoCompilerVisibleWiring()
        {
            var propsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "build", "Heddle.Build.props")));
            var targetsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "build", "Heddle.Build.targets")));
            var props = Regex.Replace(propsXml, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
            var targets = Regex.Replace(targetsXml, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);

            Assert.DoesNotContain("CompilerVisible", props, StringComparison.Ordinal);
            Assert.DoesNotContain("CompilerVisible", targets, StringComparison.Ordinal);
            Assert.DoesNotContain("AdditionalFiles", props, StringComparison.Ordinal);

            // No declaration: neither an item nor a property named AdditionalFiles.
            Assert.DoesNotContain("<AdditionalFiles", targets, StringComparison.Ordinal);

            // Every remaining mention is a read of the project's own item list, never anything of Heddle's...
            var mentions = Regex.Matches(targets, "AdditionalFiles");
            var reads = Regex.Matches(targets, @"@\(AdditionalFiles\)");
            // (the forwarding attribute spells the word once more, as its own name)
            Assert.Equal(mentions.Count, reads.Count + 1);

            // ...and the only consumers are the intermediate pass: its Csc attribute, passed through whole,
            // and the digest that decides whether that pass has to run.
            var forwarded = Regex.Matches(targets, @"\bAdditionalFiles=""([^""]*)""");
            Assert.Single(forwarded);
            Assert.Equal("@(AdditionalFiles)", forwarded[0].Groups[1].Value);
            var cscElements = Regex.Matches(targets, @"<Csc\b[^>]*>", RegexOptions.Singleline);
            Assert.Single(cscElements);
            Assert.Contains("AdditionalFiles=\"@(AdditionalFiles)\"", cscElements[0].Value, StringComparison.Ordinal);
            Assert.Contains("OutputAssembly=\"@(_HeddleIntermediateModel)\"", cscElements[0].Value,
                StringComparison.Ordinal);
            Assert.Equal(2, reads.Count);
            Assert.Matches(@"DigestFiles=""[^""]*@\(AdditionalFiles\)[^""]*""", targets);

            // The real compile is never invoked or redefined from here: the targets only hook before it.
            Assert.DoesNotContain("<CallTarget", targets, StringComparison.Ordinal);
            Assert.DoesNotContain("<MSBuild ", targets, StringComparison.Ordinal);
            Assert.DoesNotContain("Name=\"CoreCompile\"", targets, StringComparison.Ordinal);
        }

        /// <summary>The assembly escape hatch, gated the way the item metadata above is. Every item the targets
        /// declare is appended to <c>@(ReferencePath)</c> — that, and only that, is how the assembly reaches the
        /// host's bind-over set. Neither name gains a property default, so absent items are byte-identical output.
        /// Set equality, not a count, for the reason the metadata gate gives.</summary>
        [Fact]
        public void EveryDeclaredAssemblyItemReachesTheCompilerThroughReferencePath()
        {
            var targetsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "build", "Heddle.Build.targets")));
            var withoutComments = Regex.Replace(targetsXml, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);

            var appended = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(withoutComments,
                @"<ReferencePath\s+Include=""@\((?<name>\w+)\)""\s*/>"))
                appended.Add(m.Groups["name"].Value);

            Assert.Equal(new[] { "HeddleExtensionAssembly", "HeddleModelAssembly" }, appended.ToArray());
        }

        /// <summary>Retired properties are warnings, not defaults and not task inputs: a default would resurrect
        /// the option, a task parameter would rewire it. They may appear in <c>Heddle.Build.targets</c> only as
        /// <c>HED7037</c> warning conditions.</summary>
        [Fact]
        public void RetiredPropertiesHaveNoDefaultAndReachNoTaskParameter()
        {
            var propsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "build", "Heddle.Build.props")));
            var targetsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "build", "Heddle.Build.targets")));
            var taskCs = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Build", "Tasks", "HeddleCompile.cs")));

            foreach (var name in new[]
            {
                "HeddleObserveEngine", "HeddleNodeFallback", "HeddleEmitUtf8Pieces",
                "HeddleObserveIntermediatePath", "HeddleObserveImplementationPath"
            })
            {
                Assert.DoesNotContain("<" + name + " ", propsXml, StringComparison.Ordinal);
                Assert.DoesNotContain(name + "=\"$(" + name + ")", targetsXml, StringComparison.Ordinal);
                Assert.DoesNotContain(name, taskCs, StringComparison.Ordinal);
            }
        }

        private static Dictionary<string, string> ReadPropsDefaults()
        {
            var path = FindRepoFile(Path.Combine("src", "Heddle.Build", "build", "Heddle.Build.props"));
            var xml = File.ReadAllText(path);
            var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(xml, @"<(?<name>Heddle\w+) Condition=""[^""]*"">(?<value>[^<]*)</\k<name>>"))
                defaults[match.Groups["name"].Value] = match.Groups["value"].Value;

            // Presence is part of the assertion: a structural change that breaks the parse must be a red test.
            Assert.Equal(5, defaults.Count);
            return defaults;
        }

        internal static string FindRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(BuildSurfaceContractTests).Assembly.Location));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException($"Could not locate '{relativePath}' above the test assembly.");
        }
    }
}
