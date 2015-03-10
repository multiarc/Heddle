using Templates.Collections;

namespace Templates.Language {
    public class OutputItem {
        public OutputItem (string extensionName, ParseContext context, string parameterTemplate = null)
        {
            ExtensionName = extensionName ?? string.Empty;
            ParameterTemplate = parameterTemplate;
            OutList = new SmartList<OutputChain>();
            CallParameter = new CallParameter();
            Context = context;
        }

        public ParseContext Context { get; set; }

        public string ExtensionName { get; }

        public string ParameterTemplate { get; set; }

        public SmartList<OutputChain> OutList { get; private set; }

        public CallParameter CallParameter { get; }
    }
}