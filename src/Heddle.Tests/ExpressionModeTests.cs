using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The <c>AllowCSharp</c> bridge over <see cref="ExpressionMode"/> (D6), the copy-ctor propagation of the
    /// new options, and the <see cref="ExpressionMode.MemberPathsOnly"/> enforcement (HED1014).
    /// </summary>
    public class ExpressionModeTests
    {
        public class Model
        {
            public int A { get; set; }
            public int B { get; set; }
        }

        [Fact]
        public void DefaultModeIsNativeAndAllowCSharpFalse()
        {
            var options = new TemplateOptions();
            Assert.Equal(ExpressionMode.Native, options.ExpressionMode);
#pragma warning disable CS0618 // intentionally exercises the AllowCSharp bridge
            Assert.False(options.AllowCSharp);
#pragma warning restore CS0618
        }

        [Fact]
        public void SetAllowCSharpTrueSelectsFullCSharp()
        {
#pragma warning disable CS0618 // intentionally exercises the AllowCSharp bridge
            var options = new TemplateOptions { AllowCSharp = true };
            Assert.Equal(ExpressionMode.FullCSharp, options.ExpressionMode);
            Assert.True(options.AllowCSharp);
#pragma warning restore CS0618
        }

        [Fact]
        public void SetAllowCSharpFalseFromFullCSharpSelectsNative()
        {
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };
#pragma warning disable CS0618 // intentionally exercises the AllowCSharp bridge
            options.AllowCSharp = false;
#pragma warning restore CS0618
            Assert.Equal(ExpressionMode.Native, options.ExpressionMode);
        }

        [Fact]
        public void SetAllowCSharpFalseDoesNotUpgradeMemberPathsOnly()
        {
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.MemberPathsOnly };
#pragma warning disable CS0618 // intentionally exercises the AllowCSharp bridge
            options.AllowCSharp = false;
#pragma warning restore CS0618
            Assert.Equal(ExpressionMode.MemberPathsOnly, options.ExpressionMode);
        }

        [Fact]
        public void SetAllowCSharpFalseLeavesNativeUntouched()
        {
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.Native };
#pragma warning disable CS0618 // intentionally exercises the AllowCSharp bridge
            options.AllowCSharp = false;
#pragma warning restore CS0618
            Assert.Equal(ExpressionMode.Native, options.ExpressionMode);
        }

        [Fact]
        public void CopyConstructorPropagatesModeAndFunctions()
        {
            var functions = new FunctionRegistry();
            var source = new TemplateOptions
            {
                ExpressionMode = ExpressionMode.MemberPathsOnly,
                Functions = functions
            };
            var copy = new TemplateOptions(source);
            Assert.Equal(ExpressionMode.MemberPathsOnly, copy.ExpressionMode);
            Assert.Same(functions, copy.Functions);
        }

        [Fact]
        public void MemberPathsOnlyRejectsNativeExpressionWithPositionedHed1014()
        {
            HeddleTemplate.Configure(typeof(ExpressionModeTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.MemberPathsOnly };
            var t = new HeddleTemplate("@(A + B)", new CompileContext(options, typeof(Model)));
            Assert.False(t.CompileResult.Success);
            var error = t.CompileResult.Errors.Single(e => e.DiagnosticId == HeddleDiagnosticIds.NativeExpressionsDisabled);
            Assert.Contains("MemberPathsOnly", error.Error);
            Assert.True(error.Position.Length > 0);
        }

        [Fact]
        public void NativeModeCompilesTheSameExpression()
        {
            HeddleTemplate.Configure(typeof(ExpressionModeTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.Native };
            var t = new HeddleTemplate("@(A + B)", new CompileContext(options, typeof(Model)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("7", t.Generate(new Model { A = 3, B = 4 }));
        }

        [Fact]
        public void MemberPathsOnlyStillCompilesPlainMemberPaths()
        {
            HeddleTemplate.Configure(typeof(ExpressionModeTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.MemberPathsOnly };
            var t = new HeddleTemplate("@(A)", new CompileContext(options, typeof(Model)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("5", t.Generate(new Model { A = 5 }));
        }
    }
}
