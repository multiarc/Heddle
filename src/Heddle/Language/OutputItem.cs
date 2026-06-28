using Heddle.Strings.Core;

namespace Heddle.Language {
    public class OutputItem {
        internal OutputItem(OutputItem toIsolate, string definitionName) {
            ExtensionName = toIsolate.ExtensionName;
            ParameterTemplate = toIsolate.ParameterTemplate;
            CallParameter = toIsolate.CallParameter;
            Context = toIsolate.Context?.IsolateContext(definitionName);
            Position = toIsolate.Position;
        }

        public OutputItem (string extensionName, BlockPosition itemPosition, string parameterTemplate = null)
        {
            ExtensionName = extensionName ?? string.Empty;
            ParameterTemplate = parameterTemplate;
            Position = itemPosition;
            CallParameter = new CallParameter();
        }

        public BlockPosition Position { get; set; }

        public ParseContext Context { get; set; }

        public string ExtensionName { get; }

        public string ParameterTemplate { get; set; }

        public CallParameter CallParameter { get; }
    }
}