using System;
using System.Dynamic;
using System.Threading.Tasks;
using Heddle.Precompiled;
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
    /// <see cref="PrecompiledRuntime.DynamicMember"/> is the sole implementation to prevent drift between engine
    /// and generated code; they previously inlined <c>(dynamic)</c> casts that bind in the consumer's assembly
    /// context and see internal members the engine's dynamic tier (binding in <c>Heddle</c>'s context) cannot.
    /// These tests pin the once-chosen behavior.
    /// </summary>
    public class DynamicMemberTests
    {
        [Fact]
        public void NullReceiverPropagatesNull()
        {
            Assert.Null(PrecompiledRuntime.DynamicMember(null, "Name"));
        }

        [Fact]
        public void ReadsAPublicMember()
        {
            Assert.Equal("x", PrecompiledRuntime.DynamicMember(new DynamicHopModel { Name = "x" }, "Name"));
        }

        [Fact]
        public void ChainsHopByHop_WithNullPropagationAtEveryStep()
        {
            var model = new DynamicHopModel { Next = new DynamicHopModel { Name = "deep" } };
            var hop = PrecompiledRuntime.DynamicMember(PrecompiledRuntime.DynamicMember(model, "Next"), "Name");
            Assert.Equal("deep", hop);

            var missing = new DynamicHopModel();
            Assert.Null(PrecompiledRuntime.DynamicMember(PrecompiledRuntime.DynamicMember(missing, "Next"), "Name"));
        }

        [Fact]
        public void ReadsAnExpandoMember()
        {
            dynamic bag = new ExpandoObject();
            bag.Title = "t";
            Assert.Equal("t", PrecompiledRuntime.DynamicMember(bag, "Title"));
        }

        [Fact]
        public void BindsInHeddlesContext_SoAForeignInternalMemberStaysInvisible()
        {
            // The engine's behavior is normative and the generator reproduces it. Note the deliberate asymmetry
            // this preserves — the *typed* member tier accepts an internal getter regardless of assembly;
            // harmonizing the two is a breaking-window candidate, not a drift fix.
            Assert.Throws<RuntimeBinderException>(
                () => PrecompiledRuntime.DynamicMember(new DynamicHopModel { Secret = "s" }, "Secret"));
        }

        [Fact]
        public void UnknownMemberThrowsTheBindersOwnError()
        {
            Assert.Throws<RuntimeBinderException>(() => PrecompiledRuntime.DynamicMember(new DynamicHopModel(), "Nope"));
        }

        [Fact]
        public void CallSiteCacheIsThreadSafe()
        {
            var model = new DynamicHopModel { Name = "n" };
            Parallel.For(0, 512, _ => Assert.Equal("n", PrecompiledRuntime.DynamicMember(model, "Name")));
        }

        [Fact]
        public void NullNameIsAProgrammingError()
        {
            Assert.Throws<ArgumentNullException>(() => PrecompiledRuntime.DynamicMember(new DynamicHopModel(), null));
        }
    }
}
