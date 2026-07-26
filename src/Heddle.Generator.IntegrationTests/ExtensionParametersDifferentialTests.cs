using System.Linq;
using Heddle;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Parameter-declaring custom extensions render byte-identically between precompiled and dynamic tiers.
    /// Tests encode-attribute alignment, HED7017 for malformed [Prop], and guard against dropped named args.
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
            // Bodied custom call: generator emits no entry class; dynamic tier renders with parameters.
            var t = "@model(){{System.String}}@\\\n@grid(this, columns: 4){{body}}\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/gridbodied.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Empty(gen.TemplateSources);   // degraded — no .g.cs
            DifferentialHarness.ExpectDegrade(gen, "views/gridbodied.heddle");   // Declared fallback intent

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            Assert.Contains("cols=4:photos", dynamicTemplate.Generate("photos"));
        }

        [Fact]
        public void EncodedGridCrossTierSelfEncodesIdentically()
        {
            // [EncodeOutput] + [Prop]: precompiled inner self-encodes via AllocateParameterizedExtension and BindExtension.
            var t = "@model(){{System.String}}@\\\n@encodedGrid(this, columns: 4)\n";
            var (pre, dyn) = DifferentialHarness.Render("views/encgrid.heddle", t, typeof(string), "a&b");
            Assert.Equal(dyn, pre);
            Assert.Contains("&lt;grid cols=4&gt;a&amp;b&lt;/grid&gt;", dyn);   // encoded — never raw markup
            Assert.DoesNotContain("<grid", dyn);
        }

        [Fact]
        public void EncodedBareCrossTierAlignsPlainCustomRenderType()
        {
            // Derives Encode from [EncodeOutput] instead of hard-coding Raw; pre-fix would fail (tier divergence).
            var t = "@model(){{System.String}}@\\\n@encodedBare(this)\n";

            // Non-vacuity: generated source binds via PrecompiledRuntime.Bind with derived RenderType.Encode (no fallback).
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
            // Derivation touches only [EncodeOutput]; plain custom without it stays Raw (byte-identical).
            var t = "@model(){{System.String}}@\\\n@yell(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/yellraw.heddle", t) });
            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("global::Heddle.Data.RenderType.Raw", source);

            var (pre, dyn) = DifferentialHarness.Render("views/yellraw.heddle", t, typeof(string), "<b>");
            Assert.Equal(dyn, pre);
            Assert.Contains("<B>!", dyn);   // raw — unencoded
        }

        /// <summary>
        /// Malformed-[Prop] sentences produced once and quoted by both tiers: HED7017 (build), HED5007-5015 (dynamic).
        /// Cross-tier testing pinned by PropLayoutCore lockstep.
        /// </summary>
        [Theory]
        [InlineData("malformedDup", "Prop 'a' is declared more than once on extension 'malformedDup'.")]
        [InlineData("malformedReserved", "'out' is reserved and cannot be used as a prop name.")]
        [InlineData("malformedNullName",
            "A [Prop] parameter name on extension 'malformedNullName' is null or empty.")]
        [InlineData("malformedDefault",
            "The default value for prop 'a' (String) is not convertible to System.Int32.")]
        [InlineData("malformedType", "Cannot resolve type for prop 'a' of extension 'malformedType'.")]
        [InlineData("wideningItem",
            "Prop 'item' is re-declared with type System.Object, which is not assignable to the inherited type " +
            "System.String.")]
        public void MalformedPropDeclarationReportsHed7017(string name, string sentence)
        {
            var t = "@model(){{System.String}}@\\\n@" + name + "(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/" + name + ".heddle", t) });
            var hed7017 = gen.Diagnostics.FirstOrDefault(d => d.Id == "HED7017");
            Assert.NotEqual(default, hed7017);
            Assert.Equal(DiagnosticSeverity.Error, hed7017.Severity);
            Assert.Contains(name, hed7017.GetMessage());
            Assert.Contains(sentence, hed7017.GetMessage());
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
            // int? <- int: reflection's rule accepts; twin must NOT false-error.
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
            // Generator must NOT drop named args (degrades); dynamic tier raises HED5005 — one verdict governs both.
            var t = "@model(){{System.String}}@\\\n@yell(this, p: 1)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/yellnamed.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Empty(gen.TemplateSources);   // degraded — the named args were not bound away
            DifferentialHarness.ExpectDegrade(gen, "views/yellnamed.heddle");   // Declared fallback intent

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains(dynamicTemplate.CompileResult.ErrorList, e => e.DiagnosticId == "HED5005");
        }
    }
}
