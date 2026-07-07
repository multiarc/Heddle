using System;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The D6 inheritance matrix (flattening, index stability, re-default, re-type assignability) and the
    /// declaration-side diagnostics HED5007–HED5010, HED5015–HED5017. Layout indices are asserted white-box via
    /// <see cref="PropLayout"/>; render behavior confirms defaults/overrides end-to-end.
    /// </summary>
    public class PropsInheritanceTests
    {
        private const string CardBase =
            "<card(style: string = \"plain\", compact: bool = false)>{{[@(style)|@(compact)]}} :: PropArticle";

        private static HeddleTemplate Compile(string document, Type modelType)
        {
            HeddleTemplate.Configure(typeof(PropsInheritanceTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(document, new CompileContext(new TemplateOptions(), modelType));
        }

        private static PropLayout ResolveLayout(string template, string definitionName)
        {
            var ctx = DocumentParser.Parse(template, new CompileContext(new TemplateOptions()), out _);
            var definition = ctx.DefinitionsBlock.Definitions[definitionName];
            using var scope = new CompileScope(new CompileContext(new TemplateOptions(), typeof(PropArticle)));
            return PropLayout.Resolve(definition, scope);
        }

        [Fact] // roadmap criterion 3 — an untyped definition binds one shared layout across two call sites
        public void TwoSitesShareOneLayoutInstanceWhileBodyMonomorphises()
        {
            using var scope = new CompileScope(new CompileContext(new TemplateOptions(), typeof(PropRoot)));
            const string doc =
                "@% <panel(style: string)>{{[@(style)]}} %@\n@panel(Article, style: \"a\")@panel(Menu, style: \"b\")";
            var parseContext = DocumentParser.Parse(doc, scope.CompileContext, out var clean);
            HeddleCompiler.Compile(clean, scope, parseContext, null);
            Assert.Empty(scope.CompileErrors);
            // Both @panel sites — compiled against different positional model types (Article vs Menu) — resolve
            // one cached PropLayout: the layout is model-orthogonal.
            Assert.Single(scope.CompileContext.ResolvedPropLayouts);
        }

        [Fact]
        public void ChildInheritsAndAppends_IndexStable()
        {
            var layout = ResolveLayout(
                "@% " + CardBase +
                " <fancy(tone: string = \"info\", style: string = \"wide\"):card>{{x}} :: PropArticle %@",
                "fancy");

            Assert.Equal(3, layout.Count);
            Assert.True(layout.TryGet("style", out var style));
            Assert.True(layout.TryGet("compact", out var compact));
            Assert.True(layout.TryGet("tone", out var tone));
            // Base slots keep their indices; re-declared style keeps index 0 with the new default; tone appends.
            Assert.Equal(0, style.Index);
            Assert.Equal("wide", style.DefaultBoxed);
            Assert.Equal(1, compact.Index);
            Assert.Equal(false, compact.DefaultBoxed);
            Assert.Equal(2, tone.Index);
            Assert.Equal("info", tone.DefaultBoxed);
        }

        [Fact]
        public void InheritedDefaultsAndOverridesRender()
        {
            const string doc = "@% " + CardBase +
                               " <fancy(tone: string = \"info\", style: string = \"wide\"):card>{{[@(style)|@(compact)|@(tone)]}} :: PropArticle %@\n" +
                               "@fancy(Article)|@fancy(Article, style: \"narrow\", tone: \"loud\")";
            var t = Compile(doc, typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var article = new PropRoot { Article = new PropArticle() };
            Assert.Equal("[wide|False|info]|[narrow|False|loud]", t.Generate(article).Trim());
        }

        [Fact]
        public void NarrowingReDeclarationIsAssignable()
        {
            // string is assignable to object: the slot narrows, keeps index 0, adds the default.
            var layout = ResolveLayout(
                "@% <media(content: object)>{{x}} <textmedia(content: string = \"hi\"):media>{{x}} %@", "textmedia");
            Assert.True(layout.TryGet("content", out var content));
            Assert.Equal(0, content.Index);
            Assert.Equal(typeof(string), content.Type.Type);
            Assert.Equal("hi", content.DefaultBoxed);
        }

        [Fact] // HED5008
        public void ReTypeNotAssignableIsError()
        {
            var t = Compile("@% " + CardBase +
                            " <fancy(style: int):card>{{x}} :: PropArticle %@\n@fancy(Article)", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.PropRedeclarationMismatch);
        }

        [Fact]
        public void FullOverrideWithProps()
        {
            const string doc =
                "@% <card(style: string = \"plain\")>{{x}} :: PropArticle" +
                " <card(style: string = \"boxed\", elevated: bool = false):card>{{[@(style)|@(elevated)]}} :: PropArticle %@\n" +
                "@card(Article)";
            var t = Compile(doc, typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[boxed|False]", t.Generate(new PropRoot { Article = new PropArticle() }).Trim());
        }

        [Fact] // HED5007
        public void DuplicatePropInHeader()
        {
            var t = Compile("@% <card(a: int, a: int)>{{x}} :: PropArticle %@", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.DuplicatePropDeclaration);
        }

        [Fact] // HED5009
        public void InconvertibleDefault()
        {
            var t = Compile("@% <pad(width: int = \"x\")>{{@(width)}} :: PropArticle %@\n@pad(Article)",
                typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.PropDefaultNotConvertible);
        }

        [Fact] // HED5010 (prop form)
        public void UnresolvedPropType()
        {
            var t = Compile("@% <x(a: NoSuch.Type)>{{x}} :: PropArticle %@\n@x(Article)", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.UnresolvedPropType);
        }

        [Fact] // HED5010 (slot form)
        public void UnresolvedSlotType()
        {
            var t = Compile("@% <x(out:: NoSuch.Type)>{{@out(this)}} :: PropArticle %@\n@x(Article){{y}}",
                typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.UnresolvedPropType && e.Error.Contains("slot"));
        }

        [Fact] // HED5015 (out reserved)
        public void ReservedPropNameOut()
        {
            var t = Compile("@% <card(out: PropArticle)>{{x}} :: PropArticle %@", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.ReservedPropName && e.Error.Contains("out::"));
        }

        [Fact] // HED5015 (this reserved)
        public void ReservedPropNameThis()
        {
            var t = Compile("@% <card(this: int)>{{x}} :: PropArticle %@", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.ReservedPropName);
        }

        [Fact] // HED5016
        public void InvalidSlotDeclaration()
        {
            var t = Compile("@% <card(foo:: PropArticle)>{{x}} :: PropArticle %@", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.InvalidSlotDeclaration);
        }

        [Fact] // HED5017
        public void MultipleSlotDeclarations()
        {
            var t = Compile("@% <x(out:: PropArticle, out:: PropMenu)>{{x}} :: PropArticle %@", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.MultipleSlotDeclarations);
        }
    }
}
