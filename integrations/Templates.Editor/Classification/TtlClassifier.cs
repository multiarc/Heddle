using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Language;

namespace Templates.Editor.Classification {
    internal sealed class TtlClassifier: ITagger<ClassificationTag> {
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<TtlTokenTag> _aggregator;
        private readonly IDictionary<TtlTokenType, IClassificationType> _tokenTypes;
        public TtlClassifier(ITextBuffer buffer, ITagAggregator<TtlTokenTag> tagAggregator, IClassificationTypeRegistryService typeService)
        {
            _buffer = buffer;
            _aggregator = tagAggregator;
            _tokenTypes = new Dictionary<TtlTokenType, IClassificationType>
            {
                {TtlTokenType.DefStart, typeService.GetClassificationType("StartTtl")},
                {TtlTokenType.DefClose, typeService.GetClassificationType("EndTtl")},
                {TtlTokenType.DefStartName, typeService.GetClassificationType("StartExtensionsBlock")},
                {TtlTokenType.DefEndName, typeService.GetClassificationType("EndExtensionsBlock")},
                {TtlTokenType.Id, typeService.GetClassificationType("Identifier")},
                {TtlTokenType.Delim, typeService.GetClassificationType("ExtensionDelimeter")},
                {TtlTokenType.OutParamStart, typeService.GetClassificationType("LParen")},
                {TtlTokenType.OutParamEnd, typeService.GetClassificationType("RParen")},
                {TtlTokenType.Comment, typeService.GetClassificationType("Comment")},
                {TtlTokenType.SubStart, typeService.GetClassificationType("StartSubextension")},
                {TtlTokenType.SubClose, typeService.GetClassificationType("EndSubextension")},
                {TtlTokenType.ParseError, typeService.GetClassificationType("ParseError")},
                {TtlTokenType.CSharpStart, typeService.GetClassificationType("CSharpStart")},
                {TtlTokenType.CSharpToken, typeService.GetClassificationType("CSharpStart")},
                {TtlTokenType.DefOutputOnEnd, typeService.GetClassificationType("DefaultOut")},
                {TtlTokenType.DefType, typeService.GetClassificationType("DefinitionType")},
                {TtlTokenType.LineTerminate, typeService.GetClassificationType("Text")},
                {TtlTokenType.Out, typeService.GetClassificationType("OutStart")},
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
