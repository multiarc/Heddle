extern alias gen;
using System.Linq;
using Heddle.Generator.Diagnostics;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Generator plan phase 6 D2/D3 — build-time forwarding of front-end diagnostics. Until this phase the
    /// warning path collapsed every forwarded warning into <c>HED7013</c>, discarding both
    /// <c>HeddleCompileError.DiagnosticId</c> and <c>HeddleCompileWarning.Fix</c>, while the error path one loop
    /// above already forwarded real IDs — two rules for one seam, and a suppression surface
    /// (<c>#pragma warning disable</c>, <c>NoWarn</c>) that could not name a single Heddle lint. These pin the
    /// one rule: real ID when the entry has one, <c>HED7012</c>/<c>HED7013</c> only when it does not, severity
    /// from the entry's subtype, and the <c>Fix</c> carried into the message.
    /// </summary>
    public class ForwardedDiagnosticTests
    {
        [Theory]
        [InlineData("HED2004", true, DiagnosticSeverity.Warning)]
        [InlineData("HED3005", true, DiagnosticSeverity.Warning)]
        [InlineData("HED4003", false, DiagnosticSeverity.Error)]
        public void ForwardedKeepsTheFrontEndIdAtTheSubtypeSeverity(string id, bool isWarning,
            DiagnosticSeverity expected)
        {
            var descriptor = GeneratorDiagnostics.Forwarded(id, isWarning);

            Assert.Equal(id, descriptor.Id);
            Assert.Equal(expected, descriptor.DefaultSeverity);
            Assert.Equal("Heddle.Precompile", descriptor.Category);
            Assert.True(descriptor.IsEnabledByDefault);
            // The front end has already formatted the message, so the descriptor is a passthrough.
            Assert.Equal("{0}", descriptor.MessageFormat.ToString());
        }

        /// <summary>The contract <c>GeneratorDiagnostics</c>' doc comment and <c>docs/precompilation.md</c> have
        /// always stated: HED7012/HED7013 wrap an entry <b>carrying no id</b> — nothing else.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void IdLessEntriesStillUseTheHed7012Hed7013Wrappers(string id)
        {
            Assert.Equal("HED7013", GeneratorDiagnostics.Forwarded(id, isWarning: true).Id);
            Assert.Equal("HED7012", GeneratorDiagnostics.Forwarded(id, isWarning: false).Id);
        }

        [Fact]
        public void ForwardedDescriptorsAreCachedPerIdAndSeverity()
        {
            Assert.Same(GeneratorDiagnostics.Forwarded("HED2004", true),
                GeneratorDiagnostics.Forwarded("HED2004", true));
            Assert.NotSame(GeneratorDiagnostics.Forwarded("HED2004", true),
                GeneratorDiagnostics.Forwarded("HED2004", false));
        }

        [Fact]
        public void ForwardedMessageAppendsTheFixAsATrailingSentence()
        {
            Assert.Equal("boom", GeneratorDiagnostics.ForwardedMessage("boom", null));
            Assert.Equal("boom", GeneratorDiagnostics.ForwardedMessage("boom", string.Empty));
            Assert.Equal("boom Fix: do the other thing",
                GeneratorDiagnostics.ForwardedMessage("boom", "do the other thing"));
        }

        /// <summary>The id-less SLL parse-fallback warning is the one warning the parse channel produces today,
        /// and it carries a <c>Fix</c>. This pins the shape both halves of the fix produce for it: the
        /// <c>HED7013</c> wrapper (it has no id) <b>and</b> the Fix text, which the build tier used to drop.</summary>
        [Fact]
        public void TheIdLessSllWarningKeepsHed7013AndGainsItsFix()
        {
            var warning = new gen::Heddle.Data.HeddleCompileWarning
            {
                Error = "SLL failed",
                Fix = "SLL Mode failed, fix template or investigate why SLL is failing",
                Position = new gen::Heddle.Strings.Core.BlockPosition(0, 0)
            };

            Assert.Equal("HED7013", GeneratorDiagnostics.Forwarded(warning.DiagnosticId, true).Id);
            Assert.Equal("SLL failed Fix: SLL Mode failed, fix template or investigate why SLL is failing",
                GeneratorDiagnostics.ForwardedMessage(warning.Error, warning.Fix));
        }

        /// <summary>End-to-end: a template whose parse raises an id-carrying diagnostic reports it under that id
        /// in MSBuild output, not under a HED70xx wrapper — so <c>NoWarn</c>/<c>#pragma</c> on the id the editor
        /// shows is the id the build honours.</summary>
        [Fact]
        public void AnIdCarryingParseErrorReachesTheBuildUnderItsOwnId()
        {
            var run = GeneratorHarness.Run(new[]
            {
                ("/repo/legacy.heddle", "@import(){{other}}\nHello\n")
            });

            var reported = run.GeneratorDiagnostics.Single(d => d.Id == gen::Heddle.Data.HeddleDiagnosticIds.LegacyImportDirective);
            Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
            Assert.DoesNotContain(run.GeneratorDiagnostics, d => d.Id == "HED7012");
        }
    }
}
