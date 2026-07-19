using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Heddle.Runtime.Parameters;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The D10 binding matrix (B01–B14) plus the frozen-array identity assertions and the zero-Roslyn proof for
    /// props fixtures. Errors are asserted as positioned diagnostics (HED* id), never bare "compilation failed".
    /// </summary>
    public class PropsBindingTests
    {
        // The D1 card, trimmed to isolate binding: body echoes the two props and the model's Title.
        private const string Card =
            "@% <card(style: string = \"plain\", compact: bool = false)>{{[@(style)|@(compact)|@(Title)]}} :: PropArticle %@\n";

        private static HeddleTemplate Compile(string document, Type modelType, TemplateOptions options = null)
        {
            HeddleTemplate.Configure(typeof(PropsBindingTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(document, new CompileContext(options ?? new TemplateOptions(), modelType));
        }

        private static PropRoot Root(string style = "plain") =>
            new PropRoot
            {
                Article = new PropArticle { Title = "T", Summary = "S" },
                Site = new PropSite { DefaultCardStyle = "rooted" },
                style = style
            };

        private static void AssertError(HeddleTemplate template, string diagnosticId)
        {
            Assert.False(template.CompileResult.Success);
            Assert.Contains(template.CompileResult.Errors, e => e.DiagnosticId == diagnosticId);
            Assert.All(template.CompileResult.Errors.Where(e => e.DiagnosticId == diagnosticId),
                e => Assert.NotEqual(default, e.Position));
        }

        [Fact] // B01
        public void PropsHonoredBodySeesModel()
        {
            var t = Compile(Card + "@card(Article, style: \"wide\", compact: true)", typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[wide|True|T]", t.Generate(Root()).Trim());
        }

        [Fact] // B02
        public void AllDefaultsRender()
        {
            var t = Compile(Card + "@card(Article)", typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[plain|False|T]", t.Generate(Root()).Trim());
        }

        [Fact] // B02 (frozen-array identity, white-box)
        public void AllConstantBinderReturnsSharedFrozenArray()
        {
            var frozen = new object[] { "plain", false };
            var binder = new PropsBinder(frozen, Array.Empty<PropsBinder.DynamicSlot>());
            Assert.True(binder.IsAllConstant);
            Assert.Same(frozen, binder.Bind(Scope.Null));
            Assert.Same(binder.Bind(Scope.Null), binder.Bind(Scope.Null));
        }

        [Fact] // B02 (dynamic sites clone)
        public void DynamicBinderClonesPerInvocation()
        {
            var frozen = new object[] { "plain", null };
            var plan = new[] { new PropsBinder.DynamicSlot(1, new ConstantParameter("x"), null) };
            var binder = new PropsBinder(frozen, plan);
            Assert.False(binder.IsAllConstant);
            var a = binder.Bind(Scope.Null);
            var b = binder.Bind(Scope.Null);
            Assert.NotSame(frozen, a);
            Assert.NotSame(a, b);
            Assert.Equal("x", a[1]);
        }

        [Fact] // B03
        public void RootReferenceValue()
        {
            var t = Compile(Card + "@card(Article, style: ::Site.DefaultCardStyle)", typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[rooted|False|T]", t.Generate(Root()).Trim());
        }

        [Fact] // B04
        public void UnknownPropListsDeclaredNames()
        {
            var t = Compile(Card + "@card(Article, stlye: \"x\")", typeof(PropRoot));
            AssertError(t, HeddleDiagnosticIds.UnknownProp);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.UnknownProp && e.Error.Contains("style") &&
                     e.Error.Contains("compact"));
        }

        [Fact] // B05
        public void MistypedArgumentIsError()
        {
            var t = Compile(Card + "@card(Article, compact: \"yes\")", typeof(PropRoot));
            AssertError(t, HeddleDiagnosticIds.PropTypeMismatch);
        }

        [Fact] // B06
        public void MissingRequiredProp()
        {
            var t = Compile("@% <hero(title: string)>{{@(title)}} :: PropArticle %@\n@hero(Article)", typeof(PropRoot));
            AssertError(t, HeddleDiagnosticIds.MissingRequiredProp);
        }

        [Fact] // B07
        public void DuplicateArgumentAtSecondOccurrence()
        {
            var t = Compile(Card + "@card(Article, style: \"a\", style: \"b\")", typeof(PropRoot));
            AssertError(t, HeddleDiagnosticIds.DuplicatePropArgument);
        }

        [Fact] // B08
        public void NamedArgumentsOnExtensionTarget()
        {
            var t = Compile("@list(Menu.Options, sep: \", \"){{@(Label)}}", typeof(PropRoot));
            AssertError(t, HeddleDiagnosticIds.NamedArgumentsNotSupported);
        }

        [Fact] // B08 (function target)
        public void NamedArgumentsOnFunctionTarget()
        {
            var t = Compile("@upper(Article, style: \"x\")", typeof(PropRoot));
            AssertError(t, HeddleDiagnosticIds.NamedArgumentsNotSupported);
        }

        [Fact] // B09
        public void NamedArgumentsOnPropLessDefinition()
        {
            var t = Compile("@% <plain>{{x}} :: PropArticle %@\n@plain(Article, a: 1)", typeof(PropRoot));
            AssertError(t, HeddleDiagnosticIds.DefinitionHasNoProps);
        }

        [Fact] // B10 (no narrowing)
        public void NarrowingArgumentIsError()
        {
            var t = Compile("@% <pad(width: int)>{{@(width)}} :: PropWidening %@\n@pad(this, width: Count)",
                typeof(PropWidening));
            AssertError(t, HeddleDiagnosticIds.PropTypeMismatch);
        }

        [Fact] // B10 (widening binds)
        public void WideningArgumentBinds()
        {
            var t = Compile("@% <pad(width: int)>{{@(width)}} :: PropWidening %@\n@pad(this, width: B)",
                typeof(PropWidening));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("7", t.Generate(new PropWidening { B = 7 }).Trim());
        }

        [Fact] // B11 (null default legal for reference type)
        public void NullDefaultForReferenceType()
        {
            var t = Compile("@% <opt(tag: string = null)>{{[@(tag)]}} :: PropArticle %@\n@opt(Article)",
                typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[]", t.Generate(Root()).Trim());
        }

        [Fact] // B12 (int literal widens into a double default)
        public void IntLiteralWidensIntoDoubleDefault()
        {
            var t = Compile("@% <n(x: double = 1)>{{@(x)}} :: PropArticle %@\n@n(Article)", typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("1", t.Generate(Root()).Trim());
        }

        [Fact] // B13 (named args under MemberPathsOnly → HED1014)
        public void NamedArgsUnderMemberPathsOnly()
        {
            var t = Compile(Card + "@card(Article, style: \"x\")", typeof(PropRoot),
                new TemplateOptions { ExpressionMode = ExpressionMode.MemberPathsOnly });
            AssertError(t, HeddleDiagnosticIds.NativeExpressionsDisabled);
        }

        [Fact] // B14 (default chain enforces required props)
        public void DefaultChainEnforcesRequiredProps()
        {
            var t = Compile("@% <card(style: string)> -> (Article) {{[@(style)]}} :: PropArticle %@", typeof(PropRoot));
            AssertError(t, HeddleDiagnosticIds.MissingRequiredProp);
        }

        [Fact] // zero-Roslyn proof (success criterion 1)
        public void PropsCompileWithoutRoslyn()
        {
            using var scope = new CompileScope(new CompileContext(
                new TemplateOptions { ExpressionMode = ExpressionMode.Native }, typeof(PropRoot)));
            var document = (Card + "@card(Article, style: \"wide\", compact: true)").Replace("\r\n", "\n");
            var parseContext = DocumentParser.Parse(document, scope.CompileContext, out var clean);
            HeddleCompiler.Compile(clean, scope, parseContext, null);
            Assert.Empty(scope.CompileErrors);
            Assert.Empty(scope.CSharpContext.Methods);
            Assert.Null(scope.CSharpContext.CompiledAssembly);
        }
    }
}
