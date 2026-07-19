using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Heddle oracle for the large-loop workload: the file-based <c>large-loop.heddle</c>
    /// template (<c>@list(Items)</c> over <see cref="LoopContent.RowCount"/> rows) compiled once
    /// against the typed <see cref="LoopContent.LoopModel"/>, rendered raw
    /// (<see cref="OutputProfile.Text"/>) under the default <see cref="ExpressionMode.Native"/>
    /// tier so the comparison measures templating, not encoding.
    /// </summary>
    public sealed class LoopHeddleTest
    {
        private long _length;
        private readonly HeddleTemplate _target;

        public LoopHeddleTest()
        {
            _target = new HeddleTemplate(
                new CompileContext(
                    new TemplateOptions("large-loop")
                    {
                        FileNamePostfix = ".heddle",
                        RootPath = @"TestTemplates",
                        OutputProfile = OutputProfile.Text,
                        ExpressionMode = ExpressionMode.Native,
                        ProvideLanguageFeatures = false
                    },
                    typeof(LoopContent.LoopModel)
                )
            );
        }

        /// <summary>The rendered 5,000-row concatenation, the parity oracle for the loop twins.</summary>
        public string Render() => _target.Generate(LoopContent.Model());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
