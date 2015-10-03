using Templates.Language;
using Templates.Runtime;

namespace Templates.Core
{
    public struct InitContext
    {
        public InitContext(string parameterTemplate, CompileContext context, ParseContext parseContext)
        {
            ParameterTemplate = parameterTemplate;
            Context = context;
            ParseContext = parseContext;
        }

        public string ParameterTemplate;

        public CompileContext Context;

        public ParseContext ParseContext;
    }
}