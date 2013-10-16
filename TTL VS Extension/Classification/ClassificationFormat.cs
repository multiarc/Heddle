using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace TTL.Classification {

    #region Format definition

    /// <summary>
    /// Defines an editor format for the OrdinaryClassification type that has a purple background
    /// and is underlined.
    /// </summary>
    [Export (typeof (EditorFormatDefinition))]
    [ClassificationType (ClassificationTypeNames = "Block")]
    [Name ("Block")]
    //this should be visible to the end user
    [UserVisible (false)]
    //set the priority to be after the default classifiers
    [Order (Before = Priority.Default)]
    internal sealed class TTL: ClassificationFormatDefinition {
        /// <summary>
        /// Defines the visual format for the "ordinary" classification type
        /// </summary>
        public TTL ()
        {
            DisplayName = "TTL"; //human readable version of the name
            BackgroundColor = Colors.Tan;
        }
    }

    #endregion //Format definition
}