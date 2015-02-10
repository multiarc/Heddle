namespace Templates.Data {
    public class ExtensionItem {
        public readonly string ExtensionName;

        public string ParameterTemplate;

        public ExtensionItem (string extensionName, string parameterTemplate = "")
        {
            ExtensionName = extensionName;
            ParameterTemplate = parameterTemplate;
        }
    }
}