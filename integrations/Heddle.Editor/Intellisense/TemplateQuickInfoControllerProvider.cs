////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Heddle.Editor.Intellisense
{
    #region IIntellisenseControllerProvider

    [Export(typeof(IIntellisenseControllerProvider))]
    [Name("Heddle QuickInfo Controller")]
    [ContentType("text")]
    internal sealed class TemplateQuickInfoControllerProvider : IIntellisenseControllerProvider
    {
        #region Asset Imports

        [Import]
        internal IQuickInfoBroker QuickInfoBroker { get; set; }

        #endregion

        #region IIntellisenseControllerFactory Members

        public IIntellisenseController TryCreateIntellisenseController(ITextView textView,
            IList<ITextBuffer> subjectBuffers)
        {
            return new TemplateQuickInfoController(textView, subjectBuffers, this);
        }

        #endregion

    }

    #endregion
}