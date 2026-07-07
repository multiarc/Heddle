using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Language.Expressions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The executable grammar spec for phase 5 (grammar.md parse corpus PP01–PP24 / NP01–NP15): the 5th
    /// <c>call</c> alternative (named arguments), the <c>this</c> primary, the <c>def_props</c> rule family,
    /// and their DTO/AST shapes via the public <see cref="ExprNode"/>/<see cref="PropDeclaration"/>/
    /// <see cref="NamedArgument"/> surfaces. Positive rows assert shapes; negative rows assert a positioned
    /// HED0003 syntax error (never an exception).
    /// </summary>
    public class PropsParseTests
    {
        private static CallParameter ParseCall(string template)
        {
            var ctx = DocumentParser.Parse(template,
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.Empty(ctx.Errors);
            return ctx.OutputChains.First().Chain.First().CallParameter;
        }

        private static DefinitionItem ParseDef(string template, string name)
        {
            var ctx = DocumentParser.Parse(template,
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.Empty(ctx.Errors);
            return ctx.DefinitionsBlock.Definitions[name];
        }

        private static ExprNode Expr(string template) => ParseCall(template).NativeExpression;

        private static void Path(ExprNode node, params string[] segments)
        {
            var path = Assert.IsType<PathNode>(node);
            Assert.Equal(segments, path.Segments.ToArray());
        }

        // ---- Call side: named arguments (PP01–PP14) ----

        [Fact]
        public void PP01_PositionalMemberPlusNamedArguments()
        {
            var p = ParseCall("@card(Article, style: \"wide\", compact: true)");
            Assert.Equal(new[] { "Article" }, p.ModelParameter);
            Assert.Null(p.NativeExpression);
            Assert.NotNull(p.PropArguments);
            Assert.Equal(2, p.PropArguments.Count);
            Assert.Equal("style", p.PropArguments[0].Name);
            Assert.Equal("wide", Assert.IsType<LiteralNode>(p.PropArguments[0].Value).Value);
            Assert.Equal("compact", p.PropArguments[1].Name);
            Assert.Equal(true, Assert.IsType<LiteralNode>(p.PropArguments[1].Value).Value);
        }

        [Fact]
        public void PP02_NoNamedArgumentsIsAlternativeTwo()
        {
            var p = ParseCall("@card(Article)");
            Assert.Null(p.PropArguments);
            Assert.Equal(new[] { "Article" }, p.ModelParameter);
        }

        [Fact]
        public void PP03_NoPositionalIsEmptyModelParameter()
        {
            var p = ParseCall("@card(style: \"wide\")");
            Assert.NotNull(p.PropArguments);
            Assert.Single(p.PropArguments);
            Assert.True(p.IsModelTypeParameter);
            Assert.True(p.ModelParameter == null || p.ModelParameter.Length == 0 ||
                        string.IsNullOrEmpty(p.ModelParameter[0]));
        }

        [Fact]
        public void PP04_RootReferenceNamedValue()
        {
            var p = ParseCall("@card(Article, style: ::Site.DefaultCardStyle)");
            var path = Assert.IsType<PathNode>(p.PropArguments[0].Value);
            Assert.True(path.RootRef);
            Assert.Equal(new[] { "Site", "DefaultCardStyle" }, path.Segments.ToArray());
        }

        [Fact]
        public void PP05_UnaryNamedValue()
        {
            var p = ParseCall("@card(Article, compact: !Hidden)");
            var unary = Assert.IsType<UnaryNode>(p.PropArguments[0].Value);
            Assert.Equal(ExprOperator.Not, unary.Operator);
            Path(unary.Operand, "Hidden");
        }

        [Fact]
        public void PP06_TernaryNamedValue()
        {
            var p = ParseCall("@card(Article, style: Featured ? \"wide\" : \"plain\")");
            var ternary = Assert.IsType<TernaryNode>(p.PropArguments[0].Value);
            Path(ternary.Condition, "Featured");
        }

        [Fact]
        public void PP07_FunctionNamedValue()
        {
            var p = ParseCall("@card(Article, style: upper(Kind))");
            var call = Assert.IsType<CallNode>(p.PropArguments[0].Value);
            Assert.Equal("upper", call.Name);
        }

        [Fact]
        public void PP08_NativePositionalWithNamedArgument()
        {
            var p = ParseCall("@card(Price * 2, style: \"x\")");
            var binary = Assert.IsType<BinaryNode>(p.NativeExpression);
            Assert.Equal(ExprOperator.Multiply, binary.Operator);
            Assert.Single(p.PropArguments);
        }

        [Fact]
        public void PP09_ThisPositionalIsEmptyModelParameter()
        {
            var p = ParseCall("@card(this, style: \"x\")");
            Assert.Null(p.NativeExpression);
            Assert.True(p.ModelParameter == null || p.ModelParameter.Length == 0 ||
                        string.IsNullOrEmpty(p.ModelParameter[0]));
            Assert.Single(p.PropArguments);
        }

        [Fact]
        public void PP10_ThisExpression()
        {
            Assert.IsType<ThisNode>(Expr("@out(this)"));
        }

        [Fact]
        public void PP11_ThisMemberHop()
        {
            var path = Assert.IsType<PathNode>(Expr("@out(this.Name)"));
            Assert.Equal(new[] { "Name" }, path.Segments.ToArray());
            Assert.IsType<ThisNode>(path.Target);
        }

        [Fact]
        public void PP12_ThisAsFunctionArgument()
        {
            // Grammar reality (like phase 1's @(upper(Name))): a single-argument call parses as the alt-3
            // nested-chain carrier — the function-argument binding happens at compile time. 'this' rides as
            // the carrier's native parameter, which is the "this as a function argument" the corpus pins.
            var p = ParseCall("@(len(this))");
            Assert.NotNull(p.ChainParameter);
            var carrier = Assert.Single(p.ChainParameter);
            Assert.Equal("len", carrier.ExtensionName);
            Assert.IsType<ThisNode>(carrier.CallParameter.NativeExpression);
        }

        [Fact]
        public void PP13_NestedChainUnchanged()
        {
            var p = ParseCall("@out(a():b())");
            Assert.Null(p.NativeExpression);
            Assert.Null(p.PropArguments);
            Assert.NotNull(p.ChainParameter);
        }

        [Fact]
        public void PP14_NamedArgsThenChain()
        {
            var ctx = DocumentParser.Parse("@card(Article, style: \"wide\"):html()",
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.Empty(ctx.Errors);
            var chain = ctx.OutputChains.First().Chain;
            Assert.Equal(2, chain.Count);
            Assert.NotNull(chain[0].CallParameter.PropArguments);
            Assert.Equal("html", chain[1].ExtensionName);
        }

        // ---- Definition side: prop declarations & slots (PP15–PP24) ----

        [Fact]
        public void PP15_TwoPropsWithDecodedDefaults()
        {
            var def = ParseDef("@% <card(style: string = \"plain\", compact: bool = false)>{{x}} :: Article %@", "card");
            Assert.Equal(2, def.PropDeclarations.Count);
            Assert.Equal("style", def.PropDeclarations[0].Name);
            Assert.Equal("string", def.PropDeclarations[0].TypeName);
            Assert.True(def.PropDeclarations[0].HasDefault);
            Assert.Equal("plain", def.PropDeclarations[0].DefaultValue);
            Assert.Equal("compact", def.PropDeclarations[1].Name);
            Assert.Equal(false, def.PropDeclarations[1].DefaultValue);
        }

        [Fact]
        public void PP16_SlotOnly()
        {
            var def = ParseDef("@% <picker(out:: MenuOption)>{{x}} :: Menu %@", "picker");
            Assert.Equal("MenuOption", def.SlotTypeName);
            Assert.Empty(def.PropDeclarations);
        }

        [Fact]
        public void PP17_PropAndSlot()
        {
            var def = ParseDef("@% <combo(style: string = \"plain\", out:: Option)>{{x}} %@", "combo");
            Assert.Single(def.PropDeclarations);
            Assert.Equal("Option", def.SlotTypeName);
        }

        [Fact]
        public void PP18_PropsCoexistWithBase()
        {
            var def = ParseDef(
                "@% <card(style: string = \"plain\")>{{x}} :: Article <fancy(tone: string = \"info\"):card>{{x}} :: Article %@",
                "fancy");
            Assert.NotNull(def.BaseDefinition);
            Assert.Single(def.PropDeclarations);
            Assert.Equal("tone", def.PropDeclarations[0].Name);
        }

        [Fact]
        public void PP19_DottedGenericTypeName()
        {
            var def = ParseDef("@% <grid(items: System.Collections.Generic.List<string>)>{{x}} %@", "grid");
            Assert.Single(def.PropDeclarations);
            Assert.Equal("System.Collections.Generic.List<string>", def.PropDeclarations[0].TypeName);
        }

        [Fact]
        public void PP20_SignedNumericDefault()
        {
            var def = ParseDef("@% <pad(width: int = -4)>{{x}} %@", "pad");
            Assert.Equal(-4, def.PropDeclarations[0].DefaultValue);
        }

        [Fact]
        public void PP21_DefaultedPropBeforeRequired()
        {
            var def = ParseDef("@% <flag(active: bool = true, label: string)>{{x}} %@", "flag");
            Assert.True(def.PropDeclarations[0].HasDefault);
            Assert.False(def.PropDeclarations[1].HasDefault);
        }

        [Fact]
        public void PP22_EmptyPropList()
        {
            var def = ParseDef("@% <card()>{{x}} %@", "card");
            Assert.Empty(def.PropDeclarations);
            Assert.Null(def.SlotTypeName);
        }

        [Fact]
        public void PP23_NamedArgsInDefaultOutputChain()
        {
            var ctx = DocumentParser.Parse(
                "@% <card(style: string)> -> (Article, style: \"wide\") {{x}} %@",
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.Empty(ctx.Errors);
            var call = ctx.DefaultChains.First().Chain.First();
            Assert.Equal(new[] { "Article" }, call.CallParameter.ModelParameter);
            Assert.NotNull(call.CallParameter.PropArguments);
            Assert.Equal("style", call.CallParameter.PropArguments[0].Name);
        }

        [Fact]
        public void PP24_ArrayTypeName()
        {
            var def = ParseDef("@% <note(tags: string[])>{{x}} %@", "note");
            Assert.Equal("string[]", def.PropDeclarations[0].TypeName);
        }

        // ---- Negative corpus: positioned HED0003 (NP01–NP15) ----

        [Theory]
        [InlineData("@card(Article, style: @ Model.X )")]     // NP01 C# tier as named-argument value
        [InlineData("@card(Article, style: a():b())")]        // NP02 nested chain as named-argument value
        [InlineData("@card(A, B)")]                            // NP03 second positional
        [InlineData("@card(style: \"x\", Article)")]          // NP04 positional after named
        [InlineData("@card(Article, style:)")]                // NP05 missing value
        [InlineData("@card(Article, : \"x\")")]               // NP06 missing name
        [InlineData("@card(Article,, style: \"x\")")]         // NP07 empty argument slot
        [InlineData("@% <card(style string)>{{x}} %@")]       // NP08 missing ':' between name and type
        [InlineData("@% <card(style: string = Name)>{{x}} %@")] // NP09 identifier as default
        [InlineData("@% <card(style: string = 1 + 2)>{{x}} %@")] // NP10 expression as default
        [InlineData("@% <card(style: string,)>{{x}} %@")]     // NP11 dangling comma in prop list
        [InlineData("@card(Article, this: 1)")]               // NP12 'this' as an argument name
        [InlineData("@% <card(true: bool)>{{x}} %@")]         // NP13 keyword as a prop name
        [InlineData("@% <card(style: string = -\"x\")>{{x}} %@")] // NP14 sign on a non-numeric literal
        [InlineData("@card(Article, style: \"a\" style: \"b\")")] // NP15 missing comma between named arguments
        public void NegativeParseCorpus(string template)
        {
            var ctx = DocumentParser.Parse(template,
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.NotEmpty(ctx.Errors);
            Assert.Contains(ctx.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.SyntaxError);
        }

        // ---- Editor-token classification under ProvideLanguageFeatures ----

        private static List<HeddleTokenType> Tokens(string template)
        {
            var ctx = DocumentParser.Parse(template,
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.Empty(ctx.Errors);
            return ctx.Tokens.Select(t => t.HeddleTokenType).ToList();
        }

        [Fact]
        public void NamedArgumentTokensEmitted()
        {
            var tokens = Tokens("@card(Article, style: \"wide\")");
            Assert.Contains(HeddleTokenType.Id, tokens);
            Assert.Contains(HeddleTokenType.Delim, tokens);
            Assert.Contains(HeddleTokenType.Literal, tokens);
        }

        [Fact]
        public void ThisTokenClassifiedAsLiteral()
        {
            Assert.Contains(HeddleTokenType.Literal, Tokens("@out(this)"));
        }

        [Fact]
        public void PropDeclarationTokensEmitted()
        {
            var tokens = Tokens("@% <card(style: string = \"plain\", out:: Option)>{{x}} %@");
            Assert.Contains(HeddleTokenType.DefType, tokens);
            Assert.Contains(HeddleTokenType.Delim, tokens);
            Assert.Contains(HeddleTokenType.Operator, tokens);
        }
    }
}
