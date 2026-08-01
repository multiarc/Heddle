using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Named content regions across the generator/precompiled layers. The differential asserts NATIVE precompiled
    /// parity on region defaults AND on overridden region fills (never via fallback): pass fixtures must produce a
    /// generated source and match the dynamic tier byte-for-byte. Depth fixtures additionally pin the FILLED bytes
    /// to ensure fills propagate. Erroring region templates and the plain sibling-override idiom are NOT precompiled —
    /// the dynamic tier owns them.
    /// </summary>
    public class RegionTests
    {
        private const string FeedType = "Heddle.Generator.IntegrationTests.Fixtures.RegionFeed";
        private const string ArticleType = "Heddle.Generator.IntegrationTests.Fixtures.RegionArticle";

        private static readonly string Feed =
            "@model(){{" + FeedType + "}}@\\\n" +
            "@%<feed(theme: string = \"light\", title: string = \"Home\")>{{" +
            "@%<:heading>{{<h2 class=\"@(theme)\">@(title)</h2>}}" +
            "<:item :: " + ArticleType + ">{{<li>@(Title)</li>}}" +
            "<divider>{{<hr class=\"@(theme)\">}}%@" +
            "@heading()<ul>@list(Articles){{@item(this)}}</ul>@divider()@out()}} :: " + FeedType + "%@\n";

        private static RegionFeed Model() => new RegionFeed
        {
            Articles = new List<RegionArticle>
            {
                new RegionArticle { Title = "A", Id = 1 },
                new RegionArticle { Title = "B", Id = 2 }
            },
            ShowHeading = true
        };

        private static (string precompiled, string dynamic) RenderBoth(string key, string content, object model)
            => DifferentialHarness.Render(key, content, typeof(RegionFeed), model);

        /// <summary>Asserts the template was NOT precompiled (no generated template source) and returns the
        /// dynamic-tier compile result for the caller's error assertions.</summary>
        private static HeddleTemplate AssertNotPrecompiledAndCompileDynamic(string key, string content)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Empty(gen.TemplateSources);
            return new HeddleTemplate(content, new CompileContext(new TemplateOptions(), typeof(RegionFeed)));
        }

        [Fact]
        public void RegionDefaultsPrecompileNativelyAndMatch()
        {
            var t = Feed + "@feed()";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-defaults.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-defaults.heddle", t, Model());
            Assert.Equal(dyn, pre);
            Assert.Contains("<h2 class=\"light\">Home</h2><ul><li>A</li><li>B</li></ul><hr class=\"light\">", pre);
        }

        [Fact]
        public void BareInnerDefinitionCallPrecompiles()
        {
            var t = "@model(){{" + FeedType + "}}@\\\n" +
                    "@%<wrap>{{@%<inner>{{[@(Title)]}} :: " + ArticleType + "%@" +
                    "@list(Articles){{@inner(this)}}}} :: " + FeedType + "%@\n@wrap()";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-innerdef.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-innerdef.heddle", t, Model());
            Assert.Equal(dyn, pre);
            Assert.Contains("[A][B]", pre);
        }

        [Fact]
        public void RegionFillsPrecompileNativelyIncludingDepth()
        {
            var t = Feed +
                    "@feed(){{@%<heading:heading>{{<h2 class=\"hero\">Latest</h2>}}" +
                    "<item:item>{{<li>@(Title)#@(Id)</li>}}%@<p class=\"lede\">intro</p>}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-full.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-full.heddle", t, Model());
            Assert.Equal(dyn, pre);
            // FILLED bytes at depth must propagate (top-level fills alone would fail this pin).
            Assert.Contains(
                "<h2 class=\"hero\">Latest</h2><ul><li>A#1</li><li>B#2</li></ul><hr class=\"light\"><p class=\"lede\">intro</p>",
                pre);
        }

        [Fact]
        public void FillReachesRegionCallInsideBranchBody()
        {
            var t = "@model(){{" + FeedType + "}}@\\\n" +
                    "@%<panel>{{@%<:head>{{[default]}}%@@if(ShowHeading){{@head()}}}} :: " + FeedType + "%@\n" +
                    "@panel(){{@%<head:head>{{[filled]}}%@}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-depth.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-depth.heddle", t, Model());
            Assert.Equal(dyn, pre);
            Assert.Contains("[filled]", pre);
            Assert.DoesNotContain("[default]", pre);
        }

        [Fact]
        public void TwoCallsAreIndependentlyScoped()
        {
            var t = Feed +
                    "@feed(){{@%<heading:heading>{{<h2 class=\"hero\">Latest</h2>}}%@}}|@feed()";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-two-calls.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-two-calls.heddle", t, Model());
            Assert.Equal(dyn, pre);
            var parts = pre.Split('|');
            Assert.Contains("<h2 class=\"hero\">Latest</h2>", parts[0]);
            Assert.Contains("<h2 class=\"light\">Home</h2>", parts[1]);
        }

        [Fact]
        public void SelfCallInsideFillResolvesBaseDefaultNatively()
        {
            var t = Feed + "@feed(){{@%<heading:heading>{{[wrap:@heading()]}}%@}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-selfcall.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-selfcall.heddle", t, Model());
            Assert.Equal(dyn, pre);
            Assert.Contains("[wrap:<h2 class=\"light\">Home</h2>]", pre);
        }

        [Fact]
        public void SiblingCallInsideFillResolvesSiblingFill()
        {
            var t = "@model(){{" + FeedType + "}}@\\\n" +
                    "@%<panel>{{@%<:head>{{[h-default]}}<:foot>{{[f-default]}}%@@head()}} :: " + FeedType + "%@\n" +
                    "@panel(){{@%<head:head>{{[h:@foot()]}}<foot:foot>{{[f-filled]}}%@}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-sibling.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-sibling.heddle", t, Model());
            Assert.Equal(dyn, pre);
            Assert.Contains("[h:[f-filled]]", pre);
        }

        [Fact]
        public void PropsAndRegionsCompose()
        {
            var t = Feed +
                    "@feed(theme: \"dark\"){{@%<heading:heading>{{<h2 class=\"@(theme)\">@(title)</h2>}}%@}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-props.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-props.heddle", t, Model());
            Assert.Equal(dyn, pre);
            Assert.Contains("<h2 class=\"dark\">Home</h2>", pre);
            Assert.Contains("<hr class=\"dark\">", pre);
        }

        [Fact]
        public void PrivateOverrideRaisesTheMatchingErrorOnBothTiers()
        {
            var t = Feed + "@feed(){{@%<divider:divider>{{<hr class=\"dark\">}}%@}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-private.heddle", t) });

            var build = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7024"));
            Assert.Equal(DiagnosticSeverity.Error, build.Severity);
            Assert.Contains("'divider'", build.GetMessage());
            Assert.Contains("'feed'", build.GetMessage());
            // Tentative base-not-found error is retracted; one privacy error, not two.
            Assert.DoesNotContain(gen.Diagnostics, d => d.GetMessage().Contains("Base definition divider"));

            var dynamic = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(RegionFeed)));
            Assert.False(dynamic.CompileResult.Success);
            var runtimeError = Assert.Single(dynamic.CompileResult.ErrorList, e => e.DiagnosticId == "HED5019");
            Assert.Equal(runtimeError.Position.StartIndex,
                TemplateOffsetOf(t, "views/region-private.heddle", build));
        }

        [Fact]
        public void DanglingOverrideSurfacesTheSameErrorOnBothTiers()
        {
            var t = Feed + "@feed(){{@%<ghost:ghost>{{x}}%@}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-dangling.heddle", t) });

            // Error reaches build channel, not deferred to first dynamic render.
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7024");
            var build = Assert.Single(gen.Diagnostics.Where(
                d => d.Severity == DiagnosticSeverity.Error &&
                     d.GetMessage().Contains("Base definition ghost couldn't be found")));

            var dynamic = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(RegionFeed)));
            Assert.False(dynamic.CompileResult.Success);
            var runtimeError = Assert.Single(dynamic.CompileResult.ErrorList,
                e => e.Error == "Base definition ghost couldn't be found");
            Assert.Equal(runtimeError.Position.StartIndex,
                TemplateOffsetOf(t, "views/region-dangling.heddle", build));
        }

        /// <summary>
        /// The build diagnostic's anchor, as an offset into <paramref name="template"/> — recomputed from what the
        /// diagnostic <i>reports</i> (its file path and its line/character position) rather than read off its raw
        /// <c>SourceSpan</c>. Callers compare the result with the dynamic tier's <c>Position.StartIndex</c>, so the
        /// comparison constrains the whole anchoring chain: the diagnostic must be attached to <b>this template's
        /// file</b> (not <c>Location.None</c> and not a generated <c>.g.cs</c>), its line/character mapping must
        /// agree with its span, and the resulting offset must be the one the runtime reports.
        /// <para>This used to <c>return 0</c>. That made the caller's subtraction a no-op and the whole
        /// helper decorative — the file identity and the line/column mapping were asserted nowhere, so the
        /// generator could have anchored the error in the wrong file, or emitted a line span inconsistent with its
        /// span, and every assertion here would still have been green.</para>
        /// </summary>
        private static int TemplateOffsetOf(string template, string key, Diagnostic diagnostic)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            Assert.True(lineSpan.IsValid, "the build diagnostic is not anchored in any file");
            Assert.Equal(key, lineSpan.Path);

            var text = SourceText.From(template);
            var start = lineSpan.StartLinePosition;
            Assert.InRange(start.Line, 0, text.Lines.Count - 1);
            var offset = text.Lines[start.Line].Start + start.Character;

            // Verify line/character maps to the span's start.
            Assert.Equal(diagnostic.Location.SourceSpan.Start, offset);
            return offset;
        }

        [Fact]
        public void SiblingOverrideIdiomStaysUnprecompiledAndCorrectDynamically()
        {
            var t = "@model(){{" + FeedType + "}}@\\\n" +
                    "@%<shell_header>{{<header>[default]</header>}}" +
                    "<page_shell>{{<body>@shell_header()<main>@out()</main></body>}}%@\n" +
                    "@%<shell_header:shell_header>{{<header class=\"hero\">[hero]</header>}}%@\n" +
                    "@page_shell(){{<article>x</article>}}";
            var dynamic = AssertNotPrecompiledAndCompileDynamic("views/region-shell.heddle", t);
            Assert.True(dynamic.CompileResult.Success, dynamic.CompileResult.ToString());
            Assert.Equal(
                "<body><header class=\"hero\">[hero]</header><main><article>x</article></main></body>",
                dynamic.Generate(Model()).Trim());
        }

        [Fact]
        public void SelfCallingSiblingOverrideIsNotPrecompiledAndTerminatesDynamically()
        {
            var t = "@model(){{" + FeedType + "}}@\\\n" +
                    "@%<x>{{[base]}}%@\n@%<x:x>{{[over:@x()]}}%@\n@x()";
            var dynamic = AssertNotPrecompiledAndCompileDynamic("views/region-sibling-selfcall.heddle", t);
            Assert.True(dynamic.CompileResult.Success, dynamic.CompileResult.ToString());
            Assert.Equal("[over:[base]]", dynamic.Generate(Model()).Trim());
        }

        [Fact]
        public void RegionDeclaringNoModelTakesTheValueItsCallSitePasses()
        {
            // A region that declares no model is not a region with no model: the engine compiles its body against
            // whatever the call site hands it, an explicit value included.
            var t = "@model(){{" + FeedType + "}}@\\\n" +
                    "@%<panel>{{@%<:head>{{[x]}}%@@head(Articles)}} :: " + FeedType + "%@\n@panel()";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-abstract.heddle", t) });
            DifferentialHarness.ExpectPrecompiled(gen, "views/region-abstract.heddle");
            var (pre, dyn) = RenderBoth("views/region-abstract.heddle", t, Model());
            Assert.Equal("[x]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        [Fact]
        public void InnerDefinitionShadowingFunctionConvergesToDynamicTier()
        {
            // 'upper' shadows default function; generator must emit DEFINITION too.
            var t = "@model(){{" + FeedType + "}}@\\\n" +
                    "@%<wrap>{{@%<upper>{{[DEF]}}%@@upper()}} :: " + FeedType + "%@\n@wrap()";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-shadow.heddle", t) });
            Assert.NotEmpty(gen.TemplateSources);
            var (pre, dyn) = RenderBoth("views/region-shadow.heddle", t, Model());
            Assert.Equal(dyn, pre);
            Assert.Contains("[DEF]", pre);
        }

        public static IEnumerable<object[]> Models()
        {
            yield return new object[] { Model() };
            yield return new object[] { new RegionFeed { Articles = new List<RegionArticle>() } };
        }

        [Theory]
        [MemberData(nameof(Models))]
        public void FlagshipFillParityAcrossModels(RegionFeed model)
        {
            var t = Feed +
                    "@feed(){{@%<heading:heading>{{<h2 class=\"hero\">Latest</h2>}}" +
                    "<item:item>{{<li>@(Title)#@(Id)</li>}}%@<p>i</p>}}";
            var (pre, dyn) = RenderBoth("views/region-flagship.heddle", t, model);
            Assert.Equal(dyn, pre);
        }

        // ---- One region body, two call sites of different models ----
        //
        // Everything inside a component body shares one parsed region, so the engine compiles that region's body
        // once — at whichever call site reaches it first — and every later call site's value is cast to the model
        // that first one typed it against. These fixtures make the model observable: the element shadows the host's
        // `Tag`, so a body typed by the host prints "host" for both calls and one typed by the element prints
        // "element" for both.

        private const string ShadowHostType = "Heddle.Generator.IntegrationTests.Fixtures.RegionShadowHost";
        private const string UnrelatedHostType = "Heddle.Generator.IntegrationTests.Fixtures.RegionUnrelatedHost";

        private static string ShadowTemplate(string calls) =>
            "@model(){{" + ShadowHostType + "}}@\\\n" +
            "@%<comp>{{@%<:r>{{[@(Tag)]}}%@" + calls + "}} :: " + ShadowHostType + "%@\n@comp()";

        private static RegionShadowHost ShadowModel() => new RegionShadowHost
        {
            Items = new List<RegionShadowElement> { new RegionShadowElement() }
        };

        private static (string precompiled, string dynamic) RenderShadow(string key, string content)
            => DifferentialHarness.Render(key, content, typeof(RegionShadowHost), ShadowModel());

        [Fact]
        public void RegionBodyTypedByFirstCallSiteServesTheSecond()
        {
            // The direct call reaches the region first, so both calls run the host-typed body — the element's own
            // `Tag` is never read, even inside the @list body that hands the region an element.
            var t = ShadowTemplate("@r()|@list(Items){{@r()}}");
            var gen = DifferentialHarness.Generate(new[] { ("views/region-share-direct-first.heddle", t) });
            DifferentialHarness.ExpectPrecompiled(gen, "views/region-share-direct-first.heddle");
            var (pre, dyn) = RenderShadow("views/region-share-direct-first.heddle", t);
            Assert.Equal("[host]|[host]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        [Fact]
        public void RegionBodyReachedOnlyFromAListIsTypedByTheElement()
        {
            // The near neighbour: one call site, so the element's model is the body's and the shadowed member is
            // the one that renders. Sharing must not cost this its precompiled tier.
            var t = ShadowTemplate("@list(Items){{@r()}}");
            var gen = DifferentialHarness.Generate(new[] { ("views/region-share-list-only.heddle", t) });
            DifferentialHarness.ExpectPrecompiled(gen, "views/region-share-list-only.heddle");
            var (pre, dyn) = RenderShadow("views/region-share-list-only.heddle", t);
            Assert.Equal("[element]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        [Fact]
        public void RegionBodyCalledTwiceUnderOneModelPrecompiles()
        {
            var t = ShadowTemplate("@r()|@r()");
            var gen = DifferentialHarness.Generate(new[] { ("views/region-share-twice.heddle", t) });
            DifferentialHarness.ExpectPrecompiled(gen, "views/region-share-twice.heddle");
            var (pre, dyn) = RenderShadow("views/region-share-twice.heddle", t);
            Assert.Equal("[host]|[host]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        [Fact]
        public void RegionBodySharedWithAnUnrelatedElementCastsAsTheEngineDoes()
        {
            // No shadowing needed: the host-typed body casts the element and the cast fails, which is exactly what
            // the engine's own compiled accessor does with it.
            var t = "@model(){{" + UnrelatedHostType + "}}@\\\n" +
                    "@%<comp>{{@%<:r>{{[@(Tag)]}}%@@r()|@list(Items){{@r()}}}} :: " + UnrelatedHostType + "%@\n" +
                    "@comp()";
            var key = "views/region-share-unrelated.heddle";
            var model = new RegionUnrelatedHost
            {
                Tag = "host",
                Items = new List<RegionUnrelatedElement> { new RegionUnrelatedElement { Tag = "element" } }
            };
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var dynamic = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(RegionUnrelatedHost)));
            Assert.True(dynamic.CompileResult.Success, dynamic.CompileResult.ToString());
            var engineThrow = Assert.Throws<InvalidCastException>(() => dynamic.Generate(model));
            var generatedThrow = Assert.Throws<InvalidCastException>(
                () => DifferentialHarness.RenderGenerated(gen, key, model));
            Assert.Equal(engineThrow.Message, generatedThrow.Message);
        }

        [Fact]
        public void RegionBodyReachedFirstFromAListCastsAsTheEngineDoes()
        {
            // Mirror order: the @list body reaches the region first, so the engine's one body is typed by the
            // element and the direct call's host is cast to it — a cast that fails. The emitted body is typed by
            // the same first call site and writes the same cast, so the reader gets the engine's own exception.
            var t = ShadowTemplate("@list(Items){{@r()}}|@r()");
            var key = "views/region-share-list-first.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var dynamic = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(RegionShadowHost)));
            Assert.True(dynamic.CompileResult.Success, dynamic.CompileResult.ToString());
            var engineThrow = Assert.Throws<InvalidCastException>(() => dynamic.Generate(ShadowModel()));
            var generatedThrow = Assert.Throws<InvalidCastException>(
                () => DifferentialHarness.RenderGenerated(gen, key, ShadowModel()));
            Assert.Equal(engineThrow.Message, generatedThrow.Message);
        }

        // ---- What a region's declared model resolves to, not how it is spelled ----
        //
        // The engine asks whether the declaration resolves to something other than `System.Object`. `dynamic`,
        // `object`, `System.Object` and declaring nothing at all all answer no, and for every one of them the body
        // is compiled against the value the call site passes.

        public static IEnumerable<object[]> ObjectResolvingSpellings()
        {
            yield return new object[] { "none", "" };
            yield return new object[] { "object", " :: object" };
            yield return new object[] { "dynamic", " :: dynamic" };
            yield return new object[] { "system-object", " :: System.Object" };
        }

        private static string ShadowSpelledTemplate(string spelling, string regionBody, string calls) =>
            "@model(){{" + ShadowHostType + "}}@\\\n" +
            "@%<comp>{{@%<r>{{" + regionBody + "}}" + spelling + "%@" + calls + "}} :: " + ShadowHostType + "%@\n" +
            "@comp()";

        /// <summary>A member the element type does not have, read in a region body the <c>@list</c> hands an
        /// element. The engine types that body from the call site and refuses the template; spelled `:: dynamic` or
        /// `:: System.Object` the emitter typed it from the declaration instead, came out with no model at all, and
        /// bound the read dynamically — precompiling a template the engine will not compile and throwing at
        /// render.</summary>
        [Theory]
        [MemberData(nameof(ObjectResolvingSpellings))]
        public void ARegionWhoseModelResolvesToObjectIsTypedByItsCallSite(string name, string spelling)
        {
            var t = ShadowSpelledTemplate(spelling, "[@(Missing)]", "@list(Items){{@r(this)}}");
            var key = "views/region-spelling-missing-" + name + ".heddle";

            var compiled = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(RegionShadowHost)));
            Assert.False(compiled.CompileResult.Success, "the engine types this body and has no 'Missing' on it");
            Assert.Contains("HED0001", compiled.CompileResult.ToString());

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.Empty(gen.TemplateSources);
        }

        /// <summary>The near neighbour, one cell away: the same region body reading a member the element does have.
        /// Every spelling must still precompile and render the engine's bytes, or the rule above is a blanket
        /// refusal of regions rather than a rule about the model.</summary>
        [Theory]
        [MemberData(nameof(ObjectResolvingSpellings))]
        public void ARegionReadingAMemberTheElementHasStillPrecompiles(string name, string spelling)
        {
            var t = ShadowSpelledTemplate(spelling, "[@(Tag)]", "@list(Items){{@r(this)}}");
            var key = "views/region-spelling-present-" + name + ".heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var (pre, dyn) = RenderShadow(key, t);
            Assert.Equal("[element]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        /// <summary>And the same again where two call sites share the region body, which is where the wrong model
        /// prints rather than throws: the direct call reaches it first, so both calls run the host-typed body and
        /// the element's own shadowing <c>Tag</c> is never read.</summary>
        [Theory]
        [MemberData(nameof(ObjectResolvingSpellings))]
        public void ASharedRegionBodyPrintsTheFirstCallSitesModelWhateverTheSpelling(string name, string spelling)
        {
            var t = ShadowSpelledTemplate(spelling, "[@(Tag)]", "@r(this)|@list(Items){{@r(this)}}");
            var key = "views/region-spelling-shared-" + name + ".heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var (pre, dyn) = RenderShadow(key, t);
            Assert.Equal("[host]|[host]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        /// <summary>And the spelling that does declare a model still types the body by it, so the re-keying did not
        /// simply route every region down one arm.</summary>
        [Fact]
        public void ARegionDeclaringARealTypeIsStillTypedByIt()
        {
            const string key = "views/region-spelling-declared.heddle";
            var t = ShadowSpelledTemplate(" :: " + ShadowHostType, "[@(Tag)]", "@r(this)");
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var (pre, dyn) = RenderShadow(key, t);
            Assert.Equal("[host]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        // ---- A region declaring a slot of its own ----

        /// <summary>The engine swaps the slot parameter type around every definition body it compiles and does not
        /// exempt a region — unlike the prop layout on the line above it, which it does. Without the swap here a
        /// valueless <c>@out()</c> inside such a region looked like an ordinary content splice and rendered, where
        /// the engine refuses the whole template.</summary>
        [Fact]
        public void AValuelessOutInsideARegionsOwnSlotIsRefusedByBothTiers()
        {
            const string key = "views/region-own-slot-valueless.heddle";
            var t = "@model(){{" + ShadowHostType + "}}@\\\n" +
                    "@%<comp>{{@%<r(out:: " + ShadowHostType + ")>{{[@out()]}}%@@r(){{<@(Tag)>}}}} :: " +
                    ShadowHostType + "%@\n@comp()";

            var compiled = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(RegionShadowHost)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains("HED5013", compiled.CompileResult.ToString());

            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            Assert.Empty(gen.TemplateSources);
        }

        /// <summary>Its near neighbour, and the half the slot mode was costing: the same region with a value on the
        /// <c>@out</c> is a slot projection the engine renders, and it now precompiles rather than degrading.</summary>
        [Fact]
        public void AValuedOutInsideARegionsOwnSlotProjectsTheCallerContent()
        {
            const string key = "views/region-own-slot-valued.heddle";
            var t = "@model(){{" + ShadowHostType + "}}@\\\n" +
                    "@%<comp>{{@%<r(out:: " + ShadowHostType + ")>{{[@out(this)]}}%@@r(){{<@(Tag)>}}}} :: " +
                    ShadowHostType + "%@\n@comp()";
            var gen = DifferentialHarness.Generate(new[] { (key, t) });
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var (pre, dyn) = RenderShadow(key, t);
            Assert.Equal("[<host>]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        // ---- One body, one fill scope ----
        //
        // The engine memoizes each item of a definition body by the parsed item it came from and saves and restores
        // the region fill scope around the body compile without putting it in that memo. So two call sites inside
        // one component body that fill the same region differently still run one body — the first site's, fills
        // included — and only the parser isolating the definition tree between them makes two.

        private static string FillRaceTemplate(string outerBody) =>
            "@model(){{" + FeedType + "}}@\\\n@%<panel>{{@%<:head>{{[d]}}%@@head()}} :: " + FeedType + "\n" +
            outerBody + "\n%@\n@outer()";

        public static IEnumerable<object[]> EnclosingBodies()
        {
            const string twoCalls = "@panel(){{@%<head:head>{{[A]}}%@}}|@panel(){{@%<head:head>{{[B]}}%@}}";
            yield return new object[] { "<outer>{{" + twoCalls + "}} :: " + FeedType, "[A]|[A]" };
            yield return new object[]
            {
                "<outer>{{@panel(){{@%<head:head>{{[A]}}%@}}|@panel()}} :: " + FeedType, "[A]|[A]"
            };
            yield return new object[]
            {
                "<outer>{{@panel()|@panel(){{@%<head:head>{{[A]}}%@}}}} :: " + FeedType, "[d]|[d]"
            };
            yield return new object[] { "<outer>{{@if(ShowHeading){{" + twoCalls + "}}}} :: " + FeedType, "[A]|[A]" };
            // Two articles, so the body runs twice — and both runs are the one shared body.
            yield return new object[] { "<outer>{{@list(Articles){{" + twoCalls + "}}}} :: " + FeedType, "[A]|[A][A]|[A]" };
            yield return new object[] { "<outer>{{@%<:reg>{{" + twoCalls + "}}%@@reg()}} :: " + FeedType, "[A]|[A]" };
            yield return new object[]
            {
                "<box>{{[@out()]}} :: " + FeedType + "\n<outer>{{@box(){{" + twoCalls + "}}}} :: " + FeedType,
                "[[A]|[A]]"
            };
        }

        /// <summary>Every enclosing body the parser does not isolate. Keyed with the fill scope on the emitter's
        /// side, each call site got a body of its own and the page rendered its own fill — the engine renders the
        /// first site's twice.</summary>
        [Theory]
        [MemberData(nameof(EnclosingBodies))]
        public void TwoFillsOfOneRegionInOneBodyRunTheFirstFill(string outerBody, string expected)
        {
            var t = FillRaceTemplate(outerBody);
            var key = "views/region-fill-race-" + outerBody.GetHashCode().ToString("x8") + ".heddle";
            var (pre, dyn) = RenderBoth(key, t, Model());
            Assert.Equal(expected, dyn.Trim());
            Assert.Equal(dyn, pre);
        }

        /// <summary>The near neighbour that must keep both fills: at document scope the parser hands each call site
        /// its own copy of the definition tree, so the engine compiles two bodies and each renders its own fill.
        /// Dropping the fill scope from the body identity must not collapse these two into one.</summary>
        [Fact]
        public void TwoFillsAtDocumentScopeEachRunTheirOwn()
        {
            const string key = "views/region-fill-race-document.heddle";
            var t = "@model(){{" + FeedType + "}}@\\\n@%<panel>{{@%<:head>{{[d]}}%@@head()}} :: " + FeedType +
                    "\n%@\n@panel(){{@%<head:head>{{[A]}}%@}}|@panel(){{@%<head:head>{{[B]}}%@}}";
            var (pre, dyn) = RenderBoth(key, t, Model());
            Assert.Equal("[A]|[B]", dyn.Trim());
            Assert.Equal(dyn, pre);
        }
    }
}
