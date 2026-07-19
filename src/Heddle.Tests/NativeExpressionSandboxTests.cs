using System;
using System.Linq;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The permanent security regression suite (spec §Sandbox invariant, rows S01–S26). Every rejected
    /// construct produces a positioned compile diagnostic and never executes. Booby-trapped members prove
    /// non-execution: they throw / flip a flag if ever reached, and compilation fails before render.
    /// </summary>
    public class NativeExpressionSandboxTests
    {
        public static bool TrapExecuted;

        public class SandboxModel
        {
            public int Count { get; set; }
            public int Value { get; set; }
            public bool Flag { get; set; }
            public string Name { get; set; }
            public ulong U { get; set; }
            public int A { get; set; }
            public int B { get; set; }
            public int X { get; set; }
            public Point Point { get; set; }

            [Hidden]
            public string Secret
            {
                get
                {
                    TrapExecuted = true;
                    throw new InvalidOperationException("hidden getter must never run");
                }
            }
        }

        public class Point { public int Coordinate { get; set; } }

        public static string Trap(int x)
        {
            TrapExecuted = true;
            throw new InvalidOperationException("registered trap must never run");
        }

        private static HeddleCompileResult Compile(string template, ExType modelType = null,
            TemplateOptions options = null)
        {
            HeddleTemplate.Configure(typeof(NativeExpressionSandboxTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template,
                new CompileContext(options ?? new TemplateOptions(), modelType ?? typeof(SandboxModel)));
            return t.CompileResult;
        }

        private static void AssertPositioned(HeddleCompileResult result, string diagnosticId)
        {
            Assert.False(result.Success, "expected a compile error");
            var error = result.Errors.FirstOrDefault(e => e.DiagnosticId == diagnosticId);
            Assert.True(error != null, $"expected {diagnosticId}; got: {result}");
            Assert.True(error.Position.Length > 0, "diagnostic must carry a non-empty position");
        }

        [Fact] public void S01_MethodCall() => AssertPositioned(Compile("@(Name.ToUpper())"), HeddleDiagnosticIds.MethodCallNotAvailable);

        [Fact]
        public void S02_QualifiedMethodCall_SingleError()
        {
            var result = Compile("@(System.IO.File.ReadAllText(\"x\"))");
            AssertPositioned(result, HeddleDiagnosticIds.MethodCallNotAvailable);
            Assert.Single(result.Errors);
        }

        [Fact] public void S03_LiteralMethodCall() => AssertPositioned(Compile("@(\"x\".Substring(0))"), HeddleDiagnosticIds.MethodCallNotAvailable);

        [Fact]
        public void S04_GetTypeUnregistered_NoReflection()
        {
            TrapExecuted = false;
            AssertPositioned(Compile("@(GetType().Assembly)"), HeddleDiagnosticIds.UnknownFunction);
            Assert.False(TrapExecuted);
        }

        [Fact] public void S05_UnregisteredFunction() => AssertPositioned(Compile("@(evil(1))"), HeddleDiagnosticIds.UnknownFunction);

        [Fact] public void S06_ExtensionInsideExpression() => AssertPositioned(Compile("@(if(Flag) + 1)"), HeddleDiagnosticIds.ExtensionCalledAsFunction);

        [Fact] public void S07_Typeof() => AssertPositioned(Compile("@(typeof(string))"), HeddleDiagnosticIds.UnknownFunction);

        [Fact] public void S08_Assignment() => AssertPositioned(Compile("@(X = 1)"), HeddleDiagnosticIds.SyntaxError);

        [Fact] public void S09_Lambda() => AssertPositioned(Compile("@(Items.Where(i => i))"), HeddleDiagnosticIds.SyntaxError);

        [Fact] public void S10_New() => AssertPositioned(Compile("@(new Foo())"), HeddleDiagnosticIds.SyntaxError);

        [Fact] public void S11_LogicalRequiresBool() => AssertPositioned(Compile("@(Count && true)"), HeddleDiagnosticIds.LogicalOperatorRequiresBool);

        [Fact] public void S12_CoalesceLeftNotNullable() => AssertPositioned(Compile("@(Value ?? 0)"), HeddleDiagnosticIds.CoalesceLeftNotNullable);

        [Fact] public void S13_TernaryArmsNoCommonType() => AssertPositioned(Compile("@(Flag ? 1 : \"x\")"), HeddleDiagnosticIds.TernaryArmsNoCommonType);

        [Fact] public void S14_TernaryConditionNotBool() => AssertPositioned(Compile("@(Name ? 1 : 2)"), HeddleDiagnosticIds.TernaryConditionNotBool);

        [Fact] public void S15_BinaryNotDefined() => AssertPositioned(Compile("@(\"a\" - 1)"), HeddleDiagnosticIds.BinaryOperatorNotDefined);

        [Fact] public void S16_UnaryNotDefined() => AssertPositioned(Compile("@(-U)"), HeddleDiagnosticIds.UnaryOperatorNotDefined);

        [Fact] public void S17_IndexerNotFound() => AssertPositioned(Compile("@(Point[\"x\"])"), HeddleDiagnosticIds.IndexerNotFound);

        [Fact]
        public void S18_NoOverloadWithCandidates()
        {
            var result = Compile("@(min(Name, 2))");
            AssertPositioned(result, HeddleDiagnosticIds.NoFunctionOverload);
            Assert.Contains(result.Errors, e => e.Error.Contains("min("));
        }

        [Fact] public void S19_DynamicModel() => AssertPositioned(Compile("@(X > 1)", ExType.Dynamic), HeddleDiagnosticIds.TypedModelRequired);

        [Fact]
        public void S20_HiddenProperty_NeverExecuted()
        {
            TrapExecuted = false;
            AssertPositioned(Compile("@(Secret)"), HeddleDiagnosticIds.PropertyNotFound);
            Assert.False(TrapExecuted);
        }

        [Fact]
        public void S21_HiddenPropertyAsFunctionArg_NeverExecuted()
        {
            TrapExecuted = false;
            AssertPositioned(Compile("@(len(Secret))"), HeddleDiagnosticIds.PropertyNotFound);
            Assert.False(TrapExecuted);
        }

        [Fact] public void S22_MemberPathsOnly() =>
            AssertPositioned(Compile("@(A + B)", options: new TemplateOptions { ExpressionMode = ExpressionMode.MemberPathsOnly }),
                HeddleDiagnosticIds.NativeExpressionsDisabled);

        [Fact]
        public void S23_CSharpTierDisabledUnderNative()
        {
            var result = Compile("@( @Model.X )");
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Error.Contains("C# Code Not allowed here"));
        }

        [Fact] public void S24_FormatArgumentShortfall() => AssertPositioned(Compile("@(format(\"{0}{1}\", X))"), HeddleDiagnosticIds.FormatArgumentCountMismatch);

        [Fact]
        public void S25_FunctionWithChainArguments()
        {
            TrapExecuted = false;
            var registry = new FunctionRegistry();
            registry.Register("evil", (Func<int, string>)(x => "no"));
            var result = Compile("@evil(a():b())", options: new TemplateOptions { Functions = registry });
            AssertPositioned(result, HeddleDiagnosticIds.FunctionRequiresExpressionArguments);
            Assert.False(TrapExecuted);
        }

        [Fact] public void S26_RawString() => AssertPositioned(Compile("@(\"\"\"x\"\"\" + Name)"), HeddleDiagnosticIds.SyntaxError);

        [Fact]
        public void RegisteredTrapNeverRunsForRejectedMethodCall()
        {
            TrapExecuted = false;
            var result = Compile("@(Name.Trap())");
            AssertPositioned(result, HeddleDiagnosticIds.MethodCallNotAvailable);
            Assert.False(TrapExecuted);
        }
    }
}
