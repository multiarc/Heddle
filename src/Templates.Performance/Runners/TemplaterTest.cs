using System.Threading.Tasks;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Performance.Runners
{
    public class TemplaterTest
    {
        private long _length = 0;
        private readonly object _model = new object();
        private readonly TtlTemplate _target;

        public TemplaterTest() {
            _target = new TtlTemplate(
                new CompileContext(
                    new TemplateOptions("home") {
                        FileNamePostfix = ".ttl",
                        RootPath = @"TestTemplates",
                        AllowCSharp = true,
                        ForceRemoveWhitespace = true,
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
    }
}