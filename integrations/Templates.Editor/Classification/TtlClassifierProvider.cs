using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor.Classification {
    [Export(typeof(ITaggerProvider))]
    [ContentType("ttl")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class TtlClassifierProvider : ITaggerProvider {
        [Export]
        [Name("ttl")]
        [BaseDefinition("htmlx")]
        [BaseDefinition("code")]
        [BaseDefinition("projection")]
        internal static ContentTypeDefinition TtlContentTypeDefinition = null;

        [Export]
        [FileExtension(".thtml")]
        [ContentType("ttl")]
        internal static FileExtensionToContentTypeDefinition TtlFileExtensionToContentTypeDefinition = null;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry;

        [Import]
        internal IBufferTagAggregatorFactoryService AggregatorFactory;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            ITagAggregator<TtlTokenTag> tagAggregator = AggregatorFactory.CreateTagAggregator<TtlTokenTag>(buffer);
            return new TtlClassifier(buffer, tagAggregator, ClassificationTypeRegistry) as ITagger<T>;
        }
    }
}
