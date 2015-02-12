using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor.Intellisense
{
    [Export(typeof(IQuickInfoSourceProvider))]
    [ContentType("ttl")]
    [Name("ttlQuickInfo")]
    class TtlQuickInfoSourceProvider : IQuickInfoSourceProvider
    {

        [Import]
        IBufferTagAggregatorFactoryService aggService = null;

        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            return new TtlQuickInfoSource(textBuffer, aggService.CreateTagAggregator<TokenTag>(textBuffer));
        }
    }

    class TtlQuickInfoSource : IQuickInfoSource
    {
        private ITagAggregator<TokenTag> _aggregator;
        private ITextBuffer _buffer;
        private bool _disposed = false;


        public TtlQuickInfoSource(ITextBuffer buffer, ITagAggregator<TokenTag> aggregator)
        {
            _aggregator = aggregator;
            _buffer = buffer;
        }

        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;

            if (_disposed)
                throw new ObjectDisposedException("TestQuickInfoSource");

            var triggerPoint = (SnapshotPoint) session.GetTriggerPoint(_buffer.CurrentSnapshot);

            if (triggerPoint == null)
                return;

            foreach (IMappingTagSpan<TokenTag> curTag in _aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint)))
            {
                //TO DO check additional tags for showing quick info
                var tagSpan = curTag.Span.GetSpans(_buffer).First();
                applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                quickInfoContent.Add("Test quick info!");
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}

