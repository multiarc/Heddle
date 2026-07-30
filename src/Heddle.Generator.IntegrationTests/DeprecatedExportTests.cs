using System;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime.Expressions;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// An exported host function the <b>consumer's</b> compiler will not let generated code call. Reflection
    /// ignores <c>[Obsolete]</c> entirely, so the engine calls such a function and renders; the generated file
    /// writes the call out as C# into the consumer's own assembly, where <c>[Obsolete(…, error: true)]</c> is
    /// CS0619 and the build stops — on a <c>.g.cs</c> the author cannot edit, over a template that is not at
    /// fault. That is the emitter breaking a build, which is worse than any output difference short of a silent
    /// wrong one.
    /// <para>The emitter already refused to <em>name</em> such a type; the rule had simply never been asked about
    /// the method a call is written to.</para>
    /// </summary>
    public class DeprecatedExportTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private static TemplateOptions OptionsWithExports()
        {
            var options = new TemplateOptions();
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(DeprecatedTemplateFunctions).Assembly);
            options.Functions = registry;
            return options;
        }

        /// <summary>Both positions a call reaches the writer from — a plain expression and an <c>@out</c> slot
        /// value — over both a <c>string</c> return and a class one, because the emitter's paths to the writer
        /// differ and the return type decides which gates the value passes through on the way.</summary>
        [Theory]
        [InlineData("expr-string", "@model(){{" + CartType + "}}@\\\n[@(rgonestr(1))]\n")]
        [InlineData("expr-class", "@model(){{" + CartType + "}}@\\\n[@(rgoneobj(1))]\n")]
        [InlineData("slot-string", "@model(){{" + CartType + "}}@%\n" +
                                   "<s(out:: System.String)>{{[@out(rgonestr(1))]}} :: object\n%@\n@s(){{|@()|}}\n")]
        public void AnExportTheConsumersCompilerRejectsDegradesInsteadOfBreakingTheBuild(string name, string template)
        {
            var key = "views/deprecated-export-" + name + ".heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            DifferentialHarness.ExpectDegrade(gen, key);

            // Reported, not swallowed: the author can act on this one, and the engine still renders it, so the
            // build must not fail over it either.
            var reported = gen.Diagnostics.Where(d => d.Id == "HED7030").ToList();
            Assert.NotEmpty(reported);
            Assert.All(reported, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
            Assert.Contains(reported, d => d.GetMessage().Contains("DeprecatedTemplateFunctions"));

            // The dynamic tier compiles and renders it, which is what makes the degrade a degrade.
            var dynamicTemplate = new HeddleTemplate(template,
                new Heddle.Runtime.CompileContext(OptionsWithExports(), typeof(Cart)));
            Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
            Assert.NotEqual(string.Empty, dynamicTemplate.Generate(new Cart()));
        }

        /// <summary>
        /// The three near neighbours, without which the rule above cannot be told from a refusal of exports, of
        /// <c>[Obsolete]</c>, or of the container.
        /// <list type="bullet">
        /// <item><description>warning-level <c>[Obsolete]</c> — the generated file disables warnings outright, so
        /// it raises nothing in the consumer's build and must cost nothing;</description></item>
        /// <item><description>an export whose <b>return</b> type the consumer may not name — the call site spells
        /// the method, not what it hands back, so this one compiles;</description></item>
        /// <item><description>the same container's undecorated export.</description></item>
        /// </list>
        /// </summary>
        [Theory]
        [InlineData("warn", "rwarnstr(1)", "[ws1]\n")]
        [InlineData("gone-return-type", "rgonereturn(1)",
            "[Heddle.Generator.IntegrationTests.Fixtures.ObsoleteErrorValue]\n")]
        [InlineData("plain", "rokstr(1)", "[os1]\n")]
        public void AnExportTheConsumersCompilerAcceptsStillPrecompiles(string name, string call, string expected)
        {
            var key = "views/deprecated-export-ok-" + name + ".heddle";
            var template = "@model(){{" + CartType + "}}@\\\n[@(" + call + ")]\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart), new Cart(),
                runtimeOptions: OptionsWithExports());
            Assert.Equal(expected, dyn);
            Assert.Equal(dyn, precompiled);
        }
    }
}
