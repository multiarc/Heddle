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
    /// The executable grammar spec (grammar.md parse corpus P01–P24 / N01–N14): alternative coexistence,
    /// AST shape via the public <see cref="ExprNode"/> API, literal typing, and editor-token classification.
    /// </summary>
    public class NativeExpressionParseTests
    {
        private static CallParameter Parse(string template)
        {
            var ctx = DocumentParser.Parse(template,
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.Empty(ctx.Errors);
            return ctx.OutputChains.First().Chain.First().CallParameter;
        }

        private static ExprNode Expr(string template) => Parse(template).NativeExpression;

        private static PathNode Path(ExprNode node, params string[] segments)
        {
            var path = Assert.IsType<PathNode>(node);
            Assert.Equal(segments, path.Segments.ToArray());
            return path;
        }

        // ---- P01–P03: coexistence (alternatives 1–3 unchanged) ----

        [Fact]
        public void P01_MemberPathUnchanged()
        {
            var p = Parse("@(Name)");
            Assert.Null(p.NativeExpression);
            Assert.Equal(new[] { "Name" }, p.ModelParameter);
        }

        [Fact]
        public void P02_NestedChainUnchanged()
        {
            var p = Parse("@out(a():b())");
            Assert.Null(p.NativeExpression);
            Assert.NotNull(p.ChainParameter);
        }

        [Fact]
        public void P03_CSharpExpressionUnchanged()
        {
            var p = Parse("@( @Model.X )");
            Assert.Null(p.NativeExpression);
            Assert.False(string.IsNullOrEmpty(p.CSharpExpression));
        }

        // ---- P04–P24: native-expression AST shapes ----

        [Fact]
        public void P04_Comparison()
        {
            var b = Assert.IsType<BinaryNode>(Expr("@if(Count > 0){{x}}"));
            Assert.Equal(ExprOperator.GreaterThan, b.Operator);
            Path(b.Left, "Count");
            Assert.Equal(0, Assert.IsType<LiteralNode>(b.Right).Value);
        }

        [Fact]
        public void P05_Multiply()
        {
            var b = Assert.IsType<BinaryNode>(Expr("@(Price * Quantity)"));
            Assert.Equal(ExprOperator.Multiply, b.Operator);
            Path(b.Left, "Price");
            Path(b.Right, "Quantity");
        }

        [Fact]
        public void P06_Coalesce()
        {
            var b = Assert.IsType<BinaryNode>(Expr("@(Name ?? \"anon\")"));
            Assert.Equal(ExprOperator.Coalesce, b.Operator);
            Path(b.Left, "Name");
            Assert.Equal("anon", Assert.IsType<LiteralNode>(b.Right).Value);
        }

        [Fact]
        public void P07_Ternary()
        {
            var t = Assert.IsType<TernaryNode>(Expr("@(IsFeatured ? \"a\" : \"\")"));
            Path(t.Condition, "IsFeatured");
            Assert.Equal("a", Assert.IsType<LiteralNode>(t.WhenTrue).Value);
            Assert.Equal("", Assert.IsType<LiteralNode>(t.WhenFalse).Value);
        }

        [Fact]
        public void P08_Precedence()
        {
            // A + B * C == D && !E => AndAlso(Equal(Add(A, Multiply(B, C)), D), Not(E))
            var andAlso = Assert.IsType<BinaryNode>(Expr("@(A + B * C == D && !E)"));
            Assert.Equal(ExprOperator.AndAlso, andAlso.Operator);
            var equal = Assert.IsType<BinaryNode>(andAlso.Left);
            Assert.Equal(ExprOperator.Equal, equal.Operator);
            var add = Assert.IsType<BinaryNode>(equal.Left);
            Assert.Equal(ExprOperator.Add, add.Operator);
            Path(add.Left, "A");
            var mul = Assert.IsType<BinaryNode>(add.Right);
            Assert.Equal(ExprOperator.Multiply, mul.Operator);
            Path(equal.Right, "D");
            var not = Assert.IsType<UnaryNode>(andAlso.Right);
            Assert.Equal(ExprOperator.Not, not.Operator);
        }

        [Fact]
        public void P09_CoalesceRightAssociative()
        {
            var outer = Assert.IsType<BinaryNode>(Expr("@(a ?? b ?? c)"));
            Assert.Equal(ExprOperator.Coalesce, outer.Operator);
            Path(outer.Left, "a");
            var inner = Assert.IsType<BinaryNode>(outer.Right);
            Assert.Equal(ExprOperator.Coalesce, inner.Operator);
            Path(inner.Left, "b");
            Path(inner.Right, "c");
        }

        [Fact]
        public void P10_TernaryRightAssociative()
        {
            var outer = Assert.IsType<TernaryNode>(Expr("@(a ? b : c ? d : e)"));
            Path(outer.Condition, "a");
            Path(outer.WhenTrue, "b");
            var inner = Assert.IsType<TernaryNode>(outer.WhenFalse);
            Path(inner.Condition, "c");
        }

        [Fact]
        public void P11_GroupingBeatsPrecedence()
        {
            var mul = Assert.IsType<BinaryNode>(Expr("@((A + B) * C)"));
            Assert.Equal(ExprOperator.Multiply, mul.Operator);
            var add = Assert.IsType<BinaryNode>(mul.Left);
            Assert.Equal(ExprOperator.Add, add.Operator);
            Path(mul.Right, "C");
        }

        [Fact]
        public void P12_RootReferenceArithmetic()
        {
            var b = Assert.IsType<BinaryNode>(Expr("@(::Total - Amount)"));
            Assert.Equal(ExprOperator.Subtract, b.Operator);
            var root = Path(b.Left, "Total");
            Assert.True(root.RootRef);
            Path(b.Right, "Amount");
        }

        [Fact]
        public void P13_FunctionCall()
        {
            var call = Assert.IsType<CallNode>(Expr("@(max(A, B))"));
            Assert.Equal("max", call.Name);
            Assert.Equal(2, call.Arguments.Count);
            Path(call.Arguments[0], "A");
            Path(call.Arguments[1], "B");
        }

        [Fact]
        public void P14_FunctionInComparison()
        {
            var eq = Assert.IsType<BinaryNode>(Expr("@(upper(Name) == X)"));
            Assert.Equal(ExprOperator.Equal, eq.Operator);
            var call = Assert.IsType<CallNode>(eq.Left);
            Assert.Equal("upper", call.Name);
            Path(eq.Right, "X");
        }

        [Fact]
        public void P15_PostfixIndexThenHop()
        {
            var path = Assert.IsType<PathNode>(Expr("@(Items[0].Name)"));
            Assert.Equal(new[] { "Name" }, path.Segments.ToArray());
            var index = Assert.IsType<IndexNode>(path.Target);
            Path(index.Target, "Items");
            Assert.Equal(0, Assert.IsType<LiteralNode>(index.Arguments[0]).Value);
        }

        [Fact]
        public void P16_MultiDimIndex()
        {
            var index = Assert.IsType<IndexNode>(Expr("@(Matrix[1, 2])"));
            Path(index.Target, "Matrix");
            Assert.Equal(2, index.Arguments.Count);
        }

        [Fact]
        public void P17_HexAndBitwiseShift()
        {
            // 0x1F & Flags | 1 << Bits => Or(And(31, Flags), LeftShift(1, Bits))
            var or = Assert.IsType<BinaryNode>(Expr("@(0x1F & Flags | 1 << Bits)"));
            Assert.Equal(ExprOperator.Or, or.Operator);
            var and = Assert.IsType<BinaryNode>(or.Left);
            Assert.Equal(ExprOperator.And, and.Operator);
            Assert.Equal(31, Assert.IsType<LiteralNode>(and.Left).Value);
            var shift = Assert.IsType<BinaryNode>(or.Right);
            Assert.Equal(ExprOperator.LeftShift, shift.Operator);
        }

        [Fact]
        public void P18_LiteralTyping()
        {
            Assert.Equal(1000, Assert.IsType<LiteralNode>(Expr("@(1_000)")).Value);
            Assert.Equal(2L, Assert.IsType<LiteralNode>(Expr("@(2L)")).Value);
            Assert.Equal(1.5f, Assert.IsType<LiteralNode>(Expr("@(1.5f)")).Value);
            Assert.Equal(2.5m, Assert.IsType<LiteralNode>(Expr("@(2.5m)")).Value);
            Assert.Equal(2147483648u, Assert.IsType<LiteralNode>(Expr("@(2147483648)")).Value); // first-fit uint
        }

        [Fact]
        public void P19_CharLiteral()
        {
            var b = Assert.IsType<BinaryNode>(Expr("@('c' + Name)"));
            Assert.Equal('c', Assert.IsType<LiteralNode>(b.Left).Value);
        }

        [Fact]
        public void P20_KeywordParameterBecomesLiteral()
        {
            Assert.Equal(true, Assert.IsType<LiteralNode>(Expr("@out(true)")).Value);
        }

        [Fact]
        public void P21_MethodCallParsesForDiagnostics()
        {
            var method = Assert.IsType<MethodCallNode>(Expr("@(Name.ToUpper())"));
            Assert.Equal("ToUpper", method.Name);
            Path(method.Target, "Name");
        }

        [Fact]
        public void P22_UnaryOverPath()
        {
            var not = Assert.IsType<UnaryNode>(Expr("@(!A.B.C)"));
            Path(not.Operand, "A", "B", "C");
        }

        [Fact]
        public void P23_InteriorWhitespaceHidden()
        {
            var b = Assert.IsType<BinaryNode>(Expr("@( Price  *  Quantity )"));
            Assert.Equal(ExprOperator.Multiply, b.Operator);
        }

        [Fact]
        public void P24_Composite()
        {
            var ternary = Assert.IsType<TernaryNode>(Expr("@(A.B > 0 ? len(Name) : 0)"));
            var gt = Assert.IsType<BinaryNode>(ternary.Condition);
            Assert.Equal(ExprOperator.GreaterThan, gt.Operator);
            Assert.IsType<CallNode>(ternary.WhenTrue);
            Assert.Equal(0, Assert.IsType<LiteralNode>(ternary.WhenFalse).Value);
        }

        // ---- N01–N14: rejected constructs (positioned HED0003) ----

        [Theory]
        [InlineData("@(X = 1)")]        // N01 assignment
        [InlineData("@(X += 1)")]       // N02 compound assignment
        [InlineData("@(X++)")]          // N03 increment
        [InlineData("@(Items.Where(i => i))")] // N04 lambda
        [InlineData("@((Foo) Bar)")]    // N05 cast syntax
        [InlineData("@(x is string)")]  // N06 is
        [InlineData("@(new Foo())")]    // N07 new
        [InlineData("@(a?.b)")]         // N08 ?.
        [InlineData("@($\"x{A}\")")]    // N09 interpolated string
        [InlineData("@(\"\"\"raw\"\"\")")] // N10 raw string
        [InlineData("@(a ? b)")]        // N11 ternary missing :
        [InlineData("@(1 +)")]          // N12 dangling operator
        [InlineData("@(max(1, ))")]     // N13 dangling comma
        [InlineData("@(\"abc\"u8)")]    // N14 UTF-8 suffix
        public void NegativeParseCorpus(string template)
        {
            var ctx = DocumentParser.Parse(template,
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.NotEmpty(ctx.Errors);
            Assert.Contains(ctx.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.SyntaxError);
        }

        // ---- Editor-token classification ----

        private static List<HeddleTokenType> Tokens(string template)
        {
            var ctx = DocumentParser.Parse(template,
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.Empty(ctx.Errors);
            return ctx.Tokens.Select(t => t.HeddleTokenType).ToList();
        }

        [Fact]
        public void OperatorTokenEmitted()
        {
            Assert.Contains(HeddleTokenType.Operator, Tokens("@(A + B)"));
        }

        [Fact]
        public void LiteralTokenEmitted()
        {
            Assert.Contains(HeddleTokenType.Literal, Tokens("@(42)"));
        }

        [Fact]
        public void FunctionNameTokenEmitted()
        {
            Assert.Contains(HeddleTokenType.FunctionName, Tokens("@(max(A, B))"));
        }
    }
}
