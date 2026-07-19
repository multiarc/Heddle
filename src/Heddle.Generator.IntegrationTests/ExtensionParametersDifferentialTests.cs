using System.Linq;
using Heddle;
using Heddle.Generator.IntegrationTests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 8 (WI9) — extension-parameter differentials: a bodiless parameter-declaring custom extension binds
    /// its <c>[Prop]</c> layout at build time (frozen prototype + dynamic setters via
    /// <c>PrecompiledRuntime.BindExtension</c>) and renders byte-identically with the dynamic backend; the
    /// encode-attribute alignment (F6/H1 parameterized, P8-J-E1/D8/WI5b plain custom) keeps both tiers
    /// self-encoding; malformed <c>[Prop]</c> declarations draw <c>HED7017</c>; and the F7 guard keeps the
    /// precompiled tier from silently dropping named args the dynamic tier rejects (HED5005).
    /// </summary>
    public class ExtensionParametersDifferentialTests
    {
        [Fact]
        public void ConstantArgumentBindsAtBuildAndRendersIdentically()
        {
            var t = "@model(){{System.String}}@\\\n<x>@grid(this, columns: 4)</x>\n";
            var (pre, dyn) = DifferentialHarness.Render("views/gridconst.heddle", t, typeof(string), "photos");
            Assert.Equal(dyn, pre);
            Assert.Contains("cols=4:photos", dyn);
        }

        [Fact]
        public void DynamicArgumentBindsThroughSetterAndRendersIdentically()
        {
            var t = "@model(){{Heddle.Generator.IntegrationTests.Fixtures.GridModel}}@\\\n@grid(Name, columns: Cols)\n";
            var model = new GridModel { Name = "photos", Cols = 7 };
            var (pre, dyn) = DifferentialHarness.Render("views/griddyn.heddle", t, typeof(GridModel), model);
            Assert.Equal(dyn, pre);
            Assert.Contains("cols=7:photos", dyn);
        }

        [Fact]
        public void AllDefaultsRenderIdentically()
        {
            var t = "@model(){{System.String}}@\\\n@grid(this)\n";
            var (pre, dyn) = DifferentialHarness.Render("views/griddef.heddle", t, typeof(string), "photos");
            Assert.Equal(dyn, pre);
            Assert.Contains("cols=3:photos", dyn);
        }

        [Fact]
        public void BodiedParameterCallFallsBackAndDynamicRendersParameters()
        {
            // A bodied custom call keeps the existing dynamic-tier fallback (D7): the generator emits no entry
            // class for the template, and the dynamic tier renders it — parameters and all.
            var t = "@model(){{System.String}}@\\\n@grid(this, columns: 4){{body}}\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/gridbodied.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Empty(gen.TemplateSources);   // degraded — no .g.cs

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            Assert.Contains("cols=4:photos", dynamicTemplate.Generate("photos"));
        }

        [Fact]
        public void EncodedGridCrossTierSelfEncodesIdentically()
        {
            // F6/H1: [EncodeOutput] + [Prop] — the precompiled inner self-encodes because
            // AllocateParameterizedExtension emits RenderType.Encode and BindExtension applies it to the inner.
            var t = "@model(){{System.String}}@\\\n@encodedGrid(this, columns: 4)\n";
            var (pre, dyn) = DifferentialHarness.Render("views/encgrid.heddle", t, typeof(string), "a&b");
            Assert.Equal(dyn, pre);
            Assert.Contains("&lt;grid cols=4&gt;a&amp;b&lt;/grid&gt;", dyn);   // encoded — never raw markup
            Assert.DoesNotContain("<grid", dyn);
        }

        [Fact]
        public void EncodedBareCrossTierAlignsPlainCustomRenderType()
        {
            // P8-J-E1 / D8 / WI5b: the plain (no-parameter) custom path derives Encode from [EncodeOutput]
            // instead of hard-coding Raw. Against the pre-fix hard-coded Raw the precompiled tier would emit the
            // markup un-encoded while the dynamic tier encodes — this row would FAIL.
            var t = "@model(){{System.String}}@\\\n@encodedBare(this)\n";

            // Non-vacuity: the precompiled path must actually be taken — the generated source binds the site
            // through PrecompiledRuntime.Bind with the DERIVED RenderType.Encode (no dynamic fallback).
            var gen = DifferentialHarness.Generate(new[] { ("views/encbare.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("PrecompiledRuntime.Bind(", source);
            Assert.Contains("EncodedBareExtension", source);
            Assert.Contains("global::Heddle.Data.RenderType.Encode", source);

            var (pre, dyn) = DifferentialHarness.Render("views/encbare.heddle", t, typeof(string), "x&y");
            Assert.Equal(dyn, pre);
            Assert.Contains("&lt;b&gt;&amp;x&amp;y&lt;/b&gt;", dyn);   // both tiers HTML-encode
        }

        [Fact]
        public void PlainCustomWithoutEncodeOutputStaysRaw()
        {
            // The derivation touches only the [EncodeOutput] case: a plain custom extension without it still
            // emits Raw, byte-identical to today.
            var t = "@model(){{System.String}}@\\\n@yell(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/yellraw.heddle", t) });
            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("global::Heddle.Data.RenderType.Raw", source);

            var (pre, dyn) = DifferentialHarness.Render("views/yellraw.heddle", t, typeof(string), "<b>");
            Assert.Equal(dyn, pre);
            Assert.Contains("<B>!", dyn);   // raw — unencoded
        }

        [Theory]
        [InlineData("malformedDup", "duplicate parameter name 'a'")]
        [InlineData("malformedReserved", "reserved parameter name 'out'")]
        [InlineData("malformedNullName", "parameter name is null or empty")]
        [InlineData("malformedDefault", "default value for 'a' is not convertible")]
        [InlineData("malformedType", "unusable type")]
        [InlineData("wideningItem", "re-declared parameter 'item' widens the inherited type")]
        public void MalformedPropDeclarationReportsHed7017(string name, string faultFragment)
        {
            var t = "@model(){{System.String}}@\\\n@" + name + "(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/" + name + ".heddle", t) });
            var hed7017 = gen.Diagnostics.FirstOrDefault(d => d.Id == "HED7017");
            Assert.NotEqual(default, hed7017);
            Assert.Equal(DiagnosticSeverity.Error, hed7017.Severity);
            Assert.Contains(name, hed7017.GetMessage());
            Assert.Contains(faultFragment, hed7017.GetMessage());
        }

        [Theory]
        [InlineData("nullableIface")]   // IComparable <- int?: Roslyn boxing, runtime NOT assignable — must error
        [InlineData("nullableWiden")]   // int <- int?: not assignable — must error
        public void NullableRedeclarationTwinRejectsExactlyAsRuntime(string name)
        {
            var t = "@model(){{System.String}}@\\\n@" + name + "(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/" + name + ".heddle", t) });
            Assert.Contains(gen.Diagnostics, d => d.Id == "HED7017");
        }

        [Fact]
        public void NullableNarrowingAcceptsAndRendersIdentically()
        {
            // int? <- int: reflection's underlying-value rule accepts (rule (A)) — the twin must NOT false-error.
            var t = "@model(){{System.String}}@\\\n@nullableNarrow(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/nullnarrow.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7017");

            var (pre, dyn) = DifferentialHarness.Render("views/nullnarrow.heddle", t, typeof(string), "z");
            Assert.Equal(dyn, pre);
            Assert.Contains("n=5:z", dyn);
        }

        [Fact]
        public void NamedArgsOnParameterLessExtensionDegradeToDynamicHed5005()
        {
            // F7: the generator must NOT silently drop the named args (it degrades — no entry class), and the
            // dynamic tier raises HED5005 for the same call — one verdict governs both tiers.
            var t = "@model(){{System.String}}@\\\n@yell(this, p: 1)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/yellnamed.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Empty(gen.TemplateSources);   // degraded — the named args were not bound away

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains(dynamicTemplate.CompileResult.ErrorList, e => e.DiagnosticId == "HED5005");
        }
    }
}
