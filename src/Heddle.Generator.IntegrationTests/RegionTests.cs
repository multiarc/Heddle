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

        // The flagship 'feed' component: two public regions (one typed), one private region, props, the single
        // @out() slot, and the @item(this) region call nested inside the @list body (the depth trap).
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
            Assert.NotEmpty(gen.TemplateSources); // natively precompiled — no un-precompile reason
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
            Assert.NotEmpty(gen.TemplateSources); // the OQ1 gate: native, not fallback
            var (pre, dyn) = RenderBoth("views/region-full.heddle", t, Model());
            Assert.Equal(dyn, pre);
            // The FILLED bytes at depth — a top-level-only fill install would render the default '<li>A</li>' on
            // both tiers and pass the differential while failing this pin.
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
            Assert.NotEmpty(gen.TemplateSources); // native — no recursion, no fallback
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

        // The fixtures below were rewritten when the generator behavior was corrected. Until then it was STRICTER than
        // the engine: any fault verdict un-precompiled the whole template *silently*, and the tentative base-not-found
        // error was filtered out entirely. The generator now reacts to each verdict exactly as HeddleCompiler.BuildRegionFillScope
        // reacts. The assertion shape follows: the pin is the *twin relationship* — same condition, same position, matching
        // error on both tiers.

        [Fact]
        public void PrivateOverrideRaisesTheMatchingErrorOnBothTiers()
        {
            var t = Feed + "@feed(){{@%<divider:divider>{{<hr class=\"dark\">}}%@}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-private.heddle", t) });

            var build = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7024"));
            Assert.Equal(DiagnosticSeverity.Error, build.Severity);
            Assert.Contains("'divider'", build.GetMessage());
            Assert.Contains("'feed'", build.GetMessage());
            // The tentative base-not-found error is RETRACTED, exactly as the runtime retracts it before raising —
            // the user gets one error about privacy, not two about two different things.
            Assert.DoesNotContain(gen.Diagnostics, d => d.GetMessage().Contains("Base definition divider"));

            var dynamic = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(RegionFeed)));
            Assert.False(dynamic.CompileResult.Success);
            var runtimeError = Assert.Single(dynamic.CompileResult.ErrorList.Where(e => e.DiagnosticId == "HED5019"));
            // Same anchoring: both tiers point at the override declaration.
            Assert.Equal(runtimeError.Position.StartIndex,
                TemplateOffsetOf(t, "views/region-private.heddle", build));
        }

        [Fact]
        public void DanglingOverrideSurfacesTheSameErrorOnBothTiers()
        {
            var t = Feed + "@feed(){{@%<ghost:ghost>{{x}}%@}}";
            var gen = DifferentialHarness.Generate(new[] { ("views/region-dangling.heddle", t) });

            // Skipped as the runtime skips it — no refusal, no HED7024 — and its parse-emitted error now reaches
            // the build channel instead of waiting for the first dynamic render.
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7024");
            var build = Assert.Single(gen.Diagnostics.Where(
                d => d.Severity == DiagnosticSeverity.Error &&
                     d.GetMessage().Contains("Base definition ghost couldn't be found")));

            var dynamic = new HeddleTemplate(t, new CompileContext(new TemplateOptions(), typeof(RegionFeed)));
            Assert.False(dynamic.CompileResult.Success);
            var runtimeError = Assert.Single(dynamic.CompileResult.ErrorList.Where(
                e => e.Error == "Base definition ghost couldn't be found"));
            // This used to be `Assert.NotNull(build)` — which Assert.Single had already guaranteed, so it could not fail.
            // The twin relationship this fixture exists to pin includes the anchor: same offset on both tiers.
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
        /// <para>Q8.16: this used to <c>return 0</c>. That made the caller's subtraction a no-op and the whole
        /// helper decorative — the file identity and the line/column mapping were asserted nowhere, so the
        /// generator could have anchored the error in the wrong file, or emitted a line span inconsistent with its
        /// span, and every assertion here would still have been green.</para>
        /// </summary>
        private static int TemplateOffsetOf(string template, string key, Diagnostic diagnostic)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            Assert.True(lineSpan.IsValid, "the build diagnostic is not anchored in any file");
            Assert.Equal(key, lineSpan.Path);

            // Independent derivation: line start + character, over the same bytes the generator was handed.
            var text = SourceText.From(template);
            var start = lineSpan.StartLinePosition;
            Assert.InRange(start.Line, 0, text.Lines.Count - 1);
            var offset = text.Lines[start.Line].Start + start.Character;

            // The reported line/character and the reported span must describe the same point.
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
        public void UntypedRegionWithValueArgumentSilentlyDegrades()
        {
            // An untyped region called with an explicit value: its dynamic body typing is the argument's type,
            // which the emitter does not reproduce — left silently un-precompiled (D8/F2), rendered identically
            // by the dynamic tier.
            var t = "@model(){{" + FeedType + "}}@\\\n" +
                    "@%<panel>{{@%<:head>{{[x]}}%@@head(Articles)}} :: " + FeedType + "%@\n@panel()";
            var dynamic = AssertNotPrecompiledAndCompileDynamic("views/region-abstract.heddle", t);
            Assert.True(dynamic.CompileResult.Success, dynamic.CompileResult.ToString());
            Assert.Equal("[x]", dynamic.Generate(Model()).Trim());
        }

        [Fact]
        public void InnerDefinitionShadowingFunctionConvergesToDynamicTier()
        {
            // 'upper' is a default-table function name; the inner definition shadows it and the dynamic tier
            // resolves definition-first. After F1 the generator emits the DEFINITION too — a deliberate, pinned
            // byte change (the old flat-_parse resolution missed the inner def and emitted the function).
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
    }
}
