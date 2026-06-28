using System.Collections.Generic;
using System.Linq;
using Heddle.Strings.Core;

namespace Heddle.Language {
    public class OutputChain {
        internal OutputChain(OutputChain toIsolate, ParseContext newContext, string definitionName)
        {
            Chain = toIsolate.Chain?.Select(item => new OutputItem(item, definitionName)).ToList() ?? new List<OutputItem>();
            BlockPosition = toIsolate.BlockPosition;
            Context = newContext;
        }

        public OutputChain(ParseContext context)
        {
            Context = context;
            Chain = new List<OutputItem>();
        }

        public ParseContext Context { get; set; }

        public List<OutputItem> Chain { get; set; }

        public BlockPosition BlockPosition { get; set; }
    }
}