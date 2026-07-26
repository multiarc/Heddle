using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The run-tier arm of the projection-equivalence corpus (generator plan phase 6 D12.5). Asserts the shared
    /// drain against <see cref="DiagnosticCorpusVectors"/>, which the editor suite and the build-tier suite assert
    /// against too — so the three hosts are compared to one another through one table instead of three sets of
    /// hand-written expectations that can drift apart in exactly the way this phase exists to stop.
    /// </summary>
    public class DiagnosticProjectionCorpusTests
    {
        public static TheoryData<string> Names
        {
            get
            {
                var data = new TheoryData<string>();
                foreach (var c in DiagnosticCorpusVectors.Cases)
                    data.Add(c.Name);
                return data;
            }
        }

        internal static DiagnosticCorpusVectors.Case Find(string name) =>
            DiagnosticCorpusVectors.Cases.First(c => c.Name == name);

        internal static string Format(string id, bool isWarning, int offset, int length) =>
            $"{id ?? "<null>"}/{(isWarning ? "W" : "E")}@{offset},{length}";

        private static (CompileContext compile, HeddleCompileResult result) Compile(string template,
            OutputProfile profile)
        {
            var options = new TemplateOptions("corpus")
            {
                OutputProfile = profile,
                RootPath = AppContext.BaseDirectory,
                FileNamePostfix = ".heddle"
            };
            var compile = new CompileContext(options, ExType.Dynamic);
            return (compile, new HeddleTemplate(template, compile).CompileResult);
        }

        private static List<string> Drained(List<HeddleDiagnosticEntry> entries) =>
            entries.Select(e => Format(e.Id, e.IsWarning, e.Offset, e.Length)).ToList();

        [Theory]
        [MemberData(nameof(Names))]
        public void TheFullDrainMatchesTheCorpusUnderHtml(string name)
        {
            var c = Find(name);
            var (compile, result) = Compile(c.Template, OutputProfile.Html);

            Assert.Equal(c.Entries, Drained(HeddleDiagnosticProjection.Drain(compile, result.Context)));
        }

        /// <summary>The profile-dependent row is the point: only the encoding lint changes with the profile, and
        /// the corpus says so per fixture rather than the test guessing.</summary>
        [Theory]
        [MemberData(nameof(Names))]
        public void TheFullDrainMatchesTheCorpusUnderText(string name)
        {
            var c = Find(name);
            var (compile, result) = Compile(c.Template, OutputProfile.Text);

            Assert.Equal(c.TextProfileEntries, Drained(HeddleDiagnosticProjection.Drain(compile, result.Context)));
        }

        /// <summary>The parse-channel subset the build tier can forward today. This is the measured half of the
        /// program's recorded compile-channel gap: if a diagnostic ever moves channel, this goes red on the
        /// fixture that moved, and the build-tier arm goes red with it.</summary>
        [Theory]
        [MemberData(nameof(Names))]
        public void TheParseChannelSubsetIsExactlyWhatTheCorpusDeclares(string name)
        {
            var c = Find(name);
            var (_, result) = Compile(c.Template, OutputProfile.Html);

            Assert.Equal(c.ParseChannel, Drained(HeddleDiagnosticProjection.Drain(result.Context)));
            // …and the subset really is a subset — a declaration error in the table itself.
            Assert.Empty(c.ParseChannel.Except(c.Entries));
        }

        /// <summary>The third surface: what <see cref="HeddleCompileResult"/> renders for the same fixture is the
        /// error half of the same drain, position for position.</summary>
        [Theory]
        [MemberData(nameof(Names))]
        public void CompileResultRendersTheSameEntries(string name)
        {
            var c = Find(name);
            var (_, result) = Compile(c.Template, OutputProfile.Html);

            var rendered = result.ErrorList
                .Select(e => Format(e.DiagnosticId, e is HeddleCompileWarning, e.Position.StartIndex,
                    e.Position.Length))
                .ToList();

            Assert.Empty(rendered.Except(c.Entries));
        }
    }
}
