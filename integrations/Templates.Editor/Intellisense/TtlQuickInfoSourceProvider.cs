using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor.Intellisense {
    [Export(typeof(IQuickInfoSourceProvider))]
    [ContentType("ttl")]
    [Name("ttlQuickInfo")]
    internal sealed class TtlQuickInfoSourceProvider: IQuickInfoSourceProvider {

        [Import]
        private IBufferTagAggregatorFactoryService _aggService;

        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
            return new TtlQuickInfoSource(textBuffer, _aggService.CreateTagAggregator<TtlTokenTag>(textBuffer));
        }
    }

    class TtlQuickInfoSource: IQuickInfoSource {
        private readonly ITagAggregator<TtlTokenTag> _aggregator;
        private readonly ITextBuffer _buffer;
        private bool _disposed;


        public TtlQuickInfoSource(ITextBuffer buffer, ITagAggregator<TtlTokenTag> aggregator) {
            _aggregator = aggregator;
            _buffer = buffer;
        }

        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) {
            applicableToSpan = null;

            if (_disposed)
                throw new ObjectDisposedException("TestQuickInfoSource");

            var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(_buffer.CurrentSnapshot);

            if (triggerPoint == null)
                return;

            foreach (IMappingTagSpan<TtlTokenTag> curTag in _aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint))) {
                //TO DO check additional tags for showing quick info
                var tagSpan = curTag.Span.GetSpans(_buffer).First();
                applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                //quickInfoContent.Add("Test quick info!");
            }
        }

        public void Dispose() {
            _disposed = true;
        }
    }
}

