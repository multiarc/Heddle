using System;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Parameters;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The public local-context channel semantics (phase 3 D3/D6): last-write-wins, null values, null-key
    /// throws, reserved-<c>heddle.</c>-prefix rejection, <see cref="BranchState"/> boxing round-trip,
    /// frameless <c>Publish</c> throw / <c>TryRead</c> false, a publisher/consumer pair end-to-end, the
    /// <c>Scope.Null</c> directive-body row (E14), and the compiled-parameter sandbox shape pin (N7).
    /// </summary>
    public class ScopeChannelTests
    {
        public class FlagModel { public bool A { get; set; } }

        private static HeddleTemplate Compile(string template, ExType modelType = null)
        {
            HeddleTemplate.Configure(typeof(ScopeChannelTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var t = new HeddleTemplate(template, new CompileContext(modelType ?? typeof(object)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        // --- Direct API semantics (frame constructed via the internal ctor, InternalsVisibleTo) ---

        private static Scope FramedScope() =>
            Scope.Null.WithLocals(new ScopeLocals());

        [Fact]
        public void PublishThenTryReadRoundTrips()
        {
            var scope = FramedScope();
            scope.Publish("k", "v");
            Assert.True(scope.TryRead("k", out var value));
            Assert.Equal("v", value);
        }

        [Fact]
        public void LastWriteWins()
        {
            var scope = FramedScope();
            scope.Publish("k", "one");
            scope.Publish("k", "two");
            Assert.True(scope.TryRead("k", out var value));
            Assert.Equal("two", value);
        }

        [Fact]
        public void NullValueIsLegalAndReadsBackTrue()
        {
            var scope = FramedScope();
            scope.Publish("k", null);
            Assert.True(scope.TryRead("k", out var value));
            Assert.Null(value);
        }

        [Fact]
        public void UnknownKeyReadsFalse()
        {
            var scope = FramedScope();
            Assert.False(scope.TryRead("absent", out var value));
            Assert.Null(value);
        }

        [Fact]
        public void NullKeyThrowsOnPublishAndTryRead()
        {
            var scope = FramedScope();
            Assert.Throws<ArgumentNullException>(() => scope.Publish(null, "v"));
            Assert.Throws<ArgumentNullException>(() => scope.TryRead(null, out _));
        }

        [Fact]
        public void ReservedPrefixOtherThanBranchKeyIsRejected()
        {
            var scope = FramedScope();
            Assert.Throws<ArgumentException>(() => scope.Publish("heddle.custom", "v"));
        }

        [Fact]
        public void ReservedBranchKeyRequiresBranchStateValue()
        {
            var scope = FramedScope();
            Assert.Throws<ArgumentException>(() => scope.Publish(BranchState.ReservedKey, "not-a-branch-state"));
        }

        [Fact]
        public void BranchStateBoxingRoundTripThroughPublicRoute()
        {
            var scope = FramedScope();
            scope.Publish(BranchState.ReservedKey, new BranchState(true));
            Assert.True(scope.TryRead(BranchState.ReservedKey, out var value));
            var state = Assert.IsType<BranchState>(value);
            Assert.True(state.Satisfied);
        }

        [Fact]
        public void TryReadUnknownReservedKeyReturnsFalseNeverThrows()
        {
            var scope = FramedScope();
            Assert.False(scope.TryRead("heddle.nonexistent", out var value));
            Assert.Null(value);
        }

        [Fact]
        public void FramelessPublishThrowsInvalidOperationWithRemedy()
        {
            var scope = Scope.Null; // no frame
            var ex = Assert.Throws<InvalidOperationException>(() => scope.Publish("k", "v"));
            Assert.Contains("[ScopeChannel]", ex.Message);
        }

        [Fact]
        public void FramelessTryReadReturnsFalse()
        {
            var scope = Scope.Null;
            Assert.False(scope.TryRead("k", out var value));
            Assert.Null(value);
        }

        // --- End-to-end publisher/consumer pair through a rendered template ---

        [Fact]
        public void PublisherConsumerPairEndToEnd()
        {
            // A driver publishes Satisfied=true; the reader sibling in the same body reads it back.
            var t = Compile("@branchdriver()@branchreader()");
            Assert.Equal("SAT", t.Generate(null));
        }

        [Fact]
        public void ReaderWithoutPublisherRendersNone()
        {
            var t = Compile("@branchreader()");
            Assert.Equal("NONE", t.Generate(null));
        }

        // --- E14: directive bodies execute under Scope.Null and must not throw ---

        [Fact]
        public void DirectiveBodyExecutionUnderScopeNullDoesNotThrow()
        {
            // @using()/@model() read their body once during InitStart against Scope.Null.
            var t = Compile("@using(){{System.Text}}ok");
            Assert.Equal("ok", t.Generate(null));
        }

        // --- N7: the compiled-parameter delegate carries no Scope (sandbox shape) ---

        [Fact]
        public void CompiledParameterDelegateCarriesNoScope()
        {
            var field = typeof(CompiledParameter).GetProperty(nameof(CompiledParameter.ParameterImplementation));
            Assert.NotNull(field);
            Assert.Equal(typeof(Func<object, object, object, object>), field.PropertyType);
        }
    }
}
