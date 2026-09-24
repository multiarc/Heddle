using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Plan exit criterion 5 (P3-R3 rationale): a generated accessor allocates nothing beyond the
    /// engine's value-type box — a reference-type result allocates zero bytes, a value-type result one
    /// box — measured with <c>GC.GetAllocatedBytesForCurrentThread</c> around 1,000 reads of the exact
    /// shape the printer emits. The count-path fact pins the P3-R9 <c>ListExtension</c> change: the
    /// non-generic count for value-type elements only pre-sizes the result array, never a byte.</summary>
    public class GeneratedAccessorAllocationTests
    {
        private const int Reads = 1000;

        public sealed class AllocationOrder
        {
            public AllocationCustomer Customer { get; set; }
        }

        public sealed class AllocationCustomer
        {
            public AllocationAddress Address { get; set; }
        }

        public sealed class AllocationAddress
        {
            public decimal Total { get; set; }
            public string City { get; set; }
        }

        private static object ReferenceAccessor(object model)
        {
            var v0 = (AllocationOrder)model;
            var v1 = v0.Customer;
            var v2 = v1 == null ? null : v1.Address;
            return v2 == null ? null : (object)v2.City;
        }

        private static object ValueAccessor(object model)
        {
            var v0 = (AllocationOrder)model;
            var v1 = v0.Customer;
            var v2 = v1 == null ? null : v1.Address;
            var v3 = v2 == null ? default(decimal) : v2.Total;
            return (object)v3;
        }

        private static long MeasureReads(Func<object, object> accessor, object model, int reads)
        {
            // Warm up past JIT tiering so the measured window is the steady-state read alone.
            for (var i = 0; i < 100; i++)
                accessor(model);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < reads; i++)
                accessor(model);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public void ReferenceTypeResultAllocatesZeroBytes()
        {
            var model = new AllocationOrder
            {
                Customer = new AllocationCustomer { Address = new AllocationAddress { City = "Minsk" } }
            };
            Assert.Equal("Minsk", ReferenceAccessor(model));
            Assert.Equal(0, MeasureReads(ReferenceAccessor, model, Reads));
        }

        [Fact]
        public void ValueTypeResultAllocatesOneBoxPerRead()
        {
            var model = new AllocationOrder
            {
                Customer = new AllocationCustomer { Address = new AllocationAddress { Total = 129.50m } }
            };
            Assert.Equal((object)129.50m, ValueAccessor(model));
            var one = MeasureReads(ValueAccessor, model, 1);
            Assert.True(one > 0, "A value-type result must box once per read.");
            Assert.Equal(Reads * one, MeasureReads(ValueAccessor, model, Reads));
        }

        public sealed class IntCollections
        {
            public List<int> Nums { get; set; }
        }

        public sealed class IntArray
        {
            public int[] Nums { get; set; }
        }

        public sealed class IntSet
        {
            public HashSet<int> Nums { get; set; }
        }

        private static string RenderList(object model)
        {
            const string text = "@list(Nums){{<li>@()</li>}}";
            var options = new TemplateOptions("allocation-probe") { OutputProfile = OutputProfile.Text };
            var context = new CompileContext(options, new ExType(model.GetType()));
            using (var template = new HeddleTemplate(text, context))
            {
                Assert.True(template.CompileResult.Success,
                    "Probe template failed to compile: " + CompiledFormHarness.Summarize(context) + ".");
                return template.Generate(model);
            }
        }

        [Fact]
        public void ValueTypeCountPathPreservesBytes()
        {
            // List<int> (generic count before, non-generic now), int[] and HashSet<int> all carry a
            // value-type element: every one takes the P3-R9 path, and the bytes must agree throughout.
            var list = RenderList(new IntCollections { Nums = new List<int> { 1, 2, 3 } });
            var array = RenderList(new IntArray { Nums = new[] { 1, 2, 3 } });
            Assert.Equal(array, list);
            var oneList = RenderList(new IntCollections { Nums = new List<int> { 7 } });
            var oneSet = RenderList(new IntSet { Nums = new HashSet<int> { 7 } });
            Assert.Equal(oneList, oneSet);
        }
    }
}
