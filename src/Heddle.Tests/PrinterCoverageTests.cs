using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.TestCorpus;
#if NET10_0_OR_GREATER
using Heddle.Tool.Compile.Sites;
#endif
using Xunit;

namespace Heddle.Tests
{
    /// <summary>The printer's corpus gate (P3-R7): the site printer runs over every bound corpus
    /// row's record, and a decline the printer records on a row without the field is a red gate —
    /// rows declaring neither refusals nor late-bound functions must print every site, except calls
    /// to engine-internal default built-ins: generated code names only what the consumer's assembly
    /// can see (P3-R2), so those decline by decision, are listed in the template's HED7031 notice,
    /// and are rebuilt from data at load (roundtrip-proven, strict-served from the record). Rows with
    /// refusals or late-bound calls decline by decision (data path + strict kind) and are
    /// inventoried but not gated here. The printer ships in Heddle.Tool (net10-only), so other
    /// targets skip. Serialized — the build fixtures are process-global state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class PrinterCoverageTests : IDisposable
    {
        public PrinterCoverageTests()
        {
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
        }

        // The site printer ships in Heddle.Tool, which is net10-only, so this gate has a subject on that leg alone.
        // On the other legs it is explicit (reported as not run) rather than skipped: the CI wrapper runs with
        // --fail-skips on, which would fail the leg for a subject that does not exist there, and the class must
        // still declare a fact on every leg for the test-classes.txt inventory to hold.
#if NET10_0_OR_GREATER
        [Fact]
#else
        [Fact(Explicit = true)]
#endif
        public void NoUndeclaredRowDeclinesSites()
        {
#if NET10_0_OR_GREATER
            RunGate();
#else
            Assert.Fail("The site printer ships in Heddle.Tool (net10-only); this gate has no subject on this target.");
#endif
        }

#if NET10_0_OR_GREATER
        private static void RunGate()
        {
            var offenders = new List<string>();
            var inventory = new List<string>();
            foreach (var row in CorpusIntent.Rows)
            {
                if (!row.Bound || CompiledFormHarness.IsUnresolvableRow(row.Name))
                    continue;
                string text = CompiledFormHarness.CorpusText(row.Name);
                var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
                var modelEx = CompiledFormHarness.ModelExFor(row, out _, out _);
                var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out var context);
                if (!template.CompileResult.Success || context.CompileErrors.Count != 0)
                    continue;
                var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx,
                    buildOptions, row);
                var input = new SitePrinter.Input
                {
                    Record = context.FormRecord,
                    ContentHash = artifact.Templates.Count != 0
                        ? artifact.Templates[0].ContentHash
                        : string.Empty,
                    TemplateIndex = 0,
                    MemberBase = 0,
                    ExpressionBase = 0,
                    CSharpBase = 0
                };
                var printed = SitePrinter.Print(input, artifact);
                foreach (var decline in printed.Declines)
                    inventory.Add(row.Name + ": " + decline + " (" + decline.Why + ")");
                if (row.Refusals.Count == 0 && row.LateBound.Count == 0)
                    foreach (var decline in printed.Declines)
                        if (!IsByDecisionBuiltInDecline(decline))
                            offenders.Add(row.Name + ": " + decline + " (" + decline.Why + ")");
            }

            Assert.True(offenders.Count == 0,
                "Printer declines on rows declaring no refusals/lateBound (beyond by-decision built-ins): " +
                string.Join("; ", offenders.ToArray()) + ". Full decline inventory: " +
                string.Join("; ", inventory.ToArray()));
        }

        /// <summary>A decline on a call to an engine-internal default built-in: the P3-R2 by-decision
        /// class. Matches the printer's exact sentence shape, so a reworded printer fails closed
        /// (the decline offends again) rather than silently widening the exemption; the name must
        /// resolve to a method on the engine's own built-in container, so a newly unprintable call
        /// offends too.</summary>
        private static bool IsByDecisionBuiltInDecline(SiteDecline decline)
        {
            const string prefix = "call to '";
            const string suffix = "' is not a public static method";
            var why = decline == null ? null : decline.Why;
            if (string.IsNullOrEmpty(why) || !why.StartsWith(prefix, StringComparison.Ordinal) ||
                !why.EndsWith(suffix, StringComparison.Ordinal))
                return false;
            var name = why.Substring(prefix.Length, why.Length - prefix.Length - suffix.Length);
            return EngineInternalBuiltIns.Contains(name);
        }

        private static readonly HashSet<string> EngineInternalBuiltIns = new HashSet<string>(
            typeof(HeddleTemplate).Assembly.GetType("Heddle.Runtime.Expressions.BuiltInFunctions")
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                    BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name),
            StringComparer.Ordinal);
#endif
    }
}
