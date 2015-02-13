using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor.Classification {

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "StartTtl")]
    [Name("StartTtl")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class StartTtl: ClassificationFormatDefinition {
        public StartTtl()
        {
            DisplayName = "TTL Start";
            ForegroundColor = Colors.CadetBlue;
            BackgroundColor = Colors.Honeydew;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "EndTtl")]
    [Name("EndTtl")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class EndTtl: ClassificationFormatDefinition {
        public EndTtl()
        {
            DisplayName = "TTL End";
            ForegroundColor = Colors.CadetBlue;
            BackgroundColor = Colors.Honeydew;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "StartExtensionsBlock")]
    [Name("StartExtensionsBlock")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class StartExtensionsBlock: ClassificationFormatDefinition {
        public StartExtensionsBlock()
        {
            DisplayName = "TTL Start Extension Block";
            ForegroundColor = Colors.DarkBlue;
            BackgroundColor = Colors.Honeydew;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "EndExtensionsBlock")]
    [Name("EndExtensionsBlock")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class EndExtensionsBlock: ClassificationFormatDefinition {
        public EndExtensionsBlock()
        {
            DisplayName = "TTL End Extension Block";
            ForegroundColor = Colors.DarkBlue;
            BackgroundColor = Colors.Honeydew;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "ValidIdentifier")]
    [Name("ValidIdentifier")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class ValidIdentifier: ClassificationFormatDefinition {
        public ValidIdentifier()
        {
            DisplayName = "TTL Valid Identifier";
            ForegroundColor = Colors.Black;
            BackgroundColor = Colors.Honeydew;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "ExtensionDelimeter")]
    [Name("ExtensionDelimeter")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class ExtensionDelimeter: ClassificationFormatDefinition {
        public ExtensionDelimeter()
        {
            DisplayName = "TTL Extension Delimeter";
            ForegroundColor = Colors.Brown;
            BackgroundColor = Colors.Honeydew;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "StartParameter")]
    [Name("StartParameter")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class StartParameter: ClassificationFormatDefinition {
        public StartParameter()
        {
            DisplayName = "TTL Start Parameters";
            ForegroundColor = Colors.DarkGreen;
            BackgroundColor = Colors.Honeydew;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "EndParameter")]
    [Name("EndParameter")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class EndParameter: ClassificationFormatDefinition {
        public EndParameter()
        {
            DisplayName = "TTL End Parameters";
            ForegroundColor = Colors.DarkGreen;
            BackgroundColor = Colors.Honeydew;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Space")]
    [Name("Space")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class Space: ClassificationFormatDefinition {
        public Space()
        {
            DisplayName = "TTL Space";
            BackgroundColor = Colors.Honeydew;
        }
    }
}
