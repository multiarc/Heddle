extern alias gen;
using System.Linq;
using Heddle.Generator.Diagnostics;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Validates diagnostic forwarding: real IDs when available, <c>HED7012</c>/<c>HED7013</c> as fallback wrappers,
    /// severity from entry subtype, and <c>Fix</c> text carried into the message.
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

        /// <summary>HED7012/HED7013 wrap an entry <b>carrying no id</b> — nothing else.</summary>
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

        /// <summary>Id-less SLL warning keeps HED7013 wrapper and gains its Fix text.</summary>
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

        /// <summary>Id-carrying parse error reaches build under its own id, not a HED70xx wrapper.</summary>
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
