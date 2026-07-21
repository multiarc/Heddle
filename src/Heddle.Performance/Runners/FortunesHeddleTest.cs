using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Heddle oracle for the fortunes-encoded workload: the file-based
    /// <c>fortunes-encoded.heddle</c> table (12 pinned rows including the XSS payload and the
    /// Japanese string) compiled once against the typed
    /// <see cref="FortunesContent.FortuneModel"/>, rendered through the escaping path
    /// (<see cref="OutputProfile.Html"/> — text-context substitutions HTML-encode) under the
    /// default <see cref="ExpressionMode.Native"/> tier so the comparison measures escaping
    /// correctness and cost at micro scale.
    /// </summary>
    public sealed class FortunesHeddleTest
    {
        private long _length;
        private readonly HeddleTemplate _target;

        public FortunesHeddleTest()
        {
            _target = new HeddleTemplate(
                new CompileContext(
                    new TemplateOptions("fortunes-encoded")
                    {
                        FileNamePostfix = ".heddle",
                        RootPath = @"TestTemplates",
                        OutputProfile = OutputProfile.Html,
                        ExpressionMode = ExpressionMode.Native,
                        ProvideLanguageFeatures = false
                    },
                    typeof(FortunesContent.FortuneModel)
                )
            );
        }

        /// <summary>The rendered fortunes table, used as the parity oracle for the fortunes twins.</summary>
        public string Render() => _target.Generate(FortunesContent.Model());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
