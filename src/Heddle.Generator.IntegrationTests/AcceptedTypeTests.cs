using System;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The accepted-type gate, over host extensions that declare one. The engine asks
    /// <c>Type.IsAssignableFrom</c> — the CLR relation, which has generic covariance, array covariance and the
    /// CLR's own <c>Nullable&lt;T&gt;</c> treatment in it — and refuses the whole template when the call site's
    /// value fits none of the declared types.
    /// <para>Reproducing that relation as nominal identity over the base chain and the interface set answers
    /// "no" to every row below that is not an exact match, which takes a working template off the precompiled
    /// tier for nothing. The two halves are here together: every accepted row renders the engine's bytes out of
    /// generated code, and every refused row degrades on this side and is a compile error on the other. A gate
    /// that answered "yes" to everything would fail the second half; one that answered "no" to everything would
    /// fail the first.</para>
    /// </summary>
    public class AcceptedTypeTests
    {
        private const string ModelType = "Heddle.Generator.IntegrationTests.Fixtures.AcceptanceModel";

        private static AcceptanceModel Model() => new AcceptanceModel
        {
            I = 7,
            Maybe = 7,
            L = 7L,
            S = "abc",
            D = 1.5m,
            Strs = new System.Collections.Generic.List<string> { "a", "b" },
            IStrs = new System.Collections.Generic.List<string> { "a", "b" },
            StrArr = new[] { "a", "b" },
            Ints = new[] { 1, 2 },
            UInts = new uint[] { 1, 2 },
            Days = new[] { DayOfWeek.Monday },
            Longs = new[] { 1L },
            IntList = new System.Collections.Generic.List<int> { 1, 2 }
        };

        private static string Template(string call) =>
            "@model(){{" + ModelType + "}}@\\\n[@" + call + "]\n";

        /// <summary>Every relation the CLR admits between a declared accepted type and a call-site value, each
        /// with the marker the reached extension renders.</summary>
        [Theory]
        [InlineData("takesni(I)", "[ni]\n")]              // int into int?  — the CLR's Nullable<T> assignability
        [InlineData("takesni(Maybe)", "[ni]\n")]          // int? into int? — after the engine unwraps the value
        [InlineData("takesseqobj(Strs)", "[so]\n")]       // List<string>  → IEnumerable<object>, covariance
        [InlineData("takesseqobj(IStrs)", "[so]\n")]      // IList<string> → IEnumerable<object>, covariance
        [InlineData("takesseqobj(StrArr)", "[so]\n")]     // string[]      → IEnumerable<object>
        [InlineData("takesobjarr(StrArr)", "[oa]\n")]     // string[]      → object[], array covariance
        [InlineData("takesintarr(Ints)", "[ia]\n")]       // int[]         → int[], identity
        [InlineData("takesintarr(UInts)", "[ia]\n")]      // uint[]        → int[], the CLR's reduced element type
        [InlineData("takesintarr(Days)", "[ia]\n")]       // DayOfWeek[]   → int[], an enum over that element
        [InlineData("takesstrorint(S)", "[si]\n")]        // the base declaration's string
        [InlineData("takesstrorint(I)", "[si]\n")]        // the subclass's own int
        public void AValueTheEngineAcceptsPrecompilesAndRendersTheEnginesBytes(string call, string expected)
        {
            var template = Template(call);
            var key = "views/accept-" + call.Replace("(", "-").Replace(")", "").ToLowerInvariant() + ".heddle";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(AcceptanceModel), Model());
            Assert.Equal(expected, dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The other direction. Each of these is a relation the CLR does <b>not</b> admit — a widening,
        /// an unrelated type, variance over a value-type argument, array covariance over a value-type element —
        /// so the engine refuses the template and the emitter must not precompile it.</summary>
        [Theory]
        [InlineData("takesni(S)", "System.String")]
        [InlineData("takesni(L)", "System.Int64")]           // no numeric widening in the CLR relation
        [InlineData("takesseqobj(IntList)", "System.Collections.Generic.List`1[[System.Int32")]
        [InlineData("takesobjarr(Ints)", "System.Int32[]")]
        [InlineData("takesintarr(Longs)", "System.Int64[]")]   // a wider element is not a reduced one
        [InlineData("takesintarr(StrArr)", "System.String[]")] // nor is a reference element
        [InlineData("takesstrorint(D)", "System.Decimal")]
        public void AValueTheEngineRefusesDegradesRatherThanPrecompiling(string call, string valueTypeName)
        {
            var template = Template(call);
            var key = "views/refuse-" + call.Replace("(", "-").Replace(")", "").ToLowerInvariant() + ".heddle";

            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);

            var dynamicTemplate = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), typeof(AcceptanceModel)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            var text = dynamicTemplate.CompileResult.ToString();
            Assert.Contains(HeddleDiagnosticIds.ReturnTypeMismatch, text, StringComparison.Ordinal);
            Assert.Contains("Return Type is " + valueTypeName, text, StringComparison.Ordinal);
        }

        /// <summary>The inheritance walk on its own. <c>takesstrorint</c> declares <c>int</c> and derives from a
        /// base declaring <c>string</c>; the runtime reads the attribute with <c>inherit: true</c>, so both are
        /// accepted and a third type is not. Reading only the extension type's own attributes would refuse the
        /// <c>string</c> row while leaving the other two exactly as they are.</summary>
        [Fact]
        public void AnInheritedAcceptedTypeIsAcceptedAlongsideTheDeclaredOne()
        {
            var model = Model();

            var inherited = DifferentialHarness.Render("views/inherit-string.heddle",
                Template("takesstrorint(S)"), typeof(AcceptanceModel), model);
            Assert.Equal("[si]\n", inherited.dynamic);
            Assert.Equal(inherited.dynamic, inherited.precompiled);

            var own = DifferentialHarness.Render("views/inherit-int.heddle",
                Template("takesstrorint(I)"), typeof(AcceptanceModel), model);
            Assert.Equal("[si]\n", own.dynamic);
            Assert.Equal(own.dynamic, own.precompiled);

            const string key = "views/inherit-neither.heddle";
            var neither = Template("takesstrorint(D)");
            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, neither) }), key);
        }
    }
}
