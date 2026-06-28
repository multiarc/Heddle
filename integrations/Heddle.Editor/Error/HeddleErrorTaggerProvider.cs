using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Heddle.Editor.Error {

    [Export(typeof(ITaggerProvider))]
    [TagType(typeof(IErrorTag))]
    [ContentType("heddle")]
    internal sealed class HeddleErrorTaggerProvider : ITaggerProvider {
        [Import]
        internal IBufferTagAggregatorFactoryService AggregatorFactory;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new HeddleErrorTagger(buffer, AggregatorFactory) as ITagger<T>;
        }
    }
}
