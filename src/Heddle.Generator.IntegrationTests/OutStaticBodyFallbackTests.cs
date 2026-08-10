using System;
using System.Linq;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Differential coverage for a built-in <c>@out()</c> that carries its own body. The generator refuses to
    /// precompile a bodied slot projection (<c>TemplateEmitter.BuildSlotProjectionCall</c>:
    /// <c>!string.IsNullOrEmpty(ParameterTemplate)</c> → reason "bodied @out" → <c>null</c>), so any template
    /// containing one <b>falls back to the dynamic tier</b>. Outside a slot-declaring definition the hook accepts
    /// the body, so the arm the refusal covers is the emitter's own, and the byte it named — an emitted body being
    /// a real strategy where the engine's compile of the same static-only text produces no processors at all — is
    /// no longer produced anywhere else: the build emits that post-state for every other bodied call. The refusal
    /// stands ahead of the body build, so it never reaches it. This
    /// pins that documented tier fallback (no precompiled strategy is emitted) — which is what keeps the precompiled and
    /// runtime backends in lockstep for the <c>@out</c> double-render fix (both render through the dynamic engine) — and
    /// asserts the runtime renders the corrected output: because a non-slot <c>@out</c> is a value emitter, a static-only
    /// (no dynamic <c>@</c> content) body is inert, so <c>@out</c> emits the chained value alone, never the chained value
    /// AND the inert static body.
    /// </summary>
    public class OutStaticBodyFallbackTests
    {
        private static string RenderDynamic(string content, string model)
        {
            var t = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), new ExType(typeof(string))));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Theory]
        // @for threads the index on the chained channel; the inert static @out() body must NOT also render (was "[0D][1D][2D]").
        [InlineData("@for(3){{[@out(){{D}}]}}", "x", "[0][1][2]\n")]
        // A top-level static-body @out with no chained value: the inert body is dropped (was "[BODY]").
        [InlineData("[@out(){{BODY}}]", "x", "[]\n")]
        public void BodiedOut_FallsBackToDynamicTier_AndRendersCorrectly(string body, string value, string expected)
        {
            var template = "@model(){{System.String}}@\\\n" + body + "\n";

            var gen = DifferentialHarness.Generate(new[] { ("views/bodied-out.heddle", template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));

            DifferentialHarness.ExpectDegrade(gen, "views/bodied-out.heddle");
            Assert.Equal(expected, RenderDynamic(template, value));
        }
    }
}
