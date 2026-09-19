using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// A model whose getter is observable. Nothing forbids a template from reading a property that logs, lazily
    /// materializes, or hits a database, so the number of times the engine calls one is part of what a member path
    /// means — not an implementation detail.
    /// </summary>
    public sealed class CountingLink
    {
        public static int Reads;

        private CountingLink _next;
        private int _value;

        public CountingLink Next
        {
            get
            {
                Reads++;
                return _next;
            }
            set => _next = value;
        }

        public int Value
        {
            get
            {
                Reads++;
                return _value;
            }
            set => _value = value;
        }

        public static CountingLink Chain(int length)
        {
            var head = new CountingLink { Value = 42 };
            for (int i = 0; i < length; i++)
                head = new CountingLink { _next = head, Value = 42 };
            return head;
        }
    }

    /// <summary>
    /// A dynamic model that reports how many member reads the binder actually performed.
    /// </summary>
    public sealed class CountingBag : DynamicObject
    {
        public static int Reads;

        private readonly Dictionary<string, object> _members = new Dictionary<string, object>();

        public void Set(string name, object value) => _members[name] = value;

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            Reads++;
            return _members.TryGetValue(binder.Name, out result);
        }

        public static CountingBag Chain(int length)
        {
            var head = new CountingBag();
            head.Set("Value", 42);
            for (int i = 0; i < length; i++)
            {
                var next = new CountingBag();
                next.Set("Next", head);
                next.Set("Value", 42);
                head = next;
            }

            return head;
        }
    }

    /// <summary>
    /// A null-safe hop tests its receiver and then reads through it. Building that as two copies of the same
    /// sub-expression doubles the work per hop, so an <c>n</c>-hop path evaluated <c>2ⁿ−1</c> getters and produced a
    /// tree of the same order — deep paths took seconds to compile and past seventeen hops the runtime refused the
    /// method outright. The generated tier hops once per segment, so the two tiers also disagreed on how many times
    /// a template read the model.
    /// <para>These tests assert the property — cost grows with the number of hops, not with two raised to it — and
    /// deliberately not the shape of the tree that delivers it.</para>
    /// </summary>
    public class NullSafeHopEvaluationTests
    {
        private static string RenderTyped(string template, object model)
        {
            HeddleTemplate.Configure(typeof(NullSafeHopEvaluationTests).GetTypeInfo().Assembly);
            using var target = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), model.GetType()));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            return target.Generate(model);
        }

        private static string RenderDynamic(string template, object model)
        {
            HeddleTemplate.Configure(typeof(NullSafeHopEvaluationTests).GetTypeInfo().Assembly);
            using var target = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), ExType.Dynamic));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            return target.Generate(model);
        }

        private static string Path(string leaf, int hops)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < hops; i++)
                sb.Append("Next.");
            return sb.Append(leaf).ToString();
        }

        /// <summary>The member tier: one getter call per segment, however deep the path.</summary>
        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(16)]
        public void TheMemberTierReadsEachHopExactlyOnce(int hops)
        {
            var model = CountingLink.Chain(hops);
            CountingLink.Reads = 0;
            Assert.Equal("42", RenderTyped("@(" + Path("Value", hops) + ")", model));
            Assert.Equal(hops + 1, CountingLink.Reads);
        }

        /// <summary>The native-expression tier resolves the same paths and must charge the same.</summary>
        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(16)]
        public void TheNativeExpressionTierReadsEachHopExactlyOnce(int hops)
        {
            var model = CountingLink.Chain(hops);
            CountingLink.Reads = 0;
            Assert.Equal("42", RenderTyped("@(" + Path("Value", hops) + " + 0)", model));
            Assert.Equal(hops + 1, CountingLink.Reads);
        }

        /// <summary>The dynamic tier binds a call site per hop; duplicating the receiver duplicated those too.</summary>
        [Theory]
        [InlineData(4)]
        [InlineData(10)]
        [InlineData(16)]
        public void TheDynamicTierReadsEachHopExactlyOnce(int hops)
        {
            var model = CountingBag.Chain(hops);
            CountingBag.Reads = 0;
            Assert.Equal("42", RenderDynamic("@(" + Path("Value", hops) + ")", model));
            Assert.Equal(hops + 1, CountingBag.Reads);
        }

        /// <summary>
        /// Indexers sit on the same null-safe hop shape, and an index expression can be as costly to evaluate as
        /// any other receiver.
        /// </summary>
        [Fact]
        public void AnIndexedHopEvaluatesItsReceiverOnce()
        {
            var model = new IndexRoot { Items = new[] { new IndexRoot.Cell { Amount = 7 } } };
            IndexRoot.Reads = 0;
            Assert.Equal("7", RenderTyped("@(Counted[0].Amount)", model));
            Assert.Equal(1, IndexRoot.Reads);
        }

        public sealed class IndexRoot
        {
            public static int Reads;

            public Cell[] Items { get; set; }

            public Cell[] Counted
            {
                get
                {
                    Reads++;
                    return Items;
                }
            }

            public sealed class Cell
            {
                public int Amount { get; set; }
            }
        }

        /// <summary>
        /// The compiled tree has to stay proportional to the path as well. A tree that doubled per hop stopped
        /// being compilable at all around seventeen segments — the runtime rejected the emitted method — so a path
        /// no deeper than a namespace could take a template down.
        /// </summary>
        [Fact]
        public void ADeepPathStillCompiles()
        {
            const int hops = 18;
            var model = CountingLink.Chain(hops);
            CountingLink.Reads = 0;
            Assert.Equal("42", RenderTyped("@(" + Path("Value", hops) + ")", model));
            Assert.Equal(hops + 1, CountingLink.Reads);
        }

        /// <summary>
        /// Node count is the compile-time face of the same property: the accessor for a path must grow by a fixed
        /// amount per hop. This counts nodes rather than timing the compile so the bound means the same thing on a
        /// loaded machine as on an idle one.
        /// </summary>
        [Fact]
        public void TheAccessorTreeGrowsLinearlyWithPathLength()
        {
            Assert.True(NodeCount(12) - NodeCount(10) == NodeCount(10) - NodeCount(8),
                $"expected a constant node cost per hop, got {NodeCount(8)}, {NodeCount(10)}, {NodeCount(12)}");
        }

        private static int NodeCount(int hops)
        {
            var accessor = DynamicParameterAccessor(hops);
            var counter = new NodeCounter();
            counter.Visit(accessor);
            return counter.Count;
        }

        private static Expression DynamicParameterAccessor(int hops)
        {
            var property = typeof(CountingLink).GetProperty(nameof(CountingLink.Next));
            var chain = new List<(Type, PropertyInfo)>();
            for (int i = 0; i < hops; i++)
                chain.Add((typeof(CountingLink), property));
            var method = typeof(HeddleTemplate).GetTypeInfo().Assembly
                .GetType("Heddle.Runtime.Parameters.ModelParameter")
                .GetMethod("GetPropertyChainAccessor", BindingFlags.Static | BindingFlags.NonPublic);
            return (Expression)method.Invoke(null, new object[] { chain });
        }

        private sealed class NodeCounter : ExpressionVisitor
        {
            public int Count;

            public override Expression Visit(Expression node)
            {
                if (node != null)
                    Count++;
                return base.Visit(node);
            }
        }
    }
}
