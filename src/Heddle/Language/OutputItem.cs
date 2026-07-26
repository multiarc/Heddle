using Heddle.Strings.Core;

namespace Heddle.Language {
    public class OutputItem {
        internal OutputItem(OutputItem toIsolate, string definitionName) {
            ExtensionName = toIsolate.ExtensionName;
            ParameterTemplate = toIsolate.ParameterTemplate;
            CallParameter = toIsolate.CallParameter;
            Context = toIsolate.Context?.IsolateContext(definitionName);
            Position = toIsolate.Position;
            IsDefaultChainSelfCall = toIsolate.IsDefaultChainSelfCall;
            IsChainedConsumer = toIsolate.IsChainedConsumer;
        }

        public OutputItem (string extensionName, BlockPosition itemPosition, string parameterTemplate = null)
        {
            ExtensionName = extensionName ?? string.Empty;
            ParameterTemplate = parameterTemplate;
            Position = itemPosition;
            CallParameter = new CallParameter();
        }

        public BlockPosition Position { get; set; }

        /// <summary>
        /// True for the synthetic unnamed self-call in a <c>-&gt; chain</c> default output;
        /// exempts it from the HED4002 double-render warning. Copied on isolation.
        /// </summary>
        internal bool IsDefaultChainSelfCall { get; set; }

        /// <summary>
        /// True for a non-leading chain item; fires the slot-projection runtime guard on <c>@out</c>.
        /// Copied on isolation.
        /// </summary>
        internal bool IsChainedConsumer { get; set; }

        public ParseContext Context { get; set; }

        public string ExtensionName { get; }

        public string ParameterTemplate { get; set; }

        public CallParameter CallParameter { get; }
    }
}