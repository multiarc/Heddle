using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Heddle.Editor
{

    [Export(typeof(ITaggerProvider))]
    [ContentType("heddle")]
    [TagType(typeof(HeddleTokenTag))]
    internal sealed class HeddleTaggerProvider: ITaggerProvider {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T: ITag {
            return new HeddleTokenTagger(buffer) as ITagger<T>;
        }
    }
}
