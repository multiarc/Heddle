using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using TTL.Tagger;

namespace TTL.Intellisense {
    [Export (typeof (IQuickInfoSourceProvider))]
    [ContentType (Configuration.ContentType)]
    [Name ("ookQuickInfo")]
    internal class OokQuickInfoSourceProvider: IQuickInfoSourceProvider {
        [Import]
        private IBufferTagAggregatorFactoryService aggService;

        #region IQuickInfoSourceProvider Members

        public IQuickInfoSource TryCreateQuickInfoSource (ITextBuffer textBuffer)
        {
            return new OokQuickInfoSource(textBuffer, aggService.CreateTagAggregator<TTLTokenTag>(textBuffer));
        }

        #endregion
    }

    internal class OokQuickInfoSource: IQuickInfoSource {
        private readonly ITagAggregator<TTLTokenTag> _aggregator;
        private readonly ITextBuffer _buffer;
        private bool _disposed;

        public OokQuickInfoSource (ITextBuffer buffer, ITagAggregator<TTLTokenTag> aggregator)
        {
            _aggregator = aggregator;
            _buffer = buffer;
        }

        #region IQuickInfoSource Members

        public void AugmentQuickInfoSession (IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
        {
            applicableToSpan = null;

            if (_disposed)
                throw new ObjectDisposedException("TestQuickInfoSource");

            var triggerPoint = (SnapshotPoint) session.GetTriggerPoint(_buffer.CurrentSnapshot);

            if (triggerPoint == null)
                return;

            foreach (var curTag in _aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint))) {
                //if (curTag.Tag.type == TTLTokenTypes.OokExclaimation)
                //{
                //    var tagSpan = curTag.Span.GetSpans(_buffer).First();
                //    applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                //    quickInfoContent.Add("Exclaimed Ook!");
                //}
            }
        }

        public void Dispose ()
        {
            _disposed = true;
        }

        #endregion
    }
}