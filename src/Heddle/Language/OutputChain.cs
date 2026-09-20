using System.Collections.Generic;
using System.Linq;
using Heddle.Strings.Core;

namespace Heddle.Language {
    public class OutputChain {
        internal OutputChain(OutputChain toIsolate, ParseContext newContext, string definitionName)
        {
            Chain = toIsolate.Chain?.Select(item => new OutputItem(item, definitionName)).ToList() ?? new List<OutputItem>();
            _blockPosition = toIsolate.PositionAsOf(ParseContext.IsolatingAsOf);
            _positionStamp = ParseContext.IsolatingAsOf;
            CreatedAt = toIsolate.CreatedAt;
            Context = newContext;
        }

        public OutputChain(ParseContext context)
        {
            Context = context;
            Chain = new List<OutputItem>();
            CreatedAt = _positionStamp = ParseContext.CurrentIsolationStamp;
        }

        public ParseContext Context { get; set; }

        public List<OutputItem> Chain { get; set; }

        public BlockPosition BlockPosition
        {
            get { return _blockPosition; }
            set
            {
                // A position an isolation may have seen is kept for it: an isolated context copies its chains
                // when they are first read, and a compile, or an import taking the chain over, moves them first.
                long now = ParseContext.CurrentIsolationStamp;
                if (now != _positionStamp)
                {
                    lock (ParseContext.SharedLock)
                    {
                        (_earlierPositions ?? (_earlierPositions = new List<KeyValuePair<long, BlockPosition>>()))
                            .Add(new KeyValuePair<long, BlockPosition>(now, _blockPosition));
                    }

                    _positionStamp = now;
                }

                _blockPosition = value;
            }
        }

        private BlockPosition _blockPosition;
        private long _positionStamp;
        private List<KeyValuePair<long, BlockPosition>> _earlierPositions;

        /// <summary>The isolation stamp this chain was written under; an isolation taken at or before it never
        /// saw the chain. A copy carries its source's.</summary>
        internal long CreatedAt { get; }

        /// <summary>Where the chain was for an isolation stamped <paramref name="stamp"/>. Called under the shared lock.</summary>
        internal BlockPosition PositionAsOf(long stamp)
        {
            if (_earlierPositions != null)
            {
                foreach (var earlier in _earlierPositions)
                {
                    if (earlier.Key >= stamp)
                        return earlier.Value;
                }
            }

            return _blockPosition;
        }
    }
}
