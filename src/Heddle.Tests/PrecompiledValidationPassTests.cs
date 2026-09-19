using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Heddle.Data;
using Heddle.Extensions;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Fixture artifacts over in-memory rows. Each builder returns an artifact whose rows the
    /// loader's internal constructor reads; registration goes through
    /// <see cref="CompiledFormHarness.RegisterArtifact"/>.</summary>
    internal static class PassEntries
    {
        private static CompiledArtifact Artifact() => CompiledFormHarness.MinimalArtifact();

        private static int Extension(CompiledArtifact artifact, string name, string fullName,
            string assemblySimpleName, string fingerprint = null)
        {
            artifact.Extensions.Add(new CompiledExtensionRow
            {
                RegistryName = name,
                Type = CompiledFormHarness.TypeRef(fullName, assemblySimpleName),
                Fingerprint = fingerprint
            });
            return artifact.Extensions.Count - 1;
        }

        private static int Function(CompiledArtifact artifact, string name, CompiledTypeRef target,
            int overloads)
        {
            artifact.Functions.Add(new CompiledFunctionRow
            {
                Name = name,
                Target = target,
                OverloadCount = overloads
            });
            return artifact.Functions.Count - 1;
        }

        /// <summary>A well-formed row fingerprinted Text/Native/false with no bindings — passes under
        /// <see cref="PrecompiledValidationPassTests.Match"/>.</summary>
        public static CompiledArtifact Clean(string key)
        {
            var artifact = Artifact();
            artifact.Templates.Add(CompiledFormHarness.TemplateRow(key));
            return artifact;
        }

        /// <summary>A row whose only defect is an extension binding no live registry answers — fails step 2.</summary>
        public static CompiledArtifact BadExtension(string key)
        {
            var artifact = Artifact();
            var ext = Extension(artifact, "nosuchextension", "No.Such.Type", "No.Such.Assembly");
            artifact.Templates.Add(CompiledFormHarness.TemplateRow(key,
                extensionRefs: new[] { ext }));
            return artifact;
        }

        /// <summary>A row fingerprinted Html/Native/false — fails step 1 against a Text request.</summary>
        public static CompiledArtifact HtmlFingerprint(string key)
        {
            var artifact = Artifact();
            artifact.Templates.Add(CompiledFormHarness.TemplateRow(key, profile: "Html"));
            return artifact;
        }

        /// <summary>A row naming an extension the live registry <b>does</b> answer ("if") under a type name that
        /// is not its AQN: the default binding match refuses it, a custom
        /// <see cref="PrecompiledTemplates.BindingResolver"/> can accept it. The one fixture whose verdict depends
        /// on the resolver being passed through.</summary>
        public static CompiledArtifact ResolverSensitive(string key)
        {
            var artifact = Artifact();
            var ext = Extension(artifact, "if", "Some.Other.If", "Some.Other.Assembly");
            artifact.Templates.Add(CompiledFormHarness.TemplateRow(key,
                extensionRefs: new[] { ext }));
            return artifact;
        }

        /// <summary>A row with a null-target function row — a late-bound call site for a name no registry
        /// answers ("titlecase"): fails the function step.</summary>
        public static CompiledArtifact Marker(string key)
        {
            var artifact = Artifact();
            var fn = Function(artifact, "titlecase", null, 0);
            artifact.Templates.Add(CompiledFormHarness.TemplateRow(key,
                functionRefs: new[] { fn }));
            return artifact;
        }

        /// <summary>Deliberately not in key order: the report's ordering must come from the pass, not from
        /// artifact or dictionary order (PrecompiledTemplates.Entries is a Dictionary.Values snapshot).</summary>
        public static CompiledArtifact Mixed()
        {
            var artifact = Artifact();
            artifact.Templates.Add(CompiledFormHarness.TemplateRow("pass/c.heddle", profile: "Html"));
            artifact.Templates.Add(CompiledFormHarness.TemplateRow("pass/a.heddle"));
            var ext = Extension(artifact, "nosuchextension", "No.Such.Type", "No.Such.Assembly");
            artifact.Templates.Add(CompiledFormHarness.TemplateRow("pass/b.heddle",
                extensionRefs: new[] { ext }));
            return artifact;
        }
    }

    /// <summary>
    /// The aggregate post-configuration validation pass. The per-request gauntlet answers one entry at a time and
    /// only where a request reaches it; this pass runs the same gauntlet over every registered entry once the host
    /// has finished configuring and reports <b>all</b> failures together.
    /// <para>Serialized with the other registry suites — the registry is process-global static state.</para>
    /// </summary>
    [Collection("PrecompiledRegistrySerial")]
    public class PrecompiledValidationPassTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;
        private readonly Func<PrecompiledExtensionBinding, Type, bool> _savedResolver;

        public PrecompiledValidationPassTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            _savedResolver = PrecompiledTemplates.BindingResolver;
            PrecompiledTemplates.ResetForTests();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.BindingResolver = _savedResolver;
            PrecompiledTemplates.ResetForTests();
        }

        /// <summary>The fixtures are fingerprinted Text/Native/false; the 2.0 engine defaults are Html/true, so a
        /// request must pin the dimensions not under test or step 1 masks everything after it.</summary>
        internal static TemplateOptions Match() =>
            new TemplateOptions { OutputProfile = OutputProfile.Text, TrimDirectiveLines = false };

        private static Version RuntimeVersion =>
            typeof(PrecompiledTemplates).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

        private static string CompatibleVersion =>
            $"{RuntimeVersion.Major}.{Math.Max(RuntimeVersion.Minor, 0)}.{Math.Max(RuntimeVersion.Build, 0)}";

        private static void Register(CompiledArtifact artifact, string name) =>
            CompiledFormHarness.RegisterArtifact(artifact, name);

        /// <summary>The point of the pass versus the per-request gate: two entries fail for two different reasons
        /// and both are reported from one call, with the gauntlet's own pinned detail strings.</summary>
        [Fact]
        public void CollectsEveryFailureRatherThanStoppingAtTheFirst()
        {
            Register(PassEntries.Mixed(), "HeddleTestAsm_PassMixed");

            var report = PrecompiledTemplates.ValidateAll(Match());

            Assert.Equal(3, report.EntriesChecked);
            Assert.Equal(2, report.Failures.Count);
            Assert.False(report.PassedForValidatedOptions);

            var b = report.Failures.Single(f => f.TemplateKey == "pass/b.heddle");
            Assert.Equal(PrecompiledFallbackReason.ExtensionBindingMismatch, b.Reason);
            Assert.Equal("Extension 'nosuchextension': manifest=No.Such.Type, No.Such.Assembly live=<unresolved>",
                b.Detail);
            Assert.Equal("HED7101", b.DiagnosticId);

            var c = report.Failures.Single(f => f.TemplateKey == "pass/c.heddle");
            Assert.Equal(PrecompiledFallbackReason.OptionsMismatch, c.Reason);
            Assert.Equal("OutputProfile: manifest=Html request=Text", c.Detail);
        }

        /// <summary>Report order is by template key, ordinal — <see cref="PrecompiledTemplates.Entries"/> is a
        /// dictionary-values snapshot, so without this the report's order is an implementation accident.</summary>
        [Fact]
        public void FailuresAreOrderedByTemplateKey()
        {
            Register(PassEntries.Mixed(), "HeddleTestAsm_PassOrder");

            var report = PrecompiledTemplates.ValidateAll(Match());

            Assert.Equal(new[] { "pass/b.heddle", "pass/c.heddle" },
                report.Failures.Select(f => f.TemplateKey).ToArray());
        }

        /// <summary>Four gauntlet inputs are per-request, so a single pass can only be complete with respect to one
        /// options shape — the report therefore names the shape it used, and the same registry gets opposite
        /// verdicts under two shapes.</summary>
        [Fact]
        public void ReportNamesTheOptionsItValidatedAgainst()
        {
            Register(PassEntries.Mixed(), "HeddleTestAsm_PassScope");

            var text = PrecompiledTemplates.ValidateAll(Match());
            Assert.Equal(OutputProfile.Text, text.ValidatedFingerprint.Profile);
            Assert.Equal(ExpressionMode.Native, text.ValidatedFingerprint.ExpressionMode);
            Assert.False(text.ValidatedFingerprint.TrimDirectiveLines);
            Assert.Null(text.ValidatedFunctions);
            Assert.False(text.ValidatedStaleness);

            // pass/c.heddle is the Html-fingerprinted entry: it fails under Text and passes under Html, and
            // pass/a.heddle does the reverse. Neither report is the truth about the other's shape.
            var html = PrecompiledTemplates.ValidateAll(
                new TemplateOptions { OutputProfile = OutputProfile.Html, TrimDirectiveLines = false });
            Assert.Equal(OutputProfile.Html, html.ValidatedFingerprint.Profile);
            Assert.Contains(html.Failures, f => f.TemplateKey == "pass/a.heddle");
            Assert.DoesNotContain(html.Failures, f => f.TemplateKey == "pass/c.heddle");
        }

        /// <summary>The other two per-request inputs the report has to name: the effective function registry
        /// (reference identity — a request-scoped registry is a different answer) and whether staleness ran.</summary>
        [Fact]
        public void ReportNamesTheFunctionRegistryAndWhetherStalenessRan()
        {
            Register(PassEntries.Mixed(), "HeddleTestAsm_PassFunctions");

            var registry = new FunctionRegistry();
            var options = Match();
            options.Functions = registry;
            options.EnableFileChangeCheck = true;

            var report = PrecompiledTemplates.ValidateAll(options);

            Assert.Same(registry, report.ValidatedFunctions);
            Assert.True(report.ValidatedStaleness);
            // …and the rendered form distinguishes the two shapes, which is the half a host actually reads.
            Assert.Contains("Functions=<request registry>, staleness=checked", report.ToString());
        }

        /// <summary>The pass runs the host's <see cref="PrecompiledTemplates.BindingResolver"/>, exactly as the
        /// per-request gate does — a host that supplies one has told the engine what "the same binding" means, and
        /// an aggregate pass that ignored it would report failures no request would ever see.</summary>
        [Fact]
        public void HonoursTheHostBindingResolver()
        {
            Register(PassEntries.ResolverSensitive("pass/resolver.heddle"), "HeddleTestAsm_PassResolver");

            // The default AQN-sans-version match refuses this entry's binding…
            PrecompiledTemplates.BindingResolver = null;
            var strict = PrecompiledTemplates.ValidateAll(Match());
            Assert.Equal(PrecompiledFallbackReason.ExtensionBindingMismatch, Assert.Single(strict.Failures).Reason);

            // …and a host resolver that accepts it makes the same entry pass.
            PrecompiledTemplates.BindingResolver = (binding, liveType) => true;
            Assert.True(PrecompiledTemplates.ValidateAll(Match()).PassedForValidatedOptions);
        }

        /// <summary>A report can never exist without the options that scope it: there is no parameterless overload
        /// and no optional-argument form, so "everything is fine" is unsayable without saying for what.</summary>
        [Fact]
        public void EveryValidateAllOverloadRequiresTheOptionsThatScopeIt()
        {
            var overloads = typeof(PrecompiledTemplates)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == nameof(PrecompiledTemplates.ValidateAll))
                .ToList();

            Assert.NotEmpty(overloads);
            foreach (var overload in overloads)
            {
                var parameters = overload.GetParameters();
                Assert.Contains(parameters, p => p.ParameterType == typeof(TemplateOptions));
                Assert.All(parameters, p => Assert.False(p.IsOptional));
            }

            // And no property on the report reads as an unscoped verdict.
            var verdicts = typeof(PrecompiledValidationReport)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(bool))
                .Select(p => p.Name)
                .ToList();
            Assert.DoesNotContain("IsValid", verdicts);
            Assert.DoesNotContain("Success", verdicts);
            Assert.DoesNotContain("Passed", verdicts);
        }

        /// <summary>The pass re-uses the per-request gauntlet rather than restating it: entry by entry, the report
        /// says exactly what <see cref="PrecompiledTemplates.Validate"/> says under the same options.</summary>
        [Fact]
        public void AgreesWithThePerRequestGauntletEntryByEntry()
        {
            Register(PassEntries.Mixed(), "HeddleTestAsm_PassAgree");
            Register(PassEntries.Marker("pass/marker.heddle"), "HeddleTestAsm_PassAgreeMarker");

            var options = Match();
            var report = PrecompiledTemplates.ValidateAll(options);

            foreach (var entry in PrecompiledTemplates.Entries)
            {
                var direct = PrecompiledTemplates.Validate(entry, options);
                var fromReport = report.Failures
                    .Where(f => f.TemplateKey == entry.Key)
                    .Select(f => (PrecompiledFallbackEvent?)f)
                    .FirstOrDefault();

                Assert.Equal(direct.HasValue, fromReport.HasValue);
                if (direct.HasValue)
                {
                    Assert.Equal(direct.Value.Reason, fromReport.Value.Reason);
                    Assert.Equal(direct.Value.Detail, fromReport.Value.Detail);
                    Assert.Equal(direct.Value.DiagnosticId, fromReport.Value.DiagnosticId);
                }
            }
        }

        /// <summary>A marker entry is a failure of the pass exactly as it is of the gate — it is registered,
        /// and it will never render precompiled. Excluding it would make the pass quieter than the truth.</summary>
        [Fact]
        public void MarkerEntriesAreReported()
        {
            Register(PassEntries.Marker("pass/marker.heddle"), "HeddleTestAsm_PassMarker");

            var report = PrecompiledTemplates.ValidateAll(Match());

            var failure = Assert.Single(report.Failures);
            Assert.Equal("pass/marker.heddle", failure.TemplateKey);
            Assert.Equal(PrecompiledFallbackReason.UnsupportedFunction, failure.Reason);
        }

        /// <summary>The pass is a report, not a gate: it does not raise <c>OnFallback</c> (nothing degraded — no
        /// render happened) and <see cref="PrecompiledMismatchPolicy.Strict"/> does not make it throw. Both are the
        /// per-request path's behaviour, and this pass does not move it.</summary>
        [Fact]
        public void DoesNotRaiseOnFallbackAndDoesNotThrowUnderStrict()
        {
            Register(PassEntries.Mixed(), "HeddleTestAsm_PassQuiet");

            var raised = new List<PrecompiledFallbackEvent>();
            PrecompiledTemplates.OnFallback = raised.Add;

            var options = Match();
            options.PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict;

            var report = PrecompiledTemplates.ValidateAll(options);

            Assert.Equal(2, report.Failures.Count);
            Assert.Empty(raised);
        }

        /// <summary>An empty registry is a pass, not an error — and still reports the shape it is a pass for.</summary>
        [Fact]
        public void EmptyRegistryPasses()
        {
            var report = PrecompiledTemplates.ValidateAll(Match());

            Assert.Equal(0, report.EntriesChecked);
            Assert.Empty(report.Failures);
            Assert.True(report.PassedForValidatedOptions);
        }

        [Fact]
        public void NullOptionsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => PrecompiledTemplates.ValidateAll(null));
        }

        /// <summary>The rendered form a host logs. Pinned because it is the shape most hosts will actually read,
        /// and its whole job is to carry the scope alongside the verdict.</summary>
        [Fact]
        public void ToStringCarriesTheScopeAlongsideTheVerdict()
        {
            Register(PassEntries.Mixed(), "HeddleTestAsm_PassToString");

            Assert.Equal(
                "Precompiled validation: 2 of 3 registered entries failed under OutputProfile=Text, " +
                "ExpressionMode=Native, TrimDirectiveLines=false, Functions=<default>, staleness=not checked. " +
                "This verdict is about these options only.",
                PrecompiledTemplates.ValidateAll(Match()).ToString());
        }
    }
}
