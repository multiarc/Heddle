using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor.Error {

    [Export(typeof(ITaggerProvider))]
    [ContentType("ttl")]
    [TagType(typeof(ErrorTag))]
    internal sealed class TtlErrorTaggerProvider : ITaggerProvider {
        [Import]
        internal IBufferTagAggregatorFactoryService AggregatorFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new TtlErrorTagger(buffer, AggregatorFactory) as ITagger<T>;
        }
    }
}
