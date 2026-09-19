using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    public class AgreementLine
    {
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class AgreementOrder
    {
        public int Count { get; set; } = 5;
        public long Big { get; set; } = 5000000000L;
        public double Ratio { get; set; } = 1.5;
        public decimal Total { get; set; } = 12.50m;
        public bool Flag { get; set; } = true;
        public bool Off { get; set; }
        public char Mark { get; set; } = 'x';
        public int? Missing { get; set; }
        public int? Present { get; set; } = 7;
        public DayOfWeek Day { get; set; } = DayOfWeek.Friday;
        public string Name { get; set; } = "Ada";
        public List<int> Numbers { get; set; } = new List<int> { 1, 2, 3 };
        public IEnumerable<string> Letters { get; set; } =
            System.Linq.Enumerable.Where(new[] { "a", "b" }, l => l != null);
        public List<AgreementLine> Lines { get; set; } = new List<AgreementLine>
        {
            new AgreementLine { Quantity = 2, Price = 1.25m },
            new AgreementLine { Quantity = 4, Price = 0.5m }
        };
    }

    /// <summary>A body is either rendered into the sink or processed to a string and spliced in by its owner
    /// (a definition's caller content reaching <c>@out()</c>, the right-hand call of a chain). Both must produce
    /// the same text. Pins the regression where the unnamed/<c>@raw</c> carrier handed its boxed model value
    /// down the process path: the document strategies concatenate strings, so every non-string value vanished
    /// from processed bodies in Release and tripped the return-type guard in Debug — and a standalone function
    /// call declared its own return type as the chained type while its carrier handed over text.</summary>
    public class ProcessPathAgreementTests
    {
        /// <summary>One row per consumer of a processed body, each reading non-string values.</summary>
        public static IEnumerable<object[]> Bodies()
        {
            yield return new object[] { "n=@raw(Count)" };
            yield return new object[] { "n=@(Count)" };
            yield return new object[] { "@(Big)|@(Ratio)|@(Total)|@(Flag)|@(Mark)|@(Missing)|@(Present)|@(Day)" };
            yield return new object[] { "@raw(Big)|@raw(Ratio)|@raw(Total)|@raw(Flag)|@raw(Mark)|@raw(Missing)|@raw(Present)|@raw(Day)" };
            yield return new object[] { "@(Count + 1)|@(Count * Ratio)|@(Count > 3)" };
            yield return new object[] { "@list(Numbers){{<@(this)>}}" };
            yield return new object[] { "@list(Numbers){{<@raw(this)>}}" };
            yield return new object[] { "@list(Lines){{@(Quantity)x@(Price);}}" };
            yield return new object[] { "@for(3){{[@(Count)]}}" };
            yield return new object[] { "@if(Flag){{yes @(Count)}}@else(){{no @(Count)}}" };
            yield return new object[] { "@if(Off){{yes @(Count)}}@else(){{no @(Count)}}" };
            yield return new object[] { "@ifnot(Off){{not @(Ratio)}}" };
            yield return new object[] { "@len(Name)|@upper(Name)" };
            yield return new object[] { "@inner(){{deep @(Count)}}" };
            yield return new object[] { "@inner():len(Name)" };
        }

        /// <summary>Calls with no body of their own. A bodiless call has no inner result at all — not an empty
        /// one — and every consumer has to survive that on the process path the way it does on the render path.
        /// In a chain the body belongs to the leftmost call, so every call to its right is bodiless too.</summary>
        public static IEnumerable<object[]> BodilessCalls()
        {
            yield return new object[] { "@list(Numbers)" };
            yield return new object[] { "@list(Lines)" };
            yield return new object[] { "@list(Letters)" };
            yield return new object[] { "@for(3)" };
            yield return new object[] { "@if(Flag)" };
            yield return new object[] { "@if(Off)@else()" };
            yield return new object[] { "@ifnot(Off)" };
            yield return new object[] { "@inner()" };
            yield return new object[] { "@inner():list(Numbers)" };
            yield return new object[] { "@raw(Name):list(Numbers)" };
            yield return new object[] { "@upper(Name):list(Numbers)" };
            yield return new object[] { "@(Name):list(Numbers){{@(this)}}" };
            yield return new object[] { "@len(Name):list(Numbers){{@(this)}}" };
            yield return new object[] { "@len(Name):list(Lines){{<@(this)>}}" };
            yield return new object[] { "@len(Name):list(Letters){{@(this)}}" };
            yield return new object[] { "@len(Name):for(2){{x}}" };
            yield return new object[] { "@len(Name):if(Flag){{x}}" };
            yield return new object[] { "@len(Name):ifnot(Off){{x}}" };
            yield return new object[] { "@inner():inner():list(Numbers)" };
        }

        [Theory]
        [MemberData(nameof(BodilessCalls))]
        public void BodilessCallProcessesToWhatItRenders(string body)
        {
            foreach (var profile in new[] { OutputProfile.Text, OutputProfile.Html })
            {
                var model = new AgreementOrder();
                string rendered = Compile(Definitions + body, profile).Generate(model);
                string processed = Compile(Definitions + "@box(){{" + body + "}}", profile).Generate(model);
                Assert.Equal("[" + rendered + "]", processed);
            }
        }

        [Fact]
        public void BodilessListToTheRightOfAChainedCallContributesNothing()
        {
            Assert.Equal("3", Compile("@len(Name):list(Numbers){{@(this)}}", OutputProfile.Text)
                .Generate(new AgreementOrder()));
            Assert.Equal("()", Compile(Definitions + "@inner():list(Numbers)", OutputProfile.Text)
                .Generate(new AgreementOrder()));
            Assert.Equal("[]", Compile(Definitions + "@box(){{@list(Numbers)}}", OutputProfile.Text)
                .Generate(new AgreementOrder()));
        }

        private const string Definitions =
            "@% <box>{{[@out()]}} <inner>{{(@out())}} %@";

        private static HeddleTemplate Compile(string document, OutputProfile profile,
            ExpressionMode mode = ExpressionMode.Native)
        {
            HeddleTemplate.Configure(typeof(ProcessPathAgreementTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { OutputProfile = profile, ExpressionMode = mode };
            var template = new HeddleTemplate(document, new CompileContext(options, typeof(AgreementOrder)));
            Assert.True(template.CompileResult.Success, document + "\n" + template.CompileResult);
            return template;
        }

        [Theory]
        [MemberData(nameof(Bodies))]
        public void ProcessedBodyEqualsRenderedBodyInTheTextProfile(string body) =>
            AssertAgreement(body, OutputProfile.Text);

        [Theory]
        [MemberData(nameof(Bodies))]
        public void ProcessedBodyEqualsRenderedBodyInTheHtmlProfile(string body) =>
            AssertAgreement(body, OutputProfile.Html);

        private static void AssertAgreement(string body, OutputProfile profile)
        {
            var model = new AgreementOrder();
            string rendered = Compile(Definitions + body, profile).Generate(model);
            Assert.False(string.IsNullOrEmpty(rendered), "the rendered body produced nothing: " + body);

            string processed = Compile(Definitions + "@box(){{" + body + "}}", profile).Generate(model);
            Assert.Equal("[" + rendered + "]", processed);

            string twice = Compile(Definitions + "@box(){{@box(){{" + body + "}}}}", profile).Generate(model);
            Assert.Equal("[[" + rendered + "]]", twice);
        }

        [Fact]
        public void ProcessedNumberRendersTheReportedValue()
        {
            Assert.Equal("[n=5]", Compile("@% <box>{{[@out()]}} %@@box(){{n=@raw(Count)}}", OutputProfile.Html)
                .Generate(new AgreementOrder()));
            Assert.Equal("[n=5]", Compile("@% <box>{{[@out()]}} %@@box(){{n=@(Count)}}", OutputProfile.Text)
                .Generate(new AgreementOrder()));
        }

        [Fact]
        public void ProcessedValueUsesTheSameCultureAsTheRenderedValue()
        {
            var saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                string rendered = Compile("@(Ratio)|@(Total)", OutputProfile.Text).Generate(new AgreementOrder());
                string processed = Compile(Definitions + "@box(){{@(Ratio)|@(Total)}}", OutputProfile.Text)
                    .Generate(new AgreementOrder());
                Assert.Equal("[" + rendered + "]", processed);
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Theory]
        [InlineData(ExpressionMode.Native, OutputProfile.Text)]
        [InlineData(ExpressionMode.Native, OutputProfile.Html)]
        [InlineData(ExpressionMode.FullCSharp, OutputProfile.Text)]
        [InlineData(ExpressionMode.FullCSharp, OutputProfile.Html)]
        public void FunctionCallOnTheRightOfAChainHandsOverItsText(ExpressionMode mode, OutputProfile profile)
        {
            var template = Compile("@% <w>{{[@out()]}} %@@w():len(Name)|@w():upper(Name)", profile, mode);
            Assert.Equal("[3]|[ADA]", template.Generate(new AgreementOrder()));
        }

        [Fact]
        public void CSharpConsumerOfAChainedFunctionCallSeesTheDeclaredChainedType()
        {
            // The C# tier binds `chained` to the declared chained type: declared and delivered must agree, or the
            // generated cast throws InvalidCastException out of Generate.
            var template = Compile("@(@ chained ):len(Name)|@(@ chained.Length ):len(Name)", OutputProfile.Text,
                ExpressionMode.FullCSharp);
            Assert.Equal("3|1", template.Generate(new AgreementOrder()));
        }

        [Fact]
        public void FunctionCallInAChainUnderMemberPathsOnlyIsADiagnosticNotAnException()
        {
            HeddleTemplate.Configure(typeof(ProcessPathAgreementTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                OutputProfile = OutputProfile.Text,
                ExpressionMode = ExpressionMode.MemberPathsOnly
            };
            var template = new HeddleTemplate("@% <w>{{[@out()]}} %@@w():len(Name)",
                new CompileContext(options, typeof(AgreementOrder)));
            Assert.False(template.CompileResult.Success);
            Assert.NotEmpty(template.CompileResult.ErrorList);
        }

        [Fact]
        public void ChainedTypeDeclaredForAFunctionCallIsTheCarrierText()
        {
            var template = Compile("@% <w>{{@out()}} %@@w():len(Name)", OutputProfile.Text);
            Assert.Equal("3", template.Generate(new AgreementOrder()));
            Assert.Equal("3", Compile("@len(Name)", OutputProfile.Text).Generate(new AgreementOrder()));
        }
    }
}
