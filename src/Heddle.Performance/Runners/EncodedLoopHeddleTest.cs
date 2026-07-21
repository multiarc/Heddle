using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    /// <summary>
    /// Heddle oracle for the encoded-loop workload: the file-based <c>encoded-loop.heddle</c>
    /// template (5,000 rows with escapables in every cell; <c>@attr</c> in the attribute-value
    /// position, plain <c>@(…)</c> in text positions) compiled once against the typed
    /// <see cref="EncodedLoopContent.EncodedLoopModel"/>, rendered through the escaping path
    /// (<see cref="OutputProfile.Html"/>) under the default <see cref="ExpressionMode.Native"/>
    /// tier so the comparison measures escaping throughput at scale.
    /// </summary>
    public sealed class EncodedLoopHeddleTest
    {
        private long _length;
        private readonly HeddleTemplate _target;

        public EncodedLoopHeddleTest()
        {
            _target = new HeddleTemplate(
                new CompileContext(
                    new TemplateOptions("encoded-loop")
                    {
                        FileNamePostfix = ".heddle",
                        RootPath = @"TestTemplates",
                        OutputProfile = OutputProfile.Html,
                        ExpressionMode = ExpressionMode.Native,
                        ProvideLanguageFeatures = false
                    },
                    typeof(EncodedLoopContent.EncodedLoopModel)
                )
            );
        }

        /// <summary>The rendered 5,000-row table, the parity oracle for the encoded-loop twins.</summary>
        public string Render() => _target.Generate(EncodedLoopContent.Model());

        public Task Run()
        {
            _length += Render().Length;
            return Task.CompletedTask;
        }
    }
}
