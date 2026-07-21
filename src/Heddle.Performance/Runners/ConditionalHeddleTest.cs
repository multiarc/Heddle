using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Heddle oracle for the conditional-heavy workload: the file-based
    /// <c>conditional-heavy.heddle</c> template (200 rows, each one four-way tier chain plus two
    /// toggles on precomputed booleans) compiled once against the typed
    /// <see cref="ConditionalContent.ConditionalModel"/>, rendered raw
    /// (<see cref="OutputProfile.Text"/>) under the default <see cref="ExpressionMode.Native"/>
    /// tier so the comparison measures branch dispatch, not encoding.
    /// </summary>
    public sealed class ConditionalHeddleTest
    {
        private long _length;
        private readonly HeddleTemplate _target;

        public ConditionalHeddleTest()
        {
            _target = new HeddleTemplate(
                new CompileContext(
                    new TemplateOptions("conditional-heavy")
                    {
                        FileNamePostfix = ".heddle",
                        RootPath = @"TestTemplates",
                        OutputProfile = OutputProfile.Text,
                        ExpressionMode = ExpressionMode.Native,
                        ProvideLanguageFeatures = false
                    },
                    typeof(ConditionalContent.ConditionalModel)
                )
            );
        }

        /// <summary>The rendered matrix, used as the parity oracle for the conditional twins.</summary>
        public string Render() => _target.Generate(ConditionalContent.Model());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
