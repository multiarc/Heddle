namespace Templates.Language {
    public class OutputItem {
        internal OutputItem(OutputItem toIsolate, string definitionName) {
            ExtensionName = toIsolate.ExtensionName;
            ParameterTemplate = toIsolate.ParameterTemplate;
            CallParameter = toIsolate.CallParameter;
            Context = toIsolate.Context?.IsolateContext(definitionName);
        }

        public OutputItem (string extensionName, string parameterTemplate = null)
        {
            ExtensionName = extensionName ?? string.Empty;
            ParameterTemplate = parameterTemplate;
            CallParameter = new CallParameter();
        }

        public ParseContext Context { get; set; }

        public string ExtensionName { get; }

        public string ParameterTemplate { get; set; }

        public CallParameter CallParameter { get; }
    }
}