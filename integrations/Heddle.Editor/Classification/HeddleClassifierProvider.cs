using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Heddle.Editor.Classification {
    [Export(typeof(ITaggerProvider))]
    [ContentType("heddle")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class HeddleClassifierProvider : ITaggerProvider {
        [Export]
        [Name("heddle")]
        [BaseDefinition("code")]
        [BaseDefinition("projection")]
        internal static ContentTypeDefinition HeddleContentTypeDefinition = null;

        [Export]
        [FileExtension(".heddle")]
        [ContentType("heddle")]
        internal static FileExtensionToContentTypeDefinition HeddleFileExtensionToContentTypeDefinition = null;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry;

        [Import]
        internal IBufferTagAggregatorFactoryService AggregatorFactory;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            ITagAggregator<HeddleTokenTag> tagAggregator = AggregatorFactory.CreateTagAggregator<HeddleTokenTag>(buffer);
            return new HeddleClassifier(buffer, tagAggregator, ClassificationTypeRegistry) as ITagger<T>;
        }
    }
}