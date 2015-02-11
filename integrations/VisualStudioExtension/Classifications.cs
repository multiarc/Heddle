using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Templater.VisualStudioExtension {

   public static class FlowControlClassificationDefinition {
      [Export(typeof(ClassificationTypeDefinition))]
      [Name(Constants.ClassifName)]
      internal static ClassificationTypeDefinition FlowControlClassificationType = null;
   }
   public static class LinqKeywordClassificationDefinition {
      [Export(typeof(ClassificationTypeDefinition))]
      [Name(Constants.LinqClassifName)]
      internal static ClassificationTypeDefinition LinqKeywordClassificationType = null;
   }
   public static class VisibilityKeywordClassificationDefinition {
      [Export(typeof(ClassificationTypeDefinition))]
      [Name(Constants.VisibilityClassifName)]
      internal static ClassificationTypeDefinition VisibilityKeywordClassificationType = null;
   }
   public static class StringEscapeSequenceClassificationDefinition {
      [Export(typeof(ClassificationTypeDefinition))]
      [Name(Constants.StringEscapeClassifName)]
      internal static ClassificationTypeDefinition StringEscapeSequenceClassificationType = null;
   }

   [Export(typeof(EditorFormatDefinition))]
   [ClassificationType(ClassificationTypeNames = Constants.ClassifName)]
   [Name(Constants.ClassifName)]
   [UserVisible(true)]
   [Order(After = Priority.High)]
   public sealed class FlowControlFormat : ClassificationFormatDefinition {
      public FlowControlFormat() {
         this.DisplayName = Constants.ClassifName;
         this.ForegroundColor = Colors.MediumTurquoise;
         this.IsItalic = true;
      }
   }

   [Export(typeof(EditorFormatDefinition))]
   [ClassificationType(ClassificationTypeNames = Constants.LinqClassifName)]
   [Name(Constants.LinqClassifName)]
   [UserVisible(true)]
   [Order(After = Priority.High)]
   public sealed class LinqKeywordFormat : ClassificationFormatDefinition {
      public LinqKeywordFormat() {
         this.DisplayName = Constants.LinqClassifName;
         this.ForegroundColor = Colors.MediumSeaGreen;
      }
   }
   [Export(typeof(EditorFormatDefinition))]
   [ClassificationType(ClassificationTypeNames = Constants.VisibilityClassifName)]
   [Name(Constants.VisibilityClassifName)]
   [UserVisible(true)]
   [Order(After = Priority.High)]
   public sealed class VisibilityKeywordFormat : ClassificationFormatDefinition {
      public VisibilityKeywordFormat() {
         this.DisplayName = Constants.VisibilityClassifName;
         this.ForegroundColor = Colors.DimGray;
         this.IsBold = true;
      }
   }

   [Export(typeof(EditorFormatDefinition))]
   [ClassificationType(ClassificationTypeNames = Constants.StringEscapeClassifName)]
   [Name(Constants.StringEscapeClassifName)]
   [UserVisible(true)]
   [Order(After = Priority.High)]
   public sealed class StringEscapeSequenceFormat : ClassificationFormatDefinition {
      public StringEscapeSequenceFormat() {
         this.DisplayName = Constants.StringEscapeClassifName;
         this.ForegroundColor = Colors.DimGray;
      }
   }
}
