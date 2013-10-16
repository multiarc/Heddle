using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace TTL.Tagger {
    [Export (typeof (ITaggerProvider))]
    [ContentType (Configuration.ContentType)]
    [TagType (typeof (ClassificationTag))]
    internal sealed class TTLTaggerProvider: ITaggerProvider {
        [Export]
        [Name (Configuration.ContentType)]
        [BaseDefinition ("code")]
        internal static ContentTypeDefinition ContentType;

        [Export]
        [FileExtension (Configuration.FileExtension)]
        [ContentType (Configuration.ContentType)]
        internal static FileExtensionToContentTypeDefinition FileType;

        [Import]
        internal IBufferTagAggregatorFactoryService AggregatorFactory;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry;

        #region ITaggerProvider Members

        public ITagger<T> CreateTagger<T> (ITextBuffer buffer) where T : ITag
        {
            ITagAggregator<TTLTokenTag> ttlTagAggregator = AggregatorFactory.CreateTagAggregator<TTLTokenTag>(buffer);

            return new TTLTagger(ttlTagAggregator, ClassificationTypeRegistry) as ITagger<T>;
        }

        #endregion
    }
}