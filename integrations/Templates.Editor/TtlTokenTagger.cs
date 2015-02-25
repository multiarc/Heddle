using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Data;
using Templates.Editor.Error;
using Templates.Exceptions;
using Templates.Language;

namespace Templates.Editor {
    internal sealed class TtlTokenTagger: ITagger<TtlTokenTag> {
        private ITextSnapshot _snapshot;
        private readonly List<ITagSpan<TtlTokenTag>> _tags;

        internal TtlTokenTagger(ITextBuffer buffer) {
            _snapshot = buffer.CurrentSnapshot;
            _tags = new List<ITagSpan<TtlTokenTag>>();
            GetTags();
        }

        public IEnumerable<ITagSpan<TtlTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (_snapshot == spans[0].Snapshot)
                return _tags;
            _snapshot = spans[0].Snapshot;
            _tags.Clear();
            GetTags();
            return _tags;
        }

        private void GetTags() {
            
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
