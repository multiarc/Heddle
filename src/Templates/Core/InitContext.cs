using Templates.Language;
using Templates.Runtime;

namespace Templates.Core
{
    public struct InitContext
    {
        public InitContext(string parameterTemplate, CompileScope compileScope, ParseContext parseContext)
        {
            ParameterTemplate = parameterTemplate;
            CompileScope = compileScope;
            ParseContext = parseContext;
        }

        public string ParameterTemplate;

        public CompileScope CompileScope;

        public ParseContext ParseContext;
    }
}