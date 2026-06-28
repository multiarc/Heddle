using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Heddle.Editor.Classification {

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "StartHeddle")]
    [Name("StartHeddle")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class StartHeddle: ClassificationFormatDefinition {
        public StartHeddle()
        {
            DisplayName = "Heddle Start";
            ForegroundColor = Colors.CadetBlue;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "EndHeddle")]
    [Name("EndHeddle")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class EndHeddle: ClassificationFormatDefinition {
        public EndHeddle()
        {
            DisplayName = "Heddle End";
            ForegroundColor = Colors.CadetBlue;
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
            DisplayName = "Heddle Start Extension Block";
            ForegroundColor = Colors.DarkCyan;
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
            DisplayName = "Heddle End Extension Block";
            ForegroundColor = Colors.DarkCyan;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "StartSubextension")]
    [Name("StartSubextension")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class StartSubextension : ClassificationFormatDefinition
    {
        public StartSubextension()
        {
            DisplayName = "Heddle Start Extension Block";
            ForegroundColor = Colors.DarkCyan;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "EndSubextension")]
    [Name("EndSubextension")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class EndSubextension : ClassificationFormatDefinition
    {
        public EndSubextension()
        {
            DisplayName = "Heddle End Extension Block";
            ForegroundColor = Colors.DarkCyan;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Identifier")]
    [Name("Identifier")]
    [UserVisible(false)]
    [Order(After = Priority.High)]
    internal sealed class Identifier : ClassificationFormatDefinition {
        public Identifier()
        {
            DisplayName = "Heddle Valid Identifier";
            ForegroundColor = Colors.Black;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "ExtensionDelimeter")]
    [Name("ExtensionDelimeter")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class ExtensionDelimeter: ClassificationFormatDefinition {
        public ExtensionDelimeter()
        {
            DisplayName = "Heddle Extension Delimeter";
            ForegroundColor = Colors.Brown;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "LParen")]
    [Name("LParen")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class LParen : ClassificationFormatDefinition {
        public LParen()
        {
            DisplayName = "Heddle Start Parameters";
            ForegroundColor = Colors.DarkGreen;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "RParen")]
    [Name("RParen")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class RParen : ClassificationFormatDefinition {
        public RParen()
        {
            DisplayName = "Heddle End Parameters";
            ForegroundColor = Colors.DarkGreen;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "ParseError")]
    [Name("ParseError")]
    [UserVisible(false)]
    [Order(After = Priority.High)]
    internal sealed class ParseError : ClassificationFormatDefinition
    {
        public ParseError()
        {
            DisplayName = "Heddle Issue";
            BackgroundColor = Colors.OrangeRed;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "CSharpStart")]
    [Name("CSharpStart")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class CSharpStart : ClassificationFormatDefinition
    {
        public CSharpStart()
        {
            DisplayName = "Heddle C# Inline";
            ForegroundColor = Colors.CornflowerBlue;
            IsItalic = true;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "DefaultOut")]
    [Name("DefaultOut")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class DefaultOut : ClassificationFormatDefinition
    {
        public DefaultOut()
        {
            DisplayName = "Heddle Default Output on definition";
            ForegroundColor = Colors.Brown;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "DefinitionType")]
    [Name("DefinitionType")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class DefinitionType : ClassificationFormatDefinition
    {
        public DefinitionType()
        {
            DisplayName = "Heddle definition template accept type";
            ForegroundColor = Colors.Brown;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "OutStart")]
    [Name("OutStart")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class OutStart : ClassificationFormatDefinition
    {
        public OutStart()
        {
            DisplayName = "Heddle output start";
            ForegroundColor = Colors.CadetBlue;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "LineTermination")]
    [Name("LineTermination")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class LineTermination : ClassificationFormatDefinition
    {
        public LineTermination()
        {
            DisplayName = "Heddle line termination symbol";
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "MemberSelector")]
    [Name("MemberSelector")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class MemberSelector : ClassificationFormatDefinition
    {
        public MemberSelector()
        {
            DisplayName = "Heddle type member";
            ForegroundColor = Colors.DarkBlue;
            IsBold = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "RootReference")]
    [Name("RootReference")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class RootReference : ClassificationFormatDefinition
    {
        public RootReference()
        {
            DisplayName = "Heddle output start";
            ForegroundColor = Colors.CadetBlue;
            IsBold = true;
        }
    }
}