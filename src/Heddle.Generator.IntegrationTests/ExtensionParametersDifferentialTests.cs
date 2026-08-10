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

        /// <summary>A bodied call to a parameter-declaring custom extension precompiles: its own hook runs at
        /// static-init through <c>InitExtension</c>, which wraps the initialized extension in the parameter carrier
        /// afterwards — the order the engine's compiler uses — so the body and the props both arrive.</summary>
        [Fact]
        public void BodiedParameterCallPrecompilesAndRendersParameters()
        {
            var t = "@model(){{System.String}}@\\\n@grid(this, columns: 4){{body}}\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/gridbodied.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, "views/gridbodied.heddle");
            Assert.Contains("PrecompiledRuntime.InitExtension(", Assert.Single(gen.TemplateSources).Value);

            var (pre, dyn) = DifferentialHarness.Render("views/gridbodied.heddle", t, typeof(string), "photos");
            Assert.Equal(dyn, pre);
            Assert.Contains("cols=4:photos", pre);
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

            // Non-vacuity: the site runs the extension's own hook, which derives Encode off the live type — no
            // render type is written into the generated file at all.
            var gen = DifferentialHarness.Generate(new[] { ("views/encbare.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("PrecompiledRuntime.Init(", source);
            Assert.Contains("EncodedBareExtension", source);

            var (pre, dyn) = DifferentialHarness.Render("views/encbare.heddle", t, typeof(string), "x&y");
            Assert.Equal(dyn, pre);
            Assert.Contains("&lt;b&gt;&amp;x&amp;y&lt;/b&gt;", dyn);   // both tiers HTML-encode
        }

        [Fact]
        public void PlainCustomWithoutEncodeOutputStaysRaw()
        {
            // Derivation touches only [EncodeOutput]; a plain custom without it stays Raw. The derivation now runs
            // at static-init off the live type, so the proof is the rendered bytes plus the seam being taken.
            var t = "@model(){{System.String}}@\\\n@yell(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/yellraw.heddle", t) });
            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("PrecompiledRuntime.Init(", source);

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
        [InlineData("nullableEnum")]    // Enum <- DayOfWeek?: the same, with a class target rather than an interface
        public void NullableRedeclarationTwinRejectsExactlyAsRuntime(string name)
        {
            var t = "@model(){{System.String}}@\\\n@" + name + "(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/" + name + ".heddle", t) });
            Assert.Contains(gen.Diagnostics, d => d.Id == "HED7017");

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains(dynamicTemplate.CompileResult.ErrorList, e => e.DiagnosticId == "HED5008");
        }

        /// <summary>The neighbour of the <c>nullableEnum</c> row: the same <c>DayOfWeek?</c> re-declaration
        /// against <c>ValueType</c>, which is on <c>Nullable&lt;T&gt;</c>'s own base chain. The layout is clean
        /// and the call renders the engine's bytes out of generated code, so the exclusion above is a rule about
        /// which targets a nullable reaches and not a refusal of nullable re-declarations.</summary>
        [Fact]
        public void ANullableRedeclarationOnTheNullableBaseChainStillAcceptsAndRendersIdentically()
        {
            const string t = "@model(){{System.String}}@\\\n@nullableValueType(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/nullvaluetype.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7017");

            var (pre, dyn) = DifferentialHarness.Render("views/nullvaluetype.heddle", t, typeof(string), "z");
            Assert.Equal(dyn, pre);
            Assert.Contains("v=none:z", dyn);
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

        // ---- Prop defaults whose CLR type the value alone does not give away ----
        //
        // The runtime reads a [Prop] default through reflection and boxes whatever the attribute holds; the emitter
        // reads the same declaration out of metadata, where an enum constant is represented by its underlying
        // primitive. Writing that primitive into the frozen prototype stores a differently-typed box, which the
        // rendered text gives away the moment anything formats or types the value.

        [Theory]
        [InlineData("enumDefault", "day=Tuesday/DayOfWeek")]
        [InlineData("enumZeroDefault", "day=Sunday/DayOfWeek")]
        [InlineData("byteEnumDefault", "day=High/Rung")]
        [InlineData("nullableEnumDefault", "day=Friday/DayOfWeek")]
        [InlineData("objectEnumDefault", "day=Tuesday/DayOfWeek")]
        public void AnEnumPropDefaultPrecompilesAsTheEnumItAndNotItsUnderlyingPrimitive(string extension,
            string expected)
        {
            var t = "@model(){{System.String}}@\\\n@" + extension + "(this)\n";
            var key = "views/enumdefault-" + extension + ".heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, t, typeof(string), "z");
            Assert.Equal(expected, dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        /// <summary>The near neighbour that must keep being refused, and by both tiers: an <c>int</c> default
        /// against an enum-typed prop is not convertible, so the enum rows above are about the default's type and
        /// not about relaxing the conversion.</summary>
        [Fact]
        public void AnIntDefaultOnAnEnumPropStaysRefusedOnBothTiers()
        {
            const string t = "@model(){{System.String}}@\\\n@enumIntDefault(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/enumintdefault.heddle", t) });
            Assert.Contains(gen.Diagnostics, d => d.Id == "HED7017");
            DifferentialHarness.ExpectDegrade(gen, "views/enumintdefault.heddle");

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains(dynamicTemplate.CompileResult.ErrorList, e => e.DiagnosticId == "HED5009");
        }

        /// <summary>An enum default is reproduced by writing the enum's name, so one this assembly keeps internal
        /// has to cost the precompiled tier rather than a generated file the consumer's build rejects.</summary>
        [Fact]
        public void AnEnumDefaultThisAssemblyCannotNameDegrades()
        {
            const string t = "@model(){{System.String}}@\\\n@internalEnumDefault(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/internalenumdefault.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, "views/internalenumdefault.heddle");

            var dynamicTemplate = new HeddleTemplate(t,
                new Heddle.Runtime.CompileContext(new Heddle.Data.TemplateOptions(), typeof(string)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            Assert.Equal("day=One/InternalRung", dynamicTemplate.Generate("z").Trim());
        }

        /// <summary>The four integral types C# gives no literal suffix. Each prop is its default's own type, so
        /// the prototype holds it unconverted and the box has to carry that exact width and signedness.
        /// <para>The tier comparison is asserted first because it is the one this test exists to make; it used to
        /// sit behind the value assertion, which spelled its numbers invariantly and therefore failed ahead of it
        /// under any culture with a non-ASCII negative sign. The value assertion now builds its expectation from
        /// C#-typed literals under the same ambient culture the render used, so it still pins each default's value,
        /// width and signedness without also pinning the host's regional settings — and the differential keeps
        /// running under whatever culture the host has, which is the one thing only it can observe.</para></summary>
        [Fact]
        public void NarrowIntegralPropDefaultsPrecompileWithTheirOwnBoxedTypes()
        {
            const string t = "@model(){{System.String}}@\\\n@narrowDefaults(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/narrowdefaults.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, "views/narrowdefaults.heddle");

            var (pre, dyn) = DifferentialHarness.Render("views/narrowdefaults.heddle", t, typeof(string), "z");
            Assert.Equal(dyn, pre);
            var expected = $"b={(byte)5}/Byte;sb={(sbyte)-5}/SByte;s={(short)-300}/Int16;us={(ushort)400}/UInt16";
            Assert.Equal(expected, dyn.Trim());
        }

        /// <summary>
        /// Prop names carrying the characters a C# string literal escapes. The ordered name array the generated
        /// <c>BindExtension</c> call site hands the runtime was the one literal site in the emitter that spelled the
        /// escape by hand, and it covered the quote only: a name containing <c>\b</c> compiled cleanly and decoded
        /// to a different name, so the runtime's name→index map bound the wrong slot with nothing reported anywhere.
        /// </summary>
        [Theory]
        // A backslash written through unescaped is still legal C#, and the emitted name decodes to something else:
        // the row that reads the wrong slot with nothing reported on either side.
        [InlineData("escapedNames", "views/escapednames.heddle", "1,2")]
        // A newline and a trailing backslash end the literal where it stands and stop the consumer's build.
        [InlineData("unspellableNames", "views/unspellablenames.heddle", "3,4")]
        public void PropNamesNeedingEscapesSurviveIntoTheNameIndexMap(string extension, string key, string expected)
        {
            var t = "@model(){{System.String}}@\\\n@" + extension + "(this)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, t, typeof(string), "z");
            Assert.Equal(expected, dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        /// <summary>
        /// A real default C# has no literal for. <c>G17</c> spells an infinity <c>Infinity</c>, and the emitter
        /// appended the <c>D</c> suffix to it and wrote <c>InfinityD</c> into the generated file — <c>CS0103</c> in
        /// the consumer's build, with the manifest still claiming the template had precompiled.
        /// </summary>
        [Theory]
        [InlineData("nonFiniteDefaults", "views/nonfinite.heddle")]
        [InlineData("nonFiniteFloatDefaults", "views/nonfinitef.heddle")]
        public void ARealDefaultWithNoLiteralFormDegrades(string extension, string key)
        {
            var t = "@model(){{System.String}}@\\\n@" + extension + "(this)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>The near neighbour: a finite <c>double</c> default, including the smallest one there is, still
        /// precompiles and renders the engine's bytes — so the row above is about the values with no literal and
        /// not about real defaults at all.</summary>
        [Fact]
        public void AFiniteRealDefaultStillPrecompiles()
        {
            const string t = "@model(){{System.String}}@\\\n@finiteDefaults(this)\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/finitedefaults.heddle", t) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, "views/finitedefaults.heddle");

            var (pre, dyn) = DifferentialHarness.Render("views/finitedefaults.heddle", t, typeof(string), "z");
            Assert.Equal(dyn, pre);
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
