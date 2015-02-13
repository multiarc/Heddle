using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Data;

namespace Templates.Editor.Error {
    internal sealed class TtlErrorTagger :ITagger<ErrorTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<TtlTokenTag> _aggregator;

        public TtlErrorTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            _buffer = buffer;
            _aggregator = aggregatorFactory.CreateTagAggregator<TtlTokenTag>(buffer);
        }

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0 || _buffer.CurrentSnapshot.Length == 0) {
                yield break;
            }
            foreach (IMappingTagSpan<TtlTokenTag> tagSpan in _aggregator.GetTags(spans))
            {
                if (tagSpan.Tag.ParserState == State.SyntaxError)
                {
                    var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                    yield return
                        new TagSpan<ErrorTag>(tagSpans[0],
                            new ErrorTag(PredefinedErrorTypeNames.SyntaxError, tagSpan.Tag.ErrorContainer.Message));
                }
                if (tagSpan.Tag.ParserState == State.CompileError)
                {
                    var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                    yield return
                        new TagSpan<ErrorTag>(tagSpans[0],
                            new ErrorTag(PredefinedErrorTypeNames.CompilerError, tagSpan.Tag.ErrorContainer.Message));
                }
                if (tagSpan.Tag.ParserState == State.OtherError)
                {
                    var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                    yield return
                        new TagSpan<ErrorTag>(tagSpans[0],
                            new ErrorTag(PredefinedErrorTypeNames.OtherError, tagSpan.Tag.ErrorContainer.Message));
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
