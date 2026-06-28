using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Heddle.Language;

namespace Heddle.Editor.Classification {
    internal sealed class HeddleClassifier: ITagger<ClassificationTag> {
        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<HeddleTokenTag> _aggregator;
        private readonly IClassificationType[] _tokenTypes;

        public HeddleClassifier(ITextBuffer buffer, ITagAggregator<HeddleTokenTag> tagAggregator, IClassificationTypeRegistryService typeService)
        {
            _buffer = buffer;
            _aggregator = tagAggregator;
            _tokenTypes = new IClassificationType[20];
            _tokenTypes[(int) HeddleTokenType.DefStart] = typeService.GetClassificationType("StartHeddle");
            _tokenTypes[(int) HeddleTokenType.DefClose] = typeService.GetClassificationType("EndHeddle");
            _tokenTypes[(int) HeddleTokenType.DefStartName] = typeService.GetClassificationType("StartExtensionsBlock");
            _tokenTypes[(int) HeddleTokenType.DefEndName] = typeService.GetClassificationType("EndExtensionsBlock");
            _tokenTypes[(int) HeddleTokenType.Id] = typeService.GetClassificationType("Identifier");
            _tokenTypes[(int) HeddleTokenType.Delim] = typeService.GetClassificationType("ExtensionDelimeter");
            _tokenTypes[(int) HeddleTokenType.OutParamStart] = typeService.GetClassificationType("LParen");
            _tokenTypes[(int) HeddleTokenType.OutParamEnd] = typeService.GetClassificationType("RParen");
            _tokenTypes[(int) HeddleTokenType.Comment] = typeService.GetClassificationType("Comment");
            _tokenTypes[(int) HeddleTokenType.SubStart] = typeService.GetClassificationType("StartSubextension");
            _tokenTypes[(int) HeddleTokenType.SubClose] = typeService.GetClassificationType("EndSubextension");
            _tokenTypes[(int) HeddleTokenType.ParseError] = typeService.GetClassificationType("ParseError");
            _tokenTypes[(int) HeddleTokenType.CSharpStart] = typeService.GetClassificationType("CSharpStart");
            _tokenTypes[(int) HeddleTokenType.CSharpToken] = typeService.GetClassificationType("CSharpStart");
            _tokenTypes[(int) HeddleTokenType.DefOutputOnEnd] = typeService.GetClassificationType("DefaultOut");
            _tokenTypes[(int) HeddleTokenType.DefType] = typeService.GetClassificationType("DefinitionType");
            _tokenTypes[(int) HeddleTokenType.LineTerminate] = typeService.GetClassificationType("LineTermination");
            _tokenTypes[(int) HeddleTokenType.Out] = typeService.GetClassificationType("OutStart");
            _tokenTypes[(int) HeddleTokenType.MemberSelector] = typeService.GetClassificationType("MemberSelector");
            _tokenTypes[(int) HeddleTokenType.RootReference] = typeService.GetClassificationType("RootReference");
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
