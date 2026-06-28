using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Heddle.Editor.Classification;
using Heddle.Language;

namespace Heddle.Editor.Error {
    internal sealed class HeddleErrorTagger :ITagger<ErrorTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<HeddleTokenTag> _aggregator;

        public HeddleErrorTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            _buffer = buffer;
            _aggregator = aggregatorFactory.CreateTagAggregator<HeddleTokenTag>(buffer);
        }

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0 || _buffer.CurrentSnapshot.Length == 0) {
                yield break;
            }
            foreach (IMappingTagSpan<HeddleTokenTag> tagSpan in _aggregator.GetTags(spans))
            {
                if (tagSpan.Tag.Type == HeddleTokenType.ParseError)
                {
                    var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                    yield return
                        new TagSpan<ErrorTag>(tagSpans[0],
                            new ErrorTag(PredefinedErrorTypeNames.SyntaxError, tagSpan.Tag.Error));
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
