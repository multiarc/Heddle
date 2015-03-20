using Templates.Collections;
using Templates.Strings.Core;

namespace Templates.Language {
    public class OutputChain {
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