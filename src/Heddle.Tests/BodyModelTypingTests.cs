using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// What a body-hosting built-in actually compiles its body against, and what its body sees on the chained
    /// channel — asserted where it is decidable, in the rendered bytes.
    /// <para>This was once the run-tier half of a table the build kept, and every assertion here read the row it
    /// was checking. The table is gone: a body's typing comes from the extension's own <c>InitStart</c>, which the
    /// build reads off a real engine compile rather than predicting from a name. What survives is the half that
    /// was ever a contract — the output — stated directly instead of through a prediction of it.</para>
    /// </summary>
    public class BodyModelTypingTests
    {
        private sealed class Person
        {
            public string Name { get; set; }
            public int[] Scores { get; set; }
            /// <summary>Always null, so a branch on it is falsy — the continuation/terminal probes need one.</summary>
            public string Other { get; set; }
        }

        private static string Render(string template, object model) =>
            new HeddleTemplate(template, new CompileContext(new TemplateOptions(), model?.GetType()))
                .Generate(model);

        private const string PersonHeader = "@model(){{Heddle.Tests.BodyModelTypingTests+Person}}@\\\n";

        /// <summary>Renders or returns <c>null</c> on compile failure (e.g., reading a nonexistent member).</summary>
        private static string TryRender(string template, object model)
        {
            try
            {
                return Render(template, model);
            }
            catch (Exceptions.TemplateCompileException)
            {
                return null;
            }
        }

        /// <summary>The branch trio and <c>@for</c> compile their bodies against the ENCLOSING model, so a body
        /// reading a member of it renders; <c>@list</c> compiles its body against the element type, where the same
        /// member does not exist and the engine refuses the template.</summary>
        [Theory]
        [InlineData("@if(Name){{@(Name)}}", "Ada")]
        [InlineData("@ifnot(Other){{@(Name)}}", "Ada")]
        [InlineData("@if(Other){{x}}@elif(Name){{@(Name)}}", "Ada")]
        [InlineData("@if(Other){{x}}@elseif(Name){{@(Name)}}", "Ada")]
        [InlineData("@if(Other){{x}}@else(){{@(Name)}}", "Ada")]
        [InlineData("@for(1){{@(Name)}}", "Ada")]
        [InlineData("@list(Scores){{@(Name)}}", null)]
        public void ABodySeesTheEnclosingModelExceptWhereItsHostRetypesIt(string template, string expected)
        {
            Assert.Equal(expected,
                TryRender(PersonHeader + template, new Person { Name = "Ada", Scores = new[] { 7 } }));
        }

        /// <summary><c>@for</c> puts the iteration index on the chained channel, so a bodiless <c>@out()</c> inside
        /// its body splices the index.</summary>
        [Fact]
        public void OutInsideAForBodySplicesTheIterationIndex()
        {
            Assert.Equal("012", Render(PersonHeader + "@for(3){{@out()}}", new Person { Name = "Ada" }));
        }

        /// <summary><c>@list</c> puts the iteration index on the chained channel exactly as <c>@for</c> does —
        /// <c>ListExtension.InitStart</c> hands <c>new ExType(typeof(int))</c> to the body compile and
        /// <c>scope.Model(item, index)</c> puts the index there at render.
        /// <para>The build tier once carried a row claiming otherwise for this one host, and it stayed wrong for as
        /// long as it existed because the emitter read the body column alone. Nothing predicts it now; this renders
        /// it.</para></summary>
        [Fact]
        public void OutInsideAListBodySplicesTheIterationIndex()
        {
            Assert.Equal("012",
                Render(PersonHeader + "@list(Scores){{@out()}}",
                    new Person { Name = "Ada", Scores = new[] { 7, 8, 9 } }));
        }

        /// <summary>A branch body carries nothing host-specific on the chained channel, so <c>@out()</c> inside one
        /// splices nothing.</summary>
        [Theory]
        [InlineData("@if(Name){{[@out()]}}")]
        [InlineData("@if(Other){{x}}@else(){{[@out()]}}")]
        public void OutInsideABranchBodySplicesNothing(string template)
        {
            Assert.Equal("[]", Render(PersonHeader + template, new Person { Name = "Ada" }));
        }

        /// <summary>The same two typings from the reading side: <c>@if</c> reads the enclosing model, <c>@list</c>
        /// reads the element.</summary>
        [Fact]
        public void ABranchBodyReadsTheModelAndAListBodyReadsTheElement()
        {
            var model = new Person { Name = "Ada", Scores = new[] { 7, 9 } };

            Assert.Equal("Ada", Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@if(Name){{@(Name)}}", model));
            Assert.Equal("79", Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@list(Scores){{@(this)}}", model));
        }

        /// <summary><c>@for</c>'s two channels at once: the body sees the enclosing model and <c>@out()</c> splices
        /// the iteration index.</summary>
        [Fact]
        public void AForBodySeesTheModelAndTheIndexTogether()
        {
            var model = new Person { Name = "Ada" };
            Assert.Equal("Ada0Ada1Ada2",
                Render("@model(){{" + typeof(Person).FullName + "}}@\\\n@for(3){{@(Name)@out()}}", model));
        }
    }
}
