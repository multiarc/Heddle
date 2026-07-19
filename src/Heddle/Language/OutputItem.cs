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
        /// True only for the synthetic unnamed self-call the parser renames to the definition name for a
        /// <c>-&gt; chain</c> default output. Exempts that one call from the HED4002 double-render warning
        /// (it is the definition rendering itself, not a duplicate by-name call). Copied on isolation.
        /// </summary>
        internal bool IsDefaultChainSelfCall { get; set; }

        /// <summary>
        /// True for a non-leading item of a chain (<c>@a():b()</c> — <c>b</c> consumes <c>a</c>'s output).
        /// Phase 5 uses this on <c>@out</c> to fire the slot-projection runtime guard (a slot-mode <c>@out</c>
        /// cannot take a value after a chained call, D11). Copied on isolation.
        /// </summary>
        internal bool IsChainedConsumer { get; set; }

        public ParseContext Context { get; set; }

        public string ExtensionName { get; }

        public string ParameterTemplate { get; set; }

        public CallParameter CallParameter { get; }
    }
}