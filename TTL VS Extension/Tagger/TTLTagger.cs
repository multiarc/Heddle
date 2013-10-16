using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Core.CompilerServices;
using Templates.Core.Data;

namespace TTL.Tagger {
    internal sealed class TTLTagger: ITagger<ClassificationTag> {
        private readonly ITagAggregator<TTLTokenTag> _aggregator;
        private readonly IDictionary<TokenType, IClassificationType> _ttlTypes;

        internal TTLTagger (ITagAggregator<TTLTokenTag> tagAggregator, IClassificationTypeRegistryService typeService)
        {
            _aggregator = tagAggregator;
            _ttlTypes = new Dictionary<TokenType, IClassificationType>();
            _ttlTypes[TokenType.StartTTL] = typeService.GetClassificationType(ParserConfiguration.StartTTL);
            _ttlTypes[TokenType.EndTTL] = typeService.GetClassificationType(ParserConfiguration.EndTTL);
            _ttlTypes[TokenType.StartExtensionsBlock] = typeService.GetClassificationType(ParserConfiguration.StartExtensionsBlock);
            _ttlTypes[TokenType.EndExtensionsBlock] = typeService.GetClassificationType(ParserConfiguration.EndExtensionsBlock);
            _ttlTypes[TokenType.ExtensionDelimeter] = typeService.GetClassificationType(ParserConfiguration.ExtensionDelimeter);
            _ttlTypes[TokenType.StartParameter] = typeService.GetClassificationType(ParserConfiguration.StartParameter);
            _ttlTypes[TokenType.EndParameter] = typeService.GetClassificationType(ParserConfiguration.EndParameter);
            _ttlTypes[TokenType.ValidIdentifier] = typeService.GetClassificationType("ID");
            _ttlTypes[TokenType.TemplateBlock] = typeService.GetClassificationType("Block");
        }

        #region ITagger<ClassificationTag> Members

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags (NormalizedSnapshotSpanCollection spans)
        {
            return (from tagSpan in _aggregator.GetTags(spans)
                    let tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot)
                    select new TagSpan<ClassificationTag>(tagSpans[0], new ClassificationTag(_ttlTypes[tagSpan.Tag.Type])));
        }

        #endregion
    }
}