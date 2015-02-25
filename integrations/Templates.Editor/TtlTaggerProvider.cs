using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor
{

    [Export(typeof(ITaggerProvider))]
    [ContentType("ttl")]
    [TagType(typeof(TtlTokenTag))]
    internal sealed class TtlTaggerProvider: ITaggerProvider {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T: ITag {
            return new TtlTokenTagger(buffer) as ITagger<T>;
        }
    }
}
