using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Heddle oracle for the fragment-heavy workload: the file-based <c>fragment-heavy.heddle</c>
    /// template (48 invocations of one <c>tile</c> definition, each forwarded the current row via
    /// the empty-parameter call) compiled once against the typed
    /// <see cref="FragmentContent.FragmentModel"/>, rendered raw
    /// (<see cref="OutputProfile.Text"/>) under the default <see cref="ExpressionMode.Native"/>
    /// tier so the comparison measures per-call composition overhead, not encoding.
    /// </summary>
    public sealed class FragmentHeddleTest
    {
        private long _length;
        private readonly HeddleTemplate _target;

        public FragmentHeddleTest()
        {
            _target = new HeddleTemplate(
                new CompileContext(
                    new TemplateOptions("fragment-heavy")
                    {
                        FileNamePostfix = ".heddle",
                        RootPath = @"TestTemplates",
                        OutputProfile = OutputProfile.Text,
                        ExpressionMode = ExpressionMode.Native,
                        ProvideLanguageFeatures = false
                    },
                    typeof(FragmentContent.FragmentModel)
                )
            );
        }

        /// <summary>The rendered panel, used as the parity oracle for the fragment twins.</summary>
        public string Render() => _target.Generate(FragmentContent.Model());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
