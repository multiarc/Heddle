using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Heddle;
using Heddle.Runtime;

namespace Heddle.Performance
{
    /// <summary>
    /// Phase 3 branching allocation/perf guards (spec D15 / criterion 8), run with
    /// <c>dotnet run -c Release -- --filter *BranchRenderBenchmarks*</c>.
    /// <list type="bullet">
    /// <item><c>RenderListNoBranches</c> / <c>RenderListIfPair10K</c> — no channel participant: 0 B frame
    /// delta vs the pre-phase baseline (guards the D2 fast path + D7 opportunistic no-op).</item>
    /// <item><c>RenderListIfElse10K</c> — one <c>ScopeLocals</c> per iteration (the documented worst-case
    /// trade, ~320 000 B on x64 with no JIT rescue; newer runtimes stack-allocate part of it).</item>
    /// <item><c>RenderFlagshipNoPublish</c> — a realistic never-publishes document: 0 B frame delta.</item>
    /// </list>
    /// </summary>
    [MemoryDiagnoser]
    public class BranchRenderBenchmarks
    {
        public class Cell { public bool F { get; set; } public string Name { get; set; } }
        public class ListModel { public List<Cell> Items { get; set; } }
        public class FlagModel { public bool IsFeatured { get; set; } public bool IsArchived { get; set; } }

        private HeddleTemplate _noBranches;
        private HeddleTemplate _ifPair;
        private HeddleTemplate _ifElse;
        private HeddleTemplate _flagship;
        private ListModel _listModel;
        private FlagModel _flagModel;

        [GlobalSetup]
        public void Setup()
        {
            HeddleTemplate.Configure(typeof(BranchRenderBenchmarks).GetTypeInfo().Assembly);

            _noBranches = new HeddleTemplate("@list(Items){{@(Name)}}", new CompileContext(typeof(ListModel)));
            _ifPair = new HeddleTemplate("@list(Items){{@if(F){{@(Name)}}@ifnot(F){{-}}}}", new CompileContext(typeof(ListModel)));
            _ifElse = new HeddleTemplate("@list(Items){{@if(F){{@(Name)}}@else(){{-}}}}", new CompileContext(typeof(ListModel)));
            _flagship = new HeddleTemplate(
                "<div>@if(IsFeatured){{<b>F</b>}}@ifnot(IsFeatured){{@if(IsArchived){{<i>A</i>}}@ifnot(IsArchived){{R}}}}</div>",
                new CompileContext(typeof(FlagModel)));

            _listModel = new ListModel
            {
                Items = Enumerable.Range(0, 10000).Select(i => new Cell { F = i % 2 == 0, Name = "n" + i }).ToList()
            };
            _flagModel = new FlagModel { IsFeatured = false, IsArchived = true };
        }

        [Benchmark]
        public string RenderListNoBranches() => _noBranches.Generate(_listModel);

        [Benchmark]
        public string RenderListIfPair10K() => _ifPair.Generate(_listModel);

        [Benchmark]
        public string RenderListIfElse10K() => _ifElse.Generate(_listModel);

        [Benchmark]
        public string RenderFlagshipNoPublish() => _flagship.Generate(_flagModel);
    }
}
