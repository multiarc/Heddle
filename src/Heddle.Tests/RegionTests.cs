using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 7 (post-2.0) — named content regions, dynamic tier. Covers the testing-plan fixture rows that the
    /// dynamic backend owns: the flagship worked example (with the BLOCKER-A depth trap), call-scoping,
    /// emit-then-retract additivity (D5/D12), HED5019/HED5020, the reused narrowing/member errors (D7), the
    /// self-call→base rule (D4 step 5), and the sibling-idiom regression.
    /// </summary>
    public class RegionTests
    {
        // The flagship 'feed' component: two public regions (one typed), one private region, props, and the
        // single @out() slot. The @item(this) call is nested inside the @list body — the depth trap.
        private const string Feed =
            "@%<feed(theme: string = \"light\", title: string = \"Home\")>{{" +
            "@%<:heading>{{<h2 class=\"@(theme)\">@(title)</h2>}}" +
            "<:item :: RegionArticle>{{<li>@(Title)</li>}}" +
            "<divider>{{<hr class=\"@(theme)\">}}%@" +
            "@heading()<ul>@list(Articles){{@item(this)}}</ul>@divider()@out()}} :: RegionFeedModel%@";

        private static HeddleTemplate Compile(string document, Type modelType, TemplateOptions options = null)
        {
            HeddleTemplate.Configure(typeof(RegionTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(document, new CompileContext(options ?? new TemplateOptions(), modelType));
        }

        private static RegionFeedModel Model() => new RegionFeedModel
        {
            Articles = new System.Collections.Generic.List<RegionArticle>
            {
                new RegionArticle { Title = "A", Id = 1 },
                new RegionArticle { Title = "B", Id = 2 }
            },
            ShowHeading = true
        };

        [Fact] // region_feed_defaults
        public void DefaultsRenderWhenNoOverrideBlock()
        {
            var t = Compile(Feed + "@feed()", typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(
                "<h2 class=\"light\">Home</h2><ul><li>A</li><li>B</li></ul><hr class=\"light\">",
                t.Generate(Model()).Trim());
        }

        [Fact] // region_feed_full — the ratified worked example incl. the @item(this) fill AT DEPTH
        public void FillsRenderIncludingTypedFillAtDepth()
        {
            var t = Compile(Feed +
                "@feed(){{@%<heading:heading>{{<h2 class=\"hero\">Latest</h2>}}" +
                "<item:item>{{<li>@(Title)#@(Id)</li>}}%@<p class=\"lede\">intro</p>}}",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(
                "<h2 class=\"hero\">Latest</h2><ul><li>A#1</li><li>B#2</li></ul><hr class=\"light\"><p class=\"lede\">intro</p>",
                t.Generate(Model()).Trim());
        }

        [Fact] // region_fill_at_depth — minimal isolate: region call nested in an @if body
        public void FillReachesRegionCallNestedInBranchBody()
        {
            var t = Compile(
                "@%<panel>{{@%<:head>{{[default]}}%@@if(ShowHeading){{@head()}}}} :: RegionFeedModel%@" +
                "@panel(){{@%<head:head>{{[filled]}}%@}}",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[filled]", t.Generate(Model()).Trim());
        }

        [Fact] // region_feed_two_calls — call-scoped, no leak
        public void SecondCallWithoutOverridesRendersDefaults()
        {
            var t = Compile(Feed +
                "@feed(){{@%<heading:heading>{{<h2 class=\"hero\">Latest</h2>}}%@}}|@feed()",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var output = t.Generate(Model()).Trim();
            var parts = output.Split('|');
            Assert.StartsWith("<h2 class=\"hero\">Latest</h2>", parts[0]);
            Assert.StartsWith("<h2 class=\"light\">Home</h2>", parts[1]);
        }

        [Fact] // region_private_override → HED5019, positioned, single error (candidate error retracted)
        public void PrivateRegionOverrideRaisesHed5019AndRetractsBaseNotFound()
        {
            var t = Compile(Feed + "@feed(){{@%<divider:divider>{{<hr class=\"dark\">}}%@}}",
                typeof(RegionFeedModel));
            Assert.False(t.CompileResult.Success);
            var errors = t.CompileResult.Errors;
            var hed5019 = errors.Where(e => e.DiagnosticId == HeddleDiagnosticIds.RegionNotPublic).ToList();
            Assert.Single(hed5019);
            Assert.NotEqual(default, hed5019[0].Position);
            Assert.Contains("divider", hed5019[0].Error);
            Assert.DoesNotContain(errors, e => e.Error.Contains("couldn't be found"));
        }

        [Fact] // region_undeclared_override — base IS in scope (D12): a normal local override, no region routing
        public void InScopeBaseStaysANormalOverride()
        {
            var t = Compile(
                "@%<masthead>{{[site]}}%@" + Feed +
                "@%<masthead:masthead>{{[page]}}%@@masthead()@feed()",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.StartsWith("[page]", t.Generate(Model()).Trim());
        }

        [Fact] // region_dangling_override — no region, base unresolved → the error stays, byte-identical text
        public void DanglingOverrideKeepsBaseNotFoundError()
        {
            var t = Compile(Feed + "@feed(){{@%<ghost:ghost>{{x}}%@}}", typeof(RegionFeedModel));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.Error == "Base definition ghost couldn't be found" &&
                     !e.Position.Equals(default(Heddle.Strings.Core.BlockPosition)));
        }

        [Fact] // dangling override outside any definition-call body keeps today's error too
        public void DanglingOverrideAtDocumentScopeKeepsError()
        {
            var t = Compile("@%<ghost:ghost>{{x}}%@text", typeof(RegionFeedModel));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.Error == "Base definition ghost couldn't be found");
        }

        [Fact] // region_duplicate → HED5020 at the second declaration
        public void DuplicatePublicRegionRaisesHed5020()
        {
            var t = Compile(
                "@%<card>{{@%<:head>{{a}}<:head>{{b}}%@@head()}}%@@card()",
                typeof(RegionFeedModel));
            Assert.False(t.CompileResult.Success);
            var errors = t.CompileResult.Errors
                .Where(e => e.DiagnosticId == HeddleDiagnosticIds.DuplicateRegionDeclaration).ToList();
            Assert.Single(errors);
            Assert.Contains("card", errors[0].Error);
            Assert.Contains("head", errors[0].Error);
        }

        [Fact] // region_public_vs_private — public colliding with private keeps the id-less message (F6)
        public void PublicRegionCollidingWithPrivateKeepsIdlessMessage()
        {
            var t = Compile(
                "@%<card>{{@%<head>{{a}}<:head>{{b}}%@@head()}}%@@card()",
                typeof(RegionFeedModel));
            Assert.False(t.CompileResult.Success);
            Assert.DoesNotContain(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.DuplicateRegionDeclaration);
            Assert.Contains(t.CompileResult.Errors,
                e => e.Error.Contains("with the same name already exists"));
        }

        [Fact] // region_docscope_public — a <:x> at document scope is an id-less positioned parse error (F6)
        public void DocumentScopePublicRegionIsIdlessError()
        {
            var t = Compile("@%<:x>{{a}}%@text", typeof(RegionFeedModel));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.Error == "A public region '<:x>' can only be declared inside a definition body." &&
                     e.DiagnosticId == null);
        }

        [Fact] // region_typed_badmember — typed override body reading a non-Article member → HED0001 (D7)
        public void TypedOverrideBodyBadMemberIsHed0001()
        {
            var t = Compile(Feed + "@feed(){{@%<item:item>{{<li>@(Nope)</li>}}%@}}", typeof(RegionFeedModel));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.PropertyNotFound && e.Error.Contains("Nope"));
        }

        [Fact] // region_typed_narrow — non-assignable narrowing → the pre-existing id-less error, positioned,
               // fired the pre-existing count (twice, D7/review E)
        public void NonAssignableNarrowingFiresPreexistingIdlessError()
        {
            var t = Compile(Feed + "@feed(){{@%<item:item>{{<li>x</li>}} :: PropSite%@}}",
                typeof(RegionFeedModel));
            Assert.False(t.CompileResult.Success);
            var narrows = t.CompileResult.Errors
                .Where(e => e.Error.Contains("isn't assignable to base")).ToList();
            Assert.NotEmpty(narrows);
            Assert.Equal(2, narrows.Count); // the accepted, documented pre-existing double-report (review E)
            Assert.All(narrows, e => Assert.NotEqual(default, e.Position));
        }

        [Fact] // assignable narrowing is accepted and the body types against the narrowed model
        public void AssignableNarrowingTypesTheOverrideBody()
        {
            var t = Compile(Feed +
                "@feed(){{@%<item:item>{{<li>@(Title)/@(Badge)</li>}} :: RegionSpecialArticle%@}}",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var model = new RegionFeedModel
            {
                Articles = new System.Collections.Generic.List<RegionArticle>
                {
                    new RegionSpecialArticle { Title = "A", Id = 1, Badge = "hot" }
                }
            };
            Assert.Contains("<li>A/hot</li>", t.Generate(model));
        }

        [Fact] // region_selfcall_to_default — a fill body calling its own region name resolves to the base default
        public void SelfCallInsideFillResolvesToBaseDefault()
        {
            var t = Compile(Feed +
                "@feed(){{@%<heading:heading>{{[wrap:@heading()]}}%@}}",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.StartsWith("[wrap:<h2 class=\"light\">Home</h2>]", t.Generate(Model()).Trim());
        }

        [Fact] // region_sibling_from_fill — a fill body calling a SIBLING region resolves the sibling's scope entry
        public void SiblingCallInsideFillResolvesSiblingFill()
        {
            var t = Compile(
                "@%<panel>{{@%<:head>{{[h-default]}}<:foot>{{[f-default]}}%@@head()}} :: RegionFeedModel%@" +
                "@panel(){{@%<head:head>{{[h:@foot()]}}<foot:foot>{{[f-filled]}}%@}}",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[h:[f-filled]]", t.Generate(Model()).Trim());
        }

        [Fact] // region_props_compose — typed props and named regions compose independently
        public void PropsAndRegionsCompose()
        {
            var t = Compile(Feed +
                "@feed(theme: \"dark\"){{@%<heading:heading>{{<h2 class=\"@(theme)\">@(title)</h2>}}%@}}",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var output = t.Generate(Model()).Trim();
            Assert.StartsWith("<h2 class=\"dark\">Home</h2>", output);
            Assert.Contains("<hr class=\"dark\">", output);
        }

        [Fact] // patterns_sibling_shell — the documented sibling-override idiom renders unchanged (regression)
        public void SiblingOverrideIdiomStillRenders()
        {
            var t = Compile(
                "@%<shell_header>{{<header>[default]</header>}}" +
                "<page_shell>{{<body>@shell_header()<main>@out()</main></body>}}%@" +
                "@%<shell_header:shell_header>{{<header class=\"hero\">[hero]</header>}}%@" +
                "@page_shell(){{<article>x</article>}}",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal(
                "<body><header class=\"hero\">[hero]</header><main><article>x</article></main></body>",
                t.Generate(Model()).Trim());
        }

        [Fact] // region_sibling_selfcall — a self-calling plain sibling override terminates at the base (D11/D12)
        public void SelfCallingSiblingOverrideTerminatesAtBase()
        {
            var t = Compile(
                "@%<x>{{[base]}}%@@%<x:x>{{[over:@x()]}}%@@x()",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[over:[base]]", t.Generate(Model()).Trim());
        }

        [Fact] // concurrency — region state is compile-time only; concurrent renders are identical
        public void ConcurrentRendersAreIdentical()
        {
            var t = Compile(Feed +
                "@feed(){{@%<heading:heading>{{<h2 class=\"hero\">Latest</h2>}}" +
                "<item:item>{{<li>@(Title)#@(Id)</li>}}%@<p>i</p>}}",
                typeof(RegionFeedModel));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var model = Model();
            var expected = t.Generate(model);
            var results = new string[64];
            Parallel.For(0, results.Length, i => results[i] = t.Generate(model));
            Assert.All(results, r => Assert.Equal(expected, r));
        }

        [Fact] // LSP-facing seam (D5): the retract clears the error from the parse list too (reference removal)
        public void RetractClearsParseErrorListAsWell()
        {
            HeddleTemplate.Configure(typeof(RegionTests).GetTypeInfo().Assembly);
            var context = new CompileContext(new TemplateOptions(), typeof(RegionFeedModel));
            var t = new HeddleTemplate(Feed + "@feed(){{@%<heading:heading>{{<h2>x</h2>}}%@}}", context);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(context.CompileErrors, e => e.Error.Contains("couldn't be found"));
        }
    }
}
