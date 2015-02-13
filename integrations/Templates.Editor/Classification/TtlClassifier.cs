using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Data;

namespace Templates.Editor.Classification {
    internal sealed class TtlClassifier: ITagger<ClassificationTag> {
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<TtlTokenTag> _aggregator;
        private readonly IDictionary<TokenType, IClassificationType> _tokenTypes;
        public TtlClassifier(ITextBuffer buffer, ITagAggregator<TtlTokenTag> tagAggregator, IClassificationTypeRegistryService typeService)
        {
            _buffer = buffer;
            _aggregator = tagAggregator;
            _tokenTypes = new Dictionary<TokenType, IClassificationType>
            {
                {TokenType.StartTtl, typeService.GetClassificationType("StartTtl")},
                {TokenType.EndTtl, typeService.GetClassificationType("EndTtl")},
                {TokenType.StartExtensionsBlock, typeService.GetClassificationType("StartExtensionsBlock")},
                {TokenType.EndExtensionsBlock, typeService.GetClassificationType("EndExtensionsBlock")},
                {TokenType.ValidIdentifier, typeService.GetClassificationType("ValidIdentifier")},
                {TokenType.ExtensionDelimeter, typeService.GetClassificationType("ExtensionDelimeter")},
                {TokenType.StartParameter, typeService.GetClassificationType("StartParameter")},
                {TokenType.EndParameter, typeService.GetClassificationType("EndParameter")},
                {TokenType.Space, typeService.GetClassificationType("Space")}
            };
        }

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return (_aggregator.GetTags(spans)
                .Select(tagSpan => new {tagSpan, tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot)})
                .Select(
                    @t =>
                        new TagSpan<ClassificationTag>(@t.tagSpans[0],
                            new ClassificationTag(_tokenTypes[@t.tagSpan.Tag.Type]))));
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
