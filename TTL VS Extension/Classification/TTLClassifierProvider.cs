using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace TTL.Classification
{
    [Export(typeof(IClassifierProvider))]
    [ContentType(Configuration.ContentType)]
    internal sealed class TTLClassifierProvider : IClassifierProvider 
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry;

        #region Implementation of IClassifierProvider

        public IClassifier GetClassifier (ITextBuffer textBuffer)
        {
            return new TTLClassifier(textBuffer, ClassificationTypeRegistry);
        }

        #endregion
    }
}
