using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Tests.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The phase 4 <c>@for</c> counted-loop sugar (D1): an <c>int</c> model iterates 0…n−1 with the ForModel
    /// arm untouched, all rows under <c>AllowCSharp = false</c>. Negative/zero counts render empty; a
    /// non-<c>int</c>/<c>ForModel</c> typed value is a positioned HED0004, not a silent empty render.
    /// </summary>
    public class ForSugarTests
    {
        public class CountModel { public int Count { get; set; } }
        public class NullableCountModel { public int? NullableCount { get; set; } }
        public class NameModel { public string Name { get; set; } }

        private static HeddleTemplate Compile(string template, ExType modelType, bool allowCSharp = false)
        {
            HeddleTemplate.Configure(typeof(ForSugarTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = allowCSharp ? ExpressionMode.FullCSharp : ExpressionMode.Native };
            var t = new HeddleTemplate(template, new CompileContext(options, modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        [Fact] // F01
        public void F01_LiteralCountRendersBody()
        {
            Assert.Equal("xxx", Compile("@for(3){{x}}", typeof(object)).Generate(null));
        }

        [Fact] // F02
        public void F02_IndexRidesTheChainedChannel()
        {
            Assert.Equal("<i>0</i><i>1</i><i>2</i>",
                Compile("@for(3){{<i>@out()</i>}}", typeof(object)).Generate(null));
        }

        [Fact] // F03
        public void F03_TypedIntMemberIterates()
        {
            Assert.Equal("xxxx", Compile("@for(Count){{x}}", typeof(CountModel)).Generate(new CountModel { Count = 4 }));
        }

        [Fact] // F04
        public void F04_ZeroCountRendersEmpty()
        {
            Assert.Equal("", Compile("@for(Count){{x}}", typeof(CountModel)).Generate(new CountModel { Count = 0 }));
        }

        [Fact] // F05
        public void F05_NegativeCountRendersEmpty()
        {
            Assert.Equal("", Compile("@for(-3){{x}}", typeof(object)).Generate(null));
        }

        [Fact] // F06
        public void F06_NullableCountNullEmptyValueIterates()
        {
            Assert.Equal("",
                Compile("@for(NullableCount){{x}}", typeof(NullableCountModel))
                    .Generate(new NullableCountModel { NullableCount = null }));
            Assert.Equal("xx",
                Compile("@for(NullableCount){{x}}", typeof(NullableCountModel))
                    .Generate(new NullableCountModel { NullableCount = 2 }));
        }

        [Fact] // F07
        public void F07_StringMemberIsPositionedTypeError()
        {
            HeddleTemplate.Configure(typeof(ForSugarTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate("@for(Name){{x}}", new CompileContext(new TemplateOptions(), typeof(NameModel)));
            Assert.False(t.CompileResult.Success);
            var error = t.CompileResult.Errors.FirstOrDefault(e => e.DiagnosticId == HeddleDiagnosticIds.ReturnTypeMismatch);
            Assert.True(error != null, t.CompileResult.ToString());
            Assert.Equal("HED0004", HeddleDiagnosticIds.ReturnTypeMismatch);
            Assert.True(error.Position.Length > 0, "HED0004 must carry the call position");
        }

        [Fact] // F08
        public void F08_RangeStartAndStepHonored()
        {
            Assert.Equal("2 5 ", Compile("@for(range(2, 8, 3)){{@out() }}", typeof(object)).Generate(null));
        }

        [Fact] // F09
        public void F09_RangeStartGreaterOrEqualLastRendersEmpty()
        {
            Assert.Equal("", Compile("@for(range(5, 5)){{x}}", typeof(object)).Generate(null));
            Assert.Equal("", Compile("@for(range(7, 3)){{x}}", typeof(object)).Generate(null));
        }

        [Fact] // F10
        public void F10_CSharpTierForModelPathUnchanged()
        {
            var t = Compile("@using(){{Heddle.Models}}@for(@new ForModel() { Last = 2 }){{<i>@out()</i>}}",
                typeof(object), allowCSharp: true);
            Assert.Equal("<i>0</i><i>1</i>", t.Generate(null));
        }

        [Fact] // ergo-for golden (roadmap criterion 1)
        public void ErgoForGolden()
        {
            HeddleTemplate.Configure(typeof(ForSugarTests).GetTypeInfo().Assembly);
            var document = File.ReadAllText("TestTemplate/ergo-for.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document, new CompileContext(new TemplateOptions(), typeof(ErgoForData)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var actual = t.Generate(new ErgoForData { Count = 2 });
            File.WriteAllText("TestTemplate/test-ergo-for.html", actual);
            var expected = File.ReadAllText("TestTemplate/generated-ergo-for.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual);
        }
    }
}
