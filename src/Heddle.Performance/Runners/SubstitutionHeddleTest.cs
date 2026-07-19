using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Heddle oracle for the trivial-substitution workload: the file-based
    /// <c>trivial-substitution.heddle</c> card compiled once against the typed
    /// <see cref="SubstitutionContent.SubstitutionModel"/>, rendered raw
    /// (<see cref="OutputProfile.Text"/>) under the default <see cref="ExpressionMode.Native"/>
    /// tier so the comparison measures templating, not encoding.
    /// </summary>
    public sealed class SubstitutionHeddleTest
    {
        private long _length;
        private readonly HeddleTemplate _target;

        public SubstitutionHeddleTest()
        {
            _target = new HeddleTemplate(
                new CompileContext(
                    new TemplateOptions("trivial-substitution")
                    {
                        FileNamePostfix = ".heddle",
                        RootPath = @"TestTemplates",
                        OutputProfile = OutputProfile.Text,
                        ExpressionMode = ExpressionMode.Native,
                        ProvideLanguageFeatures = false
                    },
                    typeof(SubstitutionContent.SubstitutionModel)
                )
            );
        }

        /// <summary>The rendered card, used as the parity oracle for the substitution twins.</summary>
        public string Render() => _target.Generate(SubstitutionContent.Model());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
