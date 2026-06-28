using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Heddle.Editor.Intellisense {
    [Export(typeof(IQuickInfoSourceProvider))]
    [ContentType("heddle")]
    [Name("heddleQuickInfo")]
    internal sealed class HeddleQuickInfoSourceProvider: IQuickInfoSourceProvider {

        [Import]
        private IBufferTagAggregatorFactoryService _aggService;

        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
            return new HeddleQuickInfoSource(textBuffer, _aggService.CreateTagAggregator<HeddleTokenTag>(textBuffer));
        }
    }

    class HeddleQuickInfoSource: IQuickInfoSource {
        private readonly ITagAggregator<HeddleTokenTag> _aggregator;
        private readonly ITextBuffer _buffer;
        private bool _disposed;


        public HeddleQuickInfoSource(ITextBuffer buffer, ITagAggregator<HeddleTokenTag> aggregator) {
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

            foreach (IMappingTagSpan<HeddleTokenTag> curTag in _aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint))) {
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

