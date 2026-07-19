using System.Reflection;
using Heddle;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Executable doc tests (phase 3 criterion 7): the two worked examples in
    /// <c>docs/custom-extensions.md</c> — the zebra‑striping publisher/consumer pair and the
    /// <see cref="BranchState"/> participant that drives a set — compile and run here so the docs stay honest.
    /// </summary>
    public class ScopeChannelDocExampleTests
    {
        // --- Verbatim from docs/custom-extensions.md ---

        [ExtensionName("zebra")]
        [ScopeChannel]
        public class ZebraExtension : AbstractExtension
        {
            private const string Key = "myapp.zebra.row";

            public override object ProcessData(in Scope scope) => Next(scope);
            public override void RenderData(in Scope scope) => scope.Renderer.Render(Next(scope));

            private static string Next(in Scope scope)
            {
                bool odd = scope.TryRead(Key, out var value) && value is bool b && b;
                scope.Publish(Key, !odd);       // flip for the next sibling
                return odd ? "odd" : "even";
            }
        }

        [ExtensionName("satisfy")]
        [ScopeChannel]
        public class SatisfyExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope)
            {
                scope.Publish(BranchState.ReservedKey, new BranchState(true));
                return string.Empty;
            }

            public override void RenderData(in Scope scope)
            {
                scope.Publish(BranchState.ReservedKey, new BranchState(true));
            }
        }

        private static readonly object Gate = new object();
        private static bool _registered;

        private static HeddleTemplate Compile(string template)
        {
            HeddleTemplate.Configure(typeof(ScopeChannelDocExampleTests).GetTypeInfo().Assembly);
            lock (Gate)
            {
                if (!_registered)
                {
                    if (!TemplateFactory.Exists("zebra"))
                        TemplateFactory.AddExtensions(new[] { new ExtensionType("zebra", typeof(ZebraExtension), false) });
                    if (!TemplateFactory.Exists("satisfy"))
                        TemplateFactory.AddExtensions(new[] { new ExtensionType("satisfy", typeof(SatisfyExtension), false) });
                    _registered = true;
                }
            }

            var t = new HeddleTemplate(template, new CompileContext(typeof(object)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        [Fact]
        public void ZebraStripingAlternatesAcrossSiblings()
        {
            Assert.Equal("evenoddeven", Compile("@zebra()@zebra()@zebra()").Generate(null));
        }

        [Fact]
        public void SatisfyParticipantMakesFollowingElseSilent()
        {
            Assert.Equal("", Compile("@satisfy()@else(){{ fallback }}").Generate(null));
        }
    }
}
