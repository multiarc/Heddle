using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Parameters;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The normative operator-semantics table (operator-semantics.md) as <c>[Theory]</c> rows. Every expected
    /// value is produced by the equivalent C# expression over the same model, so a disagreement is a spec
    /// event, not a test fix. Covers promotion pairs, lifted nulls, enum bitwise, string concat, <c>??</c>/
    /// <c>?:</c> typing, equality fallback, char, and constant folding.
    /// </summary>
    public class NativeExpressionOperatorTests
    {
        [Flags]
        public enum Color { None = 0, Red = 1, Green = 2, Blue = 4 }

        public class M
        {
            public int I { get; set; } = 3;
            public long L { get; set; } = 10;
            public double D { get; set; } = 2.5;
            public decimal Dec { get; set; } = 2.5m;
            public float F { get; set; } = 1.5f;
            public uint UI { get; set; } = 4;
            public short Sh { get; set; } = 5;
            public byte By { get; set; } = 6;
            public char C { get; set; } = 'A';
            public bool B { get; set; } = true;
            public string S { get; set; } = "ab";
            public int? NI { get; set; } = null;
            public int? NIv { get; set; } = 7;
            public int Flags { get; set; } = 6;
            public Color EnumA { get; set; } = Color.Red | Color.Green;
            public Color EnumB { get; set; } = Color.Green;
            public object Obj { get; set; } = "ab";
            public DateTime Date { get; set; } = new DateTime(2020, 1, 1);
            public DateTime Date2 { get; set; } = new DateTime(2020, 1, 2);
        }

        private static readonly M Model = new M();

        // The expected values below are deliberately C#-computed, including comparisons the C# compiler
        // constant-folds (a nullable compared to null literal) — that folded result IS the reference value.
#pragma warning disable CS0464, CS0472, CS0458
        public static IEnumerable<object[]> Rows()
        {
            object[] R(string template, object expected) => new[] { template, expected };

            // Numeric promotion pairs.
            yield return R("@(I + L)", 3 + 10L);
            yield return R("@(Dec * I)", 2.5m * 3);
            yield return R("@(D + I)", 2.5 + 3);
            yield return R("@(F + I)", 1.5f + 3);
            yield return R("@(UI + I)", 4u + 3);        // uint + int => long
            yield return R("@(Sh + By)", (short)5 + (byte)6);
            yield return R("@(I - L)", 3 - 10L);
            yield return R("@(L % I)", 10L % 3);
            yield return R("@(I / 2)", 3 / 2);
            yield return R("@(Dec + Dec)", 2.5m + 2.5m);

            // Shift and bitwise (ints).
            yield return R("@(I << 2)", 3 << 2);
            yield return R("@(Flags & 4)", 6 & 4);
            yield return R("@(Flags | 1)", 6 | 1);
            yield return R("@(I ^ 1)", 3 ^ 1);
            yield return R("@(~I)", ~3);

            // Enum bitwise (same enum type).
            yield return R("@(EnumA & EnumB)", (Color.Red | Color.Green) & Color.Green);
            yield return R("@(EnumA | EnumB)", (Color.Red | Color.Green) | Color.Green);
            yield return R("@(~EnumA)", ~(Color.Red | Color.Green));

            // Relational / equality (liftToNull:false).
            yield return R("@(I < L)", 3 < 10L);
            yield return R("@(I == 3)", 3 == 3);
            yield return R("@(I != 3)", 3 != 3);
            yield return R("@(NI < 5)", (int?)null < 5);
            yield return R("@(NI == null)", (int?)null == null);
            yield return R("@(NIv == null)", (int?)7 == null);
            yield return R("@(Date < Date2)", new DateTime(2020, 1, 1) < new DateTime(2020, 1, 2));
            yield return R("@(Obj == S)", Equals((object)"ab", (object)"ab"));

            // Logical and unary.
            yield return R("@(B && true)", true && true);
            yield return R("@(B || false)", true || false);
            yield return R("@(!B)", !true);
            yield return R("@(-I)", -3);
            yield return R("@(-UI)", -(uint)4);         // unary minus on uint => long

            // String concatenation.
            yield return R("@(S + I)", "ab" + 3);
            yield return R("@(S + S)", "ab" + "ab");
            yield return R("@(C + 1)", 'A' + 1);         // char + int => int

            // ?? and ?: typing.
            yield return R("@(NI + 1)", (int?)null + 1);  // lifted -> null
            yield return R("@(NI ?? 0)", (int?)null ?? 0);
            yield return R("@(NIv ?? 0)", (int?)7 ?? 0);
            yield return R("@(NI ?? L)", (long?)(int?)null ?? 10L);
            yield return R("@(B ? I : L)", true ? 3L : 10L);

            // Constant folding (literal-only tree).
            yield return R("@(2 + 2 * 2)", 2 + 2 * 2);
        }
#pragma warning restore CS0464, CS0472, CS0458

        [Theory]
        [MemberData(nameof(Rows))]
        public void MatchesCSharp(string template, object expected)
        {
            var t = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), typeof(M)));
            Assert.True(t.CompileResult.Success, $"{template}: {t.CompileResult}");
            var actual = t.Generate(Model);
            Assert.Equal(Stringify(expected), actual);
        }

        [Fact]
        public void LiteralOnlyTreeFoldsToConstantParameter()
        {
            using var scope = new CompileScope(new CompileContext(new TemplateOptions(), typeof(M)));
            var parseContext = Heddle.Language.DocumentParser.Parse("@(2 + 2 * 2)", scope.CompileContext, out var clean);
            HeddleCompiler.Compile(clean, scope, parseContext, null);
            Assert.Empty(scope.CompileErrors);

            var item = parseContext.OutputChains[0].Chain[0];
            var compiled = scope.CompileContext.CompiledItems[item].CompiledItem;
            Assert.IsType<ConstantParameter>(compiled.Parameter);
        }

        [Fact]
        public void PathBearingTreeCompilesToCompiledParameter()
        {
            using var scope = new CompileScope(new CompileContext(new TemplateOptions(), typeof(M)));
            var parseContext = Heddle.Language.DocumentParser.Parse("@(I + 1)", scope.CompileContext, out var clean);
            HeddleCompiler.Compile(clean, scope, parseContext, null);
            Assert.Empty(scope.CompileErrors);

            var item = parseContext.OutputChains[0].Chain[0];
            var compiled = scope.CompileContext.CompiledItems[item].CompiledItem;
            Assert.IsType<CompiledParameter>(compiled.Parameter);
        }

        private static string Stringify(object value)
        {
            if (value == null)
                return string.Empty;
            if (value is string s)
                return s;
            return value.ToString();
        }
    }
}
