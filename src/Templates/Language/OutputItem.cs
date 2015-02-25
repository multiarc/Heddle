using Templates.Collections;

namespace Templates.Language {
    public class OutputItem {
        public OutputItem (string extensionName, string parameterTemplate = "")
        {
            ExtensionName = extensionName;
            ParameterTemplate = parameterTemplate;
            OutList = new SmartList<OutputChain>();
            CallParameter = new CallParameter();
        }

        public string ExtensionName { get; }

        public string ParameterTemplate { get; set; }

        public SmartList<OutputChain> OutList { get; private set; }

        public CallParameter CallParameter { get; }
    }
}