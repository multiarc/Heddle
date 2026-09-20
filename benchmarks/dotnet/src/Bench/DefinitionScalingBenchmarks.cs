using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using Heddle.Language;
using Heddle.Runtime;
using TemplateOptions = Heddle.Data.TemplateOptions;
using OutputProfile = Heddle.Data.OutputProfile;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// What a definitions library costs the page that uses it: <c>N</c> definitions and <c>M</c> calls to
    /// them, the shape of a page over a component library. Part of the cold sidebar — it is a parse and
    /// compile cost, paid once.
    ///
    /// <para>Every output chain keeps an isolated view of the definitions visible where it was written.
    /// While that view was built by copying every definition's body for every chain, this grew with the
    /// calls times the square of the definitions; the rows here are the proof it no longer does, and the
    /// place a return of that growth would show. Read the rows against each other: <c>M</c> is four times
    /// <c>N</c>, so linear work grows with <c>N</c> and the old growth with its cube — which is why the rows
    /// stop at 50: on the copying parser 100 definitions allocated 6.6 GB per operation and 200 allocated 91 GB.</para>
    /// </summary>
    [MemoryDiagnoser]
    public class DefinitionScalingBenchmarks
    {
        public sealed class Model
        {
            public string Title { get; set; } = "t";
        }

        [Params(10, 25, 50)]
        public int Definitions { get; set; }

        private string _document;

        internal static string Document(int definitions, int calls)
        {
            var text = new StringBuilder("@%\n");
            for (int i = 0; i < definitions; i++)
                text.Append("<card").Append(i).Append(">{{<div>@(Title) @(Title)</div>}}\n");
            text.Append("%@\n");
            for (int i = 0; i < calls; i++)
                text.Append("<p>@card").Append(i % Math.Max(definitions, 1)).Append("()</p>\n");
            return text.ToString();
        }

        [GlobalSetup]
        public void Setup()
        {
            _document = Document(Definitions, Definitions * 4);
        }

        [Benchmark(Baseline = true)]
        public void Parse()
        {
            DocumentParser.Parse(_document,
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Text }, typeof(Model)), out _);
        }

        [Benchmark]
        public bool Compile()
        {
            var template = new HeddleTemplate(_document,
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Text }, typeof(Model)));
            return template.CompileResult.Success;
        }
    }
}
