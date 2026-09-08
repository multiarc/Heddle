using System;
using System.Dynamic;
using System.Threading.Tasks;
using Heddle.Runtime.Parameters;
using Microsoft.CSharp.RuntimeBinder;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Public on purpose: the binder runs in <c>Heddle</c>'s context, so a non-public model type is not
    /// visible to it at all — the same reason the engine's dynamic tier cannot read a foreign internal member.</summary>
    public sealed class DynamicHopModel
    {
        public string Name { get; set; }
        internal string Secret { get; set; }
        public DynamicHopModel Next { get; set; }
    }

    /// <summary>
    /// The dynamic hop chain, through <see cref="DynamicParameter.GetDynamicPropertyChainAccessor"/> — the
    /// binder-context fact survives the 2.x removal: the binder is created for <c>Heddle</c>'s context, so a
    /// consumer-assembly member the engine's dynamic tier cannot see stays unreadable here too. These tests
    /// pin the once-chosen behavior.
    /// </summary>
    public class DynamicMemberTests
    {
        private static object Read(object model, params string[] names) =>
            DynamicParameter.GetDynamicPropertyChainAccessor(names).Compile()(model);

        [Fact]
        public void NullReceiverPropagatesNull()
        {
            Assert.Null(Read(null, "Name"));
        }

        [Fact]
        public void ReadsAPublicMember()
        {
            Assert.Equal("x", Read(new DynamicHopModel { Name = "x" }, "Name"));
        }

        [Fact]
        public void ChainsHopByHop_WithNullPropagationAtEveryStep()
        {
            var model = new DynamicHopModel { Next = new DynamicHopModel { Name = "deep" } };
            Assert.Equal("deep", Read(model, "Next", "Name"));

            var missing = new DynamicHopModel();
            Assert.Null(Read(missing, "Next", "Name"));
        }

        [Fact]
        public void ReadsAnExpandoMember()
        {
            dynamic bag = new ExpandoObject();
            bag.Title = "t";
            Assert.Equal("t", Read(bag, "Title"));
        }

        [Fact]
        public void BindsInHeddlesContext_SoAForeignInternalMemberStaysInvisible()
        {
            // The engine's behavior is normative and the chain reproduces it. Note the deliberate asymmetry
            // this preserves — the *typed* member tier accepts an internal getter regardless of assembly;
            // harmonizing the two is a breaking-window candidate, not a drift fix.
            Assert.Throws<RuntimeBinderException>(
                () => Read(new DynamicHopModel { Secret = "s" }, "Secret"));
        }

        [Fact]
        public void UnknownMemberThrowsTheBindersOwnError()
        {
            Assert.Throws<RuntimeBinderException>(() => Read(new DynamicHopModel(), "Nope"));
        }

        [Fact]
        public void CallSiteCacheIsThreadSafe()
        {
            var model = new DynamicHopModel { Name = "n" };
            var accessor = DynamicParameter.GetDynamicPropertyChainAccessor(new[] { "Name" }).Compile();
            Parallel.For(0, 512, _ => Assert.Equal("n", accessor(model)));
        }

        [Fact]
        public void EmptyHopChainIsAProgrammingError()
        {
            Assert.Throws<ArgumentException>(() =>
                DynamicParameter.GetDynamicPropertyChainAccessor(Array.Empty<string>()));
        }
    }
}
