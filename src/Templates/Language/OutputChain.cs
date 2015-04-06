using System.Linq;
using Templates.Collections;
using Templates.Strings.Core;

namespace Templates.Language {
    public class OutputChain {
        internal OutputChain(OutputChain toIsolate, ParseContext newContext, string definitionName) {
            Chain = toIsolate.Chain.Select(item => new OutputItem(item, definitionName)).ToSmartList();
            BlockPosition = toIsolate.BlockPosition;
            Context = newContext;
        }

        public OutputChain(ParseContext context)
        {
            Context = context;
            Chain = new SmartList<OutputItem>();
        }

        public ParseContext Context { get; set; }

        public SmartList<OutputItem> Chain { get; set; }

        public BlockPosition BlockPosition { get; set; }
    }
}