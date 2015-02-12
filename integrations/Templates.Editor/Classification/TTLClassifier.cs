using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor.Classification
{

    [Export(typeof(ITaggerProvider))]
    [ContentType("ttl")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class TtlClassifier : ITaggerProvider
    {
        [Export]
        [Name("ttl")]
        [BaseDefinition("htmlx")]
        internal static ContentTypeDefinition TtlContentTypeDefinition;

        [Export]
        [FileExtension(".thtml")]
        [ContentType("ttl")]
        internal static FileExtensionToContentTypeDefinition TtlFileExtensionToContentTypeDefinition;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry = null;

        [Import]
        internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {

            ITagAggregator<TokenTag> ookTagAggregator = aggregatorFactory.CreateTagAggregator<TokenTag>(buffer);

            return new TokenTagger(buffer, ookTagAggregator, ClassificationTypeRegistry) as ITagger<T>;
        }
    }
}
