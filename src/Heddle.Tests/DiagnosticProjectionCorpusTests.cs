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
    /// Run-tier arm of the projection-equivalence corpus: all three hosts (run, editor, build) assert against
    /// <see cref="DiagnosticCorpusVectors"/>.
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

        /// <summary>Only encoding lint changes with profile; the corpus specifies this per fixture.</summary>
        [Theory]
        [MemberData(nameof(Names))]
        public void TheFullDrainMatchesTheCorpusUnderText(string name)
        {
            var c = Find(name);
            var (compile, result) = Compile(c.Template, OutputProfile.Text);

            Assert.Equal(c.TextProfileEntries, Drained(HeddleDiagnosticProjection.Drain(compile, result.Context)));
        }

        /// <summary>The parse-channel subset the build tier can forward; if a diagnostic moves channel, this
        /// and the build-tier arm both go red.</summary>
        [Theory]
        [MemberData(nameof(Names))]
        public void TheParseChannelSubsetIsExactlyWhatTheCorpusDeclares(string name)
        {
            var c = Find(name);
            var (_, result) = Compile(c.Template, OutputProfile.Html);

            Assert.Equal(c.ParseChannel, Drained(HeddleDiagnosticProjection.Drain(result.Context)));
            Assert.Empty(c.ParseChannel.Except(c.Entries));
        }

        /// <summary><see cref="HeddleCompileResult"/> renders the same entries, position for position.</summary>
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
