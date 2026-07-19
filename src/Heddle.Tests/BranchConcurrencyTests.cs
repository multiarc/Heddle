using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The standards-mandated parallel-render isolation proof (phase 3 criterion 6): many concurrent renders
    /// of one compiled branch-set template with opposite per-thread conditions show no cross-talk, because
    /// all per-render branch state lives in <c>ScopeLocals</c> frames created inside a single render lineage
    /// and never shared across threads (extension instances hold no mutable per-render state).
    /// </summary>
    public class BranchConcurrencyTests
    {
        public class Flag { public bool A { get; set; } }
        public class Cell { public bool F { get; set; } public bool A { get; set; } }
        public class ListModel { public List<Cell> Items { get; set; } }

        [Fact]
        public void ParallelRendersOfOppositeConditionsAreIsolated()
        {
            HeddleTemplate.Configure(typeof(BranchConcurrencyTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var t = new HeddleTemplate("@if(A){{FEATURED}}@else(){{REGULAR}}", new CompileContext(typeof(Flag)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            Parallel.For(0, 20000, i =>
            {
                bool a = i % 2 == 0;
                var result = t.Generate(new Flag { A = a });
                Assert.Equal(a ? "FEATURED" : "REGULAR", result);
            });
        }

        [Fact]
        public void ParallelRendersWithElifChainAreIsolated()
        {
            HeddleTemplate.Configure(typeof(BranchConcurrencyTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var t = new HeddleTemplate("@if(F){{F}}@elif(A){{A}}@else(){{R}}", new CompileContext(typeof(Cell)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            Parallel.For(0, 20000, i =>
            {
                int m = i % 3;
                var model = new Cell { F = m == 0, A = m == 1 };
                var expected = m == 0 ? "F" : m == 1 ? "A" : "R";
                Assert.Equal(expected, t.Generate(model));
            });
        }

        [Fact]
        public void ParallelListRendersWithPerIterationSetsAreIsolated()
        {
            HeddleTemplate.Configure(typeof(BranchConcurrencyTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var t = new HeddleTemplate("@list(Items){{@if(F){{F}}@else(){{R}}}}", new CompileContext(typeof(ListModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            Parallel.For(0, 8000, i =>
            {
                bool first = i % 2 == 0;
                var model = new ListModel
                {
                    Items = new List<Cell> { new Cell { F = first }, new Cell { F = !first }, new Cell { F = first } }
                };
                var expected = string.Concat(model.Items.Select(c => c.F ? "F" : "R"));
                Assert.Equal(expected, t.Generate(model));
            });
        }
    }
}
