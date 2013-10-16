namespace Templates.Core.Data {
    internal class ExtensionItem {
        internal readonly string ExtensionName;

        internal string ParameterTemplate;

        public ExtensionItem (string extensionName, string parameterTemplate = "")
        {
            ExtensionName = extensionName;
            ParameterTemplate = parameterTemplate;
        }
    }
}