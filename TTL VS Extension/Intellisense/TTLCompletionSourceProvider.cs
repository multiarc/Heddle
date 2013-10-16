using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace TTL.Intellisense {
    [Export (typeof (ICompletionSourceProvider))]
    [ContentType (Configuration.ContentType)]
    [Name ("TTLCompletion")]
    internal class TTLCompletionSourceProvider: ICompletionSourceProvider {
        #region ICompletionSourceProvider Members

        public ICompletionSource TryCreateCompletionSource (ITextBuffer textBuffer)
        {
            return new TTLCompletionSource(textBuffer);
        }

        #endregion
    }
}