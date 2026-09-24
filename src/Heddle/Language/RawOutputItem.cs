using Heddle.Strings.Core;

namespace Heddle.Language {
    public class RawOutputItem {
        public BlockPosition BlockPosition { get; set; }

        public string Text { get; set; }

        /// <summary>The isolation stamp this item was written under; an isolation taken at or before it never
        /// saw the item.</summary>
        internal long CreatedAt { get; } = ParseContext.CurrentIsolationStamp;
    }
}