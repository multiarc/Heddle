using System;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The guard that stops a definition calling itself forever. Its counter is per thread and lives on the compiled
    /// extension, which is cached and reused for the life of the process — so what the counter does on the way out
    /// of a failed render decides whether the template still works afterwards.
    /// </summary>
    public sealed class RecursionProbeModel
    {
        public bool Explode { get; set; }

        public string Value => Explode ? throw new InvalidOperationException("boom") : "ok";
    }

    public class DefinitionRecursionTests
    {
        private const string Template =
            "@%<card>{{@(Value)}} :: Heddle.Tests.RecursionProbeModel%@\\\n@card()";

        /// <summary>
        /// A definition body can fail for entirely ordinary reasons, and the failure is the caller's to handle. If
        /// the recursion counter is not given back on the way out, each failure spends one of a fixed budget and
        /// never returns it: after enough of them the template answers every request — including healthy ones — with
        /// a recursion error describing nothing that happened.
        /// </summary>
        [Fact]
        public void AFailedRenderDoesNotSpendTheRecursionBudget()
        {
            var template = new HeddleTemplate(Template, new CompileContext(typeof(RecursionProbeModel)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            // What the template renders when nothing is wrong, taken before the failures rather than written down
            // here: the assertion is that the failures changed nothing, not what the output happens to look like.
            var healthy = template.Generate(new RecursionProbeModel { Explode = false });

            var options = new TemplateOptions();
            for (var i = 0; i < options.MaxRecursionCount * 2; i++)
                Assert.ThrowsAny<Exception>(() => template.Generate(new RecursionProbeModel { Explode = true }));

            Assert.Equal(healthy, template.Generate(new RecursionProbeModel { Explode = false }));
        }

        /// <summary>The guard itself still fires — giving the budget back on failure must not give it back on the
        /// way down a genuine recursion.</summary>
        [Fact]
        public void ASelfCallingDefinitionIsStoppedRatherThanRunningOutOfStack()
        {
            const string recursive = "@%<loop>{{@loop()}}%@\\\n@loop()";
            var template = new HeddleTemplate(recursive, new CompileContext(new TemplateOptions(), typeof(object)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            var error = Assert.Throws<TemplateProcessingException>(() => template.Generate(null));
            Assert.Contains("Recursion", error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
