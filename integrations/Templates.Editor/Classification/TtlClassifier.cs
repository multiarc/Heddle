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
        private readonly IClassificationType[] _tokenTypes;

        public TtlClassifier(ITextBuffer buffer, ITagAggregator<TtlTokenTag> tagAggregator, IClassificationTypeRegistryService typeService)
        {
            _buffer = buffer;
            _aggregator = tagAggregator;
            _tokenTypes = new IClassificationType[20];
            _tokenTypes[(int) TtlTokenType.DefStart] = typeService.GetClassificationType("StartTtl");
            _tokenTypes[(int) TtlTokenType.DefClose] = typeService.GetClassificationType("EndTtl");
            _tokenTypes[(int) TtlTokenType.DefStartName] = typeService.GetClassificationType("StartExtensionsBlock");
            _tokenTypes[(int) TtlTokenType.DefEndName] = typeService.GetClassificationType("EndExtensionsBlock");
            _tokenTypes[(int) TtlTokenType.Id] = typeService.GetClassificationType("Identifier");
            _tokenTypes[(int) TtlTokenType.Delim] = typeService.GetClassificationType("ExtensionDelimeter");
            _tokenTypes[(int) TtlTokenType.OutParamStart] = typeService.GetClassificationType("LParen");
            _tokenTypes[(int) TtlTokenType.OutParamEnd] = typeService.GetClassificationType("RParen");
            _tokenTypes[(int) TtlTokenType.Comment] = typeService.GetClassificationType("Comment");
            _tokenTypes[(int) TtlTokenType.SubStart] = typeService.GetClassificationType("StartSubextension");
            _tokenTypes[(int) TtlTokenType.SubClose] = typeService.GetClassificationType("EndSubextension");
            _tokenTypes[(int) TtlTokenType.ParseError] = typeService.GetClassificationType("ParseError");
            _tokenTypes[(int) TtlTokenType.CSharpStart] = typeService.GetClassificationType("CSharpStart");
            _tokenTypes[(int) TtlTokenType.CSharpToken] = typeService.GetClassificationType("CSharpStart");
            _tokenTypes[(int) TtlTokenType.DefOutputOnEnd] = typeService.GetClassificationType("DefaultOut");
            _tokenTypes[(int) TtlTokenType.DefType] = typeService.GetClassificationType("DefinitionType");
            _tokenTypes[(int) TtlTokenType.LineTerminate] = typeService.GetClassificationType("LineTermination");
            _tokenTypes[(int) TtlTokenType.Out] = typeService.GetClassificationType("OutStart");
            _tokenTypes[(int) TtlTokenType.MemberSelector] = typeService.GetClassificationType("MemberSelector");
            _tokenTypes[(int) TtlTokenType.RootReference] = typeService.GetClassificationType("RootReference");
        }

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return (_aggregator.GetTags(spans)
                .Select(tagSpan => new {tagSpan, tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot)})
                .Select(
                    t =>
                        new TagSpan<ClassificationTag>(t.tagSpans[0],
                            new ClassificationTag(_tokenTypes[(int) t.tagSpan.Tag.Type]))));
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
