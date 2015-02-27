using Templates.Collections;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Language {
    public class OutputChain {
        public OutputChain()
        {
            Context = new ParseContext();
            Chain = new SmartList<OutputItem>();
        }

        public ParseContext Context { get; set; }

        public SmartList<OutputItem> Chain { get; set; }

        public BlockPosition BlockPosition { get; set; }
    }
}