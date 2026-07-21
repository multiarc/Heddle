using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Heddle oracle for the mixed-page workload: the file-based <c>mixed-page.heddle</c>
    /// realistic mid-size page (literal skeleton + scalar substitutions + 36-product loop +
    /// page- and row-level conditionals) compiled once against the typed
    /// <see cref="MixedContent.MixedModel"/>, rendered raw (<see cref="OutputProfile.Text"/>)
    /// under the default <see cref="ExpressionMode.Native"/> tier so the comparison measures
    /// templating, not encoding.
    /// </summary>
    public sealed class MixedHeddleTest
    {
        private long _length;
        private readonly HeddleTemplate _target;

        public MixedHeddleTest()
        {
            _target = new HeddleTemplate(
                new CompileContext(
                    new TemplateOptions("mixed-page")
                    {
                        FileNamePostfix = ".heddle",
                        RootPath = @"TestTemplates",
                        OutputProfile = OutputProfile.Text,
                        ExpressionMode = ExpressionMode.Native,
                        ProvideLanguageFeatures = false
                    },
                    typeof(MixedContent.MixedModel)
                )
            );
        }

        /// <summary>The rendered page, used as the parity oracle for the mixed-page twins.</summary>
        public string Render() => _target.Generate(MixedContent.Model());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
