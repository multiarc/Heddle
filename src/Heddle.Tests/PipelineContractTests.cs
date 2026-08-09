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
    /// Contract pins for pipeline configuration: the shared key↔path pair, the schema/engine
    /// version constants, the options fingerprint's arity, and the props↔code↔runtime defaults lockstep.
    /// Each of these rules used to exist two-to-four times; these tests are what makes a future divergence a
    /// red build instead of a silent precompiled→dynamic fallback.
    /// </summary>
    public class PipelineContractTests
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

        /// <summary>The invariant that makes the all-or-nothing hazard structurally impossible: the version the
        /// generator emits is always inside the window the runtime accepts.</summary>
        [Fact]
        public void SchemaWindowContainsTheEmittedVersion()
        {
            Assert.True(PrecompiledSchema.MinSupportedSchemaVersion <= PrecompiledSchema.CurrentSchemaVersion);
            Assert.True(PrecompiledSchema.CurrentSchemaVersion <= PrecompiledSchema.MaxSupportedSchemaVersion);
            Assert.True(PrecompiledSchema.IsSupported(PrecompiledSchema.CurrentSchemaVersion));
        }

        /// <summary>
        /// <para>The constants, pinned — <b>and the released line is the anchor</b>, not the working one. Verified
        /// against the <c>v2.0.0</c> tag: the shipped generator emitted <c>schemaVersion: 2</c> and the shipped engine
        /// accepted <c>1–2</c>. <b>Schemas 1 and 2 are the only released schemas</b>; the three unreleased bumps that
        /// had accumulated (dynamic-member routing at 3, the prop-layout row at 4, per-carrier
        /// <c>BindDefinition</c> at 5) were collapsed into a single schema <b>3</b>, which also carries the
        /// registered name and <c>#line</c> path form. An unreleased increment is not a migration step, and
        /// advertising three of them would claim a history no user could have.</para>
        /// <para><b>2.1 narrows the floor 1 → 3</b> — the only narrowing this window has had, and a
        /// real, not-yet-shipped binary break: a released schema 1–2 manifest's IL names
        /// <c>PrecompiledExtensionBinding..ctor(string, string)</c>, which the optional third parameter removed from
        /// metadata, so accepting one faults at <c>Register</c> instead of falling back. The floor is exactly the
        /// schema at which the three-argument constructor became the only one — which is why it equals
        /// <see cref="PrecompiledSchema.PropLayoutFingerprintSchemaVersion"/>, asserted as that identity rather than
        /// as a coincidence of two literals. That identity now also means <c>Min == Max == Current</c>: with the
        /// unreleased history collapsed there is exactly one readable shape, and the window is a point.</para>
        /// </summary>
        [Fact]
        public void SchemaConstantsPinTheCollapsedWindowAgainstTheReleasedLine()
        {
            Assert.Equal(3, PrecompiledSchema.MinSupportedSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.MaxSupportedSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.CurrentSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.PropLayoutFingerprintSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.PerCarrierLocalsSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.RegisteredNameSchemaVersion);
            Assert.Equal(3, PrecompiledSchema.LinePathFormSchemaVersion);

            // The floor is the prop-layout schema *because* that is where the constructor arity changed. Stated as an
            // identity so a future bump of one without the other has to justify itself.
            Assert.Equal(PrecompiledSchema.PropLayoutFingerprintSchemaVersion,
                PrecompiledSchema.MinSupportedSchemaVersion);

            // Every field that landed in the collapse shares the one increment, so a future field that needs its own
            // schema step cannot quietly reuse this number.
            Assert.Equal(PrecompiledSchema.CurrentSchemaVersion, PrecompiledSchema.RegisteredNameSchemaVersion);
            Assert.Equal(PrecompiledSchema.CurrentSchemaVersion, PrecompiledSchema.LinePathFormSchemaVersion);

            Assert.False(PrecompiledSchema.IsSupported(0));
            Assert.False(PrecompiledSchema.IsSupported(4));
            // Below the floor: the two RELEASED schemas, which is exactly the set the 2.1 break excludes.
            Assert.False(PrecompiledSchema.IsSupported(1));
            Assert.False(PrecompiledSchema.IsSupported(2));
            Assert.True(PrecompiledSchema.IsSupported(3));
        }

        /// <summary>The generator only emits <c>DynamicMember</c> routing at or above the
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

        /// <summary>The second guard behind fingerprint arity (the first is that the generator constructs a real
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

        /// <summary>The props↔code↔runtime lockstep gate. <c>Heddle.Generator.props</c> is a second physical
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
            Assert.Equal(HeddleBuildOptions.DefaultNodeFallback ? "true" : "false",
                props[HeddleBuildOptions.NodeFallbackProperty]);

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
            Assert.True(HeddleBuildOptions.DefaultNodeFallback);
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
        /// <para>The structural gate. The per-item metadata names live in three physical places —
        /// <c>Heddle.Generator.props</c> declares them <c>CompilerVisibleItemMetadata</c> (without which Roslyn does
        /// not surface them at all), <c>Heddle.Generator.targets</c> carries <c>HeddleTemplate</c> onto
        /// <c>AdditionalFiles</c>, and the generator reads <c>build_metadata.AdditionalFiles.&lt;name&gt;</c> — and a
        /// name absent from any one of the three is silently inert. That is what happened to <c>Name</c>: declared in
        /// props since 2.0, never read, so <c>samples/codegen-t4-successor</c>'s <c>Name="BuildReport"</c> did nothing
        /// for a whole release and the review that noticed it concluded the metadata should be deleted rather than
        /// wired.</para>
        /// <para><b>The second, worse half, found while wiring it.</b> The targets' <c>AdditionalFiles</c>
        /// item restated each metadatum as <c>&lt;Key&gt;%(HeddleTemplate.Key)&lt;/Key&gt;</c>. An
        /// <c>Include="@(HeddleTemplate)"</c> transform already copies every metadatum; outside a target a cross-item
        /// <c>%(Other.Metadata)</c> reference evaluates to the empty string, so each element <em>overwrote</em> the
        /// copied value with <c>""</c>. <b>All three</b> metadata were therefore inert from a real csproj — not just
        /// <c>Name</c> but <c>Key</c> and <c>Precompile</c>, whose only coverage injects
        /// <c>build_metadata.*</c> directly and so never crossed this file. This test pins both halves: every declared
        /// name is read, and no metadatum is nulled by a restatement.</para>
        /// <para>Asserted as <b>set equality</b>, not as a count: a count is made green by editing one digit, whereas
        /// set equality can only be made green by naming the metadatum whose wiring changed. The behavioural half of
        /// the gate is the <c>codegen-t4-successor</c> sample, whose generated entry class is named by its
        /// <c>Name</c> metadatum and called by name from <c>Program.cs</c> — so a metadatum that stops flowing fails
        /// that build outright.</para>
        /// </summary>
        [Fact]
        public void EveryDeclaredItemMetadataIsReadByTheGeneratorAndNotNulledByTheTargets()
        {
            var propsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Generator", "build", "Heddle.Generator.props")));
            var targetsXml = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Generator", "build", "Heddle.Generator.targets")));
            var generatorCs = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Generator", "HeddleTemplateGenerator.cs")));

            var declared = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(propsXml,
                @"<CompilerVisibleItemMetadata\s+Include=""AdditionalFiles""\s+MetadataName=""(?<name>\w+)"""))
                declared.Add(m.Groups["name"].Value);

            var read = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(generatorCs,
                @"""build_metadata\.AdditionalFiles\.(?<name>\w+)"""))
                read.Add(m.Groups["name"].Value);

            // The shipped set, spelled out — so a name added everywhere at once is still a reviewed change.
            Assert.Equal(new[] { "Key", "ModelType", "Name", "Precompile" }, declared.ToArray());
            Assert.Equal(declared.ToArray(), read.ToArray());

            // Nothing restates a metadatum the transform already carries: any such element evaluates to "" and
            // silently deletes the value. Strip the XML comments first — the comment there explains the trap.
            var withoutComments = Regex.Replace(targetsXml, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);
            var nulled = Regex.Matches(withoutComments, @"%\(HeddleTemplate\.(?<name>\w+)\)")
                .Cast<Match>()
                .Select(m => m.Groups["name"].Value)
                .ToList();
            Assert.True(nulled.Count == 0,
                "Heddle.Generator.targets restates item metadata the @(HeddleTemplate) transform already carries; " +
                "outside a target that evaluates to \"\" and deletes the value: " + string.Join(", ", nulled));
        }

        /// <summary>
        /// <para>The build tier's assembly-configuration surface, gated the way the item metadata above is. The
        /// primary form is <c>[assembly: HeddleModelAssembly(typeof(T))]</c>, which needs no wiring at all — a
        /// <c>typeof</c> cannot be spelled without a reference, so the C# compiler is the gate. These items are the
        /// escape hatch for the projects that cannot carry one, and an escape hatch has exactly the failure mode the
        /// metadata gate exists for: declared in XML, wired nowhere, silently inert.</para>
        /// <para><b>What is asserted, and why each half matters.</b> Every item the targets declare is appended to
        /// <c>@(ReferencePath)</c> — that, and only that, is how the assembly reaches the compiler, and through the
        /// compiler the generator's reference closure. No <c>CompilerVisibleItem</c> exists for either: the generator
        /// deliberately reads nothing here and loads nothing, so build output stays a function of the compilation's
        /// declared inputs rather than of a path something read mid-build. Neither name gains a property default, so
        /// the props-defaults count above is untouched and absent items are byte-identical output. And the generator
        /// sources never name them, which is the same claim stated where it could regress.</para>
        /// <para>Set equality, not a count, for the reason the metadata gate gives: a count is made green by editing
        /// one digit. The behavioural half is <c>samples/precompiled-app</c>, whose model assembly is reachable only
        /// through <c>@(HeddleModelAssembly)</c> — stop appending it and that sample fails to build.</para>
        /// </summary>
        [Fact]
        public void EveryDeclaredAssemblyItemReachesTheCompilerThroughReferencePath()
        {
            var propsPath = FindRepoFile(Path.Combine("src", "Heddle.Generator", "build", "Heddle.Generator.props"));
            var targetsPath = FindRepoFile(
                Path.Combine("src", "Heddle.Generator", "build", "Heddle.Generator.targets"));
            var propsXml = File.ReadAllText(propsPath);
            var targetsXml = File.ReadAllText(targetsPath);
            var withoutComments = Regex.Replace(targetsXml, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);

            var appended = new SortedSet<string>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(withoutComments,
                @"<ReferencePath\s+Include=""@\((?<name>\w+)\)""\s*/>"))
                appended.Add(m.Groups["name"].Value);

            Assert.Equal(new[] { "HeddleExtensionAssembly", "HeddleModelAssembly" }, appended.ToArray());

            // The item's job is to reach the compiler, not the generator. A CompilerVisibleItem would hand the
            // generator a path to read, which is exactly the determinism the ReferencePath route preserves.
            Assert.DoesNotContain("<CompilerVisibleItem ", propsXml, StringComparison.Ordinal);
            Assert.DoesNotContain("<CompilerVisibleItem ", targetsXml, StringComparison.Ordinal);

            // Items with no default, not properties with one — so ReadPropsDefaults' count literal, the assertion
            // easiest to make green wrongly, is not touched by this surface at all.
            foreach (var name in appended)
            {
                Assert.DoesNotContain("<" + name + " Condition=", propsXml, StringComparison.Ordinal);
                Assert.DoesNotContain("<CompilerVisibleProperty Include=\"" + name + "\"", propsXml,
                    StringComparison.Ordinal);
            }

            // The generator loads nothing: it never names these items, because it never has to.
            var generatorCs = File.ReadAllText(FindRepoFile(
                Path.Combine("src", "Heddle.Generator", "HeddleTemplateGenerator.cs")));
            foreach (var name in appended)
                Assert.DoesNotContain(name, generatorCs, StringComparison.Ordinal);

            // Documented where the rest of the build surface is documented; an undocumented escape hatch is one
            // nobody can use and nobody can review.
            var docs = File.ReadAllText(FindRepoFile(Path.Combine("docs", "precompilation.md")));
            foreach (var name in appended)
                Assert.Contains("@(" + name + ")", docs, StringComparison.Ordinal);
        }

        private static Dictionary<string, string> ReadPropsDefaults()
        {
            var path = FindRepoFile(Path.Combine("src", "Heddle.Generator", "build", "Heddle.Generator.props"));
            var xml = File.ReadAllText(path);
            var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(xml, @"<(?<name>Heddle\w+) Condition=""[^""]*"">(?<value>[^<]*)</\k<name>>"))
                defaults[match.Groups["name"].Value] = match.Groups["value"].Value;

            // Presence is part of the assertion: a structural change that breaks the parse must be a red test.
            Assert.Equal(7, defaults.Count);
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
