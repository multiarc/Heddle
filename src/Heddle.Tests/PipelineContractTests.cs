using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Heddle.Data;
using Heddle.Precompiled;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 5 (pipeline &amp; configuration) contract pins: the shared key↔path pair (D2), the schema/engine
    /// version constants (D5), the options fingerprint's arity (D7), and the props↔code↔runtime defaults lockstep
    /// (D8). Each of these rules used to exist two-to-four times; these tests are what makes a future divergence a
    /// red build instead of a silent precompiled→dynamic fallback.
    /// </summary>
    public class PipelineContractTests
    {
        // ---- D2: key ↔ path -------------------------------------------------------------------------------

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

        /// <summary>The fix for 05 F3: the shared helper never invents a flattened filename key for a path outside
        /// the root. The generator still registers one (behavior preserved) but must ask for it explicitly, which is
        /// where HED7018 is raised.</summary>
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

        /// <summary>The two case domains (D2): the root <i>prefix</i> test is case-insensitive because it compares
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

        // ---- D4: the .heddle extension rule ---------------------------------------------------------------

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

        // ---- D5: schema/engine versioning -----------------------------------------------------------------

        /// <summary>The invariant that makes F5's all-or-nothing hazard structurally impossible: the version the
        /// generator emits is always inside the window the runtime accepts.</summary>
        [Fact]
        public void SchemaWindowContainsTheEmittedVersion()
        {
            Assert.True(PrecompiledSchema.MinSupportedSchemaVersion <= PrecompiledSchema.CurrentSchemaVersion);
            Assert.True(PrecompiledSchema.CurrentSchemaVersion <= PrecompiledSchema.MaxSupportedSchemaVersion);
            Assert.True(PrecompiledSchema.IsSupported(PrecompiledSchema.CurrentSchemaVersion));
        }

        /// <summary>The shipped constants, pinned. Phase 4 D11 bumped the emitted schema to 3 and widened the
        /// window to {1, 2, 3}: generated dynamic member hops now call <c>PrecompiledRuntime.DynamicMember</c>, an
        /// API an older engine does not carry, so the version is what makes an older engine fall back at
        /// registration instead of faulting mid-render.</summary>
        [Fact]
        public void SchemaConstantsAreUnchangedByTheConsolidation()
        {
            // Phase 3 (OQ4) bumped Max/Current 3→4 for the additive extension prop-layout fingerprint row;
            // phase 1 (D2) bumped them 4→5 for the per-carrier BindDefinition overload generated code now calls.
            Assert.Equal(1, PrecompiledSchema.MinSupportedSchemaVersion);
            Assert.Equal(5, PrecompiledSchema.MaxSupportedSchemaVersion);
            Assert.Equal(5, PrecompiledSchema.CurrentSchemaVersion);
            Assert.Equal(4, PrecompiledSchema.PropLayoutFingerprintSchemaVersion);
            Assert.Equal(5, PrecompiledSchema.PerCarrierLocalsSchemaVersion);
            Assert.False(PrecompiledSchema.IsSupported(0));
            Assert.False(PrecompiledSchema.IsSupported(6));
            // The row is additive: every schema in the window is still accepted, so no manifest is forced to
            // re-precompile by its arrival.
            Assert.True(PrecompiledSchema.IsSupported(1));
            Assert.True(PrecompiledSchema.IsSupported(3));
            Assert.True(PrecompiledSchema.IsSupported(5));
        }

        /// <summary>The D11 gate itself: the generator only emits <c>DynamicMember</c> routing at or above the
        /// schema that introduced it, so "routing emitted below its gate" cannot be built.</summary>
        [Fact]
        public void DynamicMemberRoutingIsGatedOnItsSchema()
        {
            Assert.Equal(3, PrecompiledSchema.DynamicMemberRoutingSchemaVersion);
            Assert.True(PrecompiledSchema.EmitsDynamicMemberRouting);
            Assert.True(PrecompiledSchema.CurrentSchemaVersion >= PrecompiledSchema.DynamicMemberRoutingSchemaVersion);
            Assert.True(PrecompiledSchema.EmitsPerCarrierLocals);
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

        // ---- D7: fingerprint arity ------------------------------------------------------------------------

        /// <summary>The second guard behind D7 (the first is that the generator constructs a real
        /// <see cref="PrecompiledOptionsFingerprint"/>, so a new field breaks its build): the emitter's formatter
        /// writes exactly as many arguments as the struct takes. A fourth identity-bearing option can never be
        /// silently omitted from the emitted fingerprint.</summary>
        [Fact]
        public void FingerprintFormatterArityMatchesTheConstructorArity()
        {
            var ctor = Assert.Single(typeof(PrecompiledOptionsFingerprint).GetConstructors());
            var arity = ctor.GetParameters().Length;
            Assert.Equal(3, arity);

            var emitted = FormatLikeTheEmitter(new PrecompiledOptionsFingerprint(
                OutputProfile.Html, ExpressionMode.Native, true));
            Assert.Equal(arity, emitted.Split(',').Length);

            // Every member of the struct is carried by the emitted expression.
            foreach (var property in typeof(PrecompiledOptionsFingerprint).GetProperties())
                Assert.Contains(property.GetValue(new PrecompiledOptionsFingerprint(
                    OutputProfile.Html, ExpressionMode.Native, true)).ToString().ToLowerInvariant(),
                    emitted.ToLowerInvariant());
        }

        private static string FormatLikeTheEmitter(PrecompiledOptionsFingerprint fingerprint) =>
            $"global::Heddle.Data.OutputProfile.{fingerprint.Profile}," +
            $"global::Heddle.Data.ExpressionMode.{fingerprint.ExpressionMode}," +
            $"trimDirectiveLines: {(fingerprint.TrimDirectiveLines ? "true" : "false")}";

        // ---- D8: the option names + defaults table --------------------------------------------------------

        /// <summary>The props↔code↔runtime lockstep gate (D8). <c>Heddle.Generator.props</c> is a second physical
        /// statement of five scalar defaults — unavoidably, because <c>HeddleTemplateRoot</c>'s default is only
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
            Assert.Equal(HeddleBuildOptions.DefaultEmitUtf8Pieces ? "true" : "false",
                props[HeddleBuildOptions.EmitUtf8PiecesProperty]);

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
        public void DefaultsAreTheShippedTwoPointZeroValues()
        {
            Assert.Equal(OutputProfile.Html, HeddleBuildOptions.DefaultOutputProfile);
            Assert.Equal(ExpressionMode.Native, HeddleBuildOptions.DefaultExpressionMode);
            Assert.True(HeddleBuildOptions.DefaultTrimDirectiveLines);
            Assert.Equal(100, HeddleBuildOptions.DefaultMaxRecursionCount);
            Assert.False(HeddleBuildOptions.DefaultEmitUtf8Pieces);
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

        private static Dictionary<string, string> ReadPropsDefaults()
        {
            var path = FindRepoFile(Path.Combine("src", "Heddle.Generator", "build", "Heddle.Generator.props"));
            var xml = File.ReadAllText(path);
            var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(xml, @"<(?<name>Heddle\w+) Condition=""[^""]*"">(?<value>[^<]*)</\k<name>>"))
                defaults[match.Groups["name"].Value] = match.Groups["value"].Value;

            // Presence is part of the assertion: a structural change that breaks the parse must be a red test.
            Assert.Equal(6, defaults.Count);
            return defaults;
        }

        internal static string FindRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(PipelineContractTests).Assembly.Location));
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
