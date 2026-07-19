using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Performance.Runners
{
    public class HeddleTest
    {
        private long _length = 0;
        private readonly object _model = new object();
        private readonly HeddleTemplate _target;

        public HeddleTest() {
            _target = new HeddleTemplate(
                new CompileContext(
                    new TemplateOptions("home") {
                        FileNamePostfix = ".heddle",
                        RootPath = @"TestTemplates",
                        ExpressionMode = ExpressionMode.FullCSharp,
                        ProvideLanguageFeatures = false
                    }
                )
            );
        }

        public Task Run() {
            var output = _target.Generate(_model);
            _length += output.Length;
            return Task.CompletedTask;
        }

        /// <summary>The rendered home page, used as the parity oracle for the D1 competitor twins.</summary>
        public string Render() => _target.Generate(_model);
    }
}