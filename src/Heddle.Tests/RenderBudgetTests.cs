using System;
using System.IO;
using System.Reflection;
using System.Text;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Heddle.Tests.Streaming;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// C1-R11 — render-budget enforcement, exercised per budget kind × per sink path (string / <see cref="TextWriter"/>
    /// / <c>IBufferWriter&lt;byte&gt;</c>) × breach and just-under-limit, plus the empty-loop deadline, recursion
    /// interplay, and the zero-cost-when-off (null budget) behavior. The wrapper is per-<c>Generate</c> and enforces at
    /// the renderer seam, so all three sinks share one counting site — every sink is expected to behave identically.
    /// </summary>
    public class RenderBudgetTests
    {
        static RenderBudgetTests()
        {
            HeddleTemplate.Configure(typeof(RenderBudgetTests).GetTypeInfo().Assembly);
        }

        public enum SinkKind { String, TextWriter, BufferWriter }

        public static TheoryData<SinkKind> Sinks() =>
            new TheoryData<SinkKind> { SinkKind.String, SinkKind.TextWriter, SinkKind.BufferWriter };

        private static HeddleTemplate Compile(string document, RenderBudget budget)
        {
            var options = new TemplateOptions { RenderBudget = budget };
            var t = new HeddleTemplate(document, new CompileContext(options));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        private static string Render(HeddleTemplate t, SinkKind sink)
        {
            switch (sink)
            {
                case SinkKind.String:
                    return t.Generate(null);
                case SinkKind.TextWriter:
                    var sw = new StringWriter();
                    t.Generate(null, sw);
                    return sw.ToString();
                case SinkKind.BufferWriter:
                    var bw = new TestBufferWriter();
                    t.Generate(null, bw);
                    return Encoding.UTF8.GetString(bw.ToArray());
                default:
                    throw new ArgumentOutOfRangeException(nameof(sink));
            }
        }

        // ---- MaxOutputChars ---------------------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(Sinks))]
        public void OutputChars_Breach_Throws(SinkKind sink)
        {
            var t = Compile("abcdefghij", new RenderBudget { MaxOutputChars = 5 });   // 10 static chars > 5
            var ex = Assert.Throws<TemplateRenderBudgetException>(() => Render(t, sink));
            Assert.Equal(RenderBudgetKind.OutputChars, ex.Kind);
            Assert.Equal(5, ex.Limit);
            Assert.True(ex.Observed > ex.Limit);
        }

        [Theory]
        [MemberData(nameof(Sinks))]
        public void OutputChars_JustUnderLimit_Completes(SinkKind sink)
        {
            // limit == exact length: `chars > limit` is false at equality, so it completes.
            var t = Compile("abcdefghij", new RenderBudget { MaxOutputChars = 10 });
            Assert.Equal("abcdefghij", Render(t, sink));
        }

        // ---- MaxRenderOps -----------------------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(Sinks))]
        public void RenderOps_Breach_Throws(SinkKind sink)
        {
            var t = Compile("@for(10){{x}}", new RenderBudget { MaxRenderOps = 5 });   // 10 write ops > 5
            var ex = Assert.Throws<TemplateRenderBudgetException>(() => Render(t, sink));
            Assert.Equal(RenderBudgetKind.RenderOps, ex.Kind);
            Assert.Equal(5, ex.Limit);
        }

        [Theory]
        [MemberData(nameof(Sinks))]
        public void RenderOps_JustUnderLimit_Completes(SinkKind sink)
        {
            var t = Compile("@for(10){{x}}", new RenderBudget { MaxRenderOps = 1000 });
            Assert.Equal("xxxxxxxxxx", Render(t, sink));
        }

        // ---- MaxRenderTime ----------------------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(Sinks))]
        public void RenderTime_Breach_Throws(SinkKind sink)
        {
            // A zero deadline fires on the first render op on every sink.
            var t = Compile("@for(1000000){{x}}", new RenderBudget { MaxRenderTime = TimeSpan.Zero });
            var ex = Assert.Throws<TemplateRenderBudgetException>(() => Render(t, sink));
            Assert.Equal(RenderBudgetKind.RenderTime, ex.Kind);
        }

        [Theory]
        [MemberData(nameof(Sinks))]
        public void RenderTime_Generous_Completes(SinkKind sink)
        {
            var t = Compile("@for(10){{x}}", new RenderBudget { MaxRenderTime = TimeSpan.FromSeconds(30) });
            Assert.Equal("xxxxxxxxxx", Render(t, sink));
        }

        // ---- Empty-loop deadline backstop (C1-R4) -----------------------------------------------------------

        [Fact]
        public void EmptyLoop_ZeroOutput_TerminatesViaDeadline()
        {
            // A loop whose body writes nothing at runtime: the ops/chars counters never see it, so only the
            // per-iteration deadline can stop it. With only MaxRenderTime set, the breach kind must be RenderTime.
            var t = Compile("@for(100000000){{@if(1>2){{never}}}}",
                new RenderBudget { MaxRenderTime = TimeSpan.Zero });
            var ex = Assert.Throws<TemplateRenderBudgetException>(() => t.Generate(null));
            Assert.Equal(RenderBudgetKind.RenderTime, ex.Kind);
        }

        // ---- Empty-loop deadline under an encode proxy (Obs-1 / C1-R4 universality) --------------------------

        [Fact]
        public void EmptyLoop_NestedInValueExtensionBody_TerminatesViaDeadline()
        {
            // Obs-1: a zero-output loop nested inside a DirectRender value-extension body (@html's encoded body).
            // There, the loop's `scope.Renderer` is the HtmlEncodedRenderer encode proxy, not the BudgetedRenderer.
            // The proxy must forward IBudgetProbe to the inner budgeted renderer so the per-iteration MaxRenderTime
            // backstop still fires — otherwise the empty loop performs no render op and runs unbounded.
            var t = Compile("@html(1){{@for(100000000){{@if(1>2){{never}}}}}}",
                new RenderBudget { MaxRenderTime = TimeSpan.Zero });
            var ex = Assert.Throws<TemplateRenderBudgetException>(() => t.Generate(null));
            Assert.Equal(RenderBudgetKind.RenderTime, ex.Kind);
        }

        [Fact]
        public void EmptyLoop_NestedInValueExtensionBody_NullBudget_Unaffected()
        {
            // The proxy's probe delegation is a no-op when the inner renderer isn't budgeted: with a null budget no
            // wrapper exists, the proxy wraps a plain sink, and the same nested zero-output loop renders empty.
            var t = Compile("@html(1){{@for(5){{@if(1>2){{never}}}}}}", budget: null);
            Assert.Equal(string.Empty, t.Generate(null));
        }

        // ---- Recursion interplay (C1-R11) -------------------------------------------------------------------

        [Fact]
        public void Budget_Fires_IndependentOf_MaxRecursionCount()
        {
            // An unbounded self-recursive definition emitting one char per level. With a very high recursion limit and
            // a small output budget, the budget trips long before the recursion counter would — proving the two limits
            // are independent and the budget is enforced through the definition/recursion path.
            const string doc = "@%\n<loop>\n{{x@loop()}} :: dynamic\n%@\n@loop()";
            var options = new TemplateOptions
            {
                MaxRecursionCount = 1_000_000,
                RenderBudget = new RenderBudget { MaxOutputChars = 10 },
            };
            var t = new HeddleTemplate(doc, new CompileContext(options));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            var ex = Assert.Throws<TemplateRenderBudgetException>(() => t.Generate(null));
            Assert.Equal(RenderBudgetKind.OutputChars, ex.Kind);
        }

        // ---- Zero cost when off (C1-R11 behavioral leg) -----------------------------------------------------

        [Theory]
        [MemberData(nameof(Sinks))]
        public void NullBudget_NeverThrows_AndRendersNormally(SinkKind sink)
        {
            var t = Compile("@for(500){{x}}", budget: null);   // no wrapper installed on the null path
            Assert.Equal(new string('x', 500), Render(t, sink));
        }

        // ---- Exception shape (C1-R5) ------------------------------------------------------------------------

        [Fact]
        public void Exception_Message_MatchesPattern()
        {
            var ex = new TemplateRenderBudgetException(RenderBudgetKind.OutputChars, 5, 10);
            Assert.Equal("Render budget exceeded: OutputChars limit 5, observed 10.", ex.Message);
            Assert.IsAssignableFrom<TemplateProcessingException>(ex);
        }
    }
}
