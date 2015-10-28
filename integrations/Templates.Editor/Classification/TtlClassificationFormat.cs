using System.ComponentModel.Composition;
using System.Windows;
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
            DisplayName = "TTL End Extension Block";
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
            DisplayName = "TTL Start Extension Block";
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
            DisplayName = "TTL End Extension Block";
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
            DisplayName = "TTL Valid Identifier";
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
            DisplayName = "TTL Extension Delimeter";
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
            DisplayName = "TTL Start Parameters";
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
            DisplayName = "TTL End Parameters";
            ForegroundColor = Colors.DarkGreen;
        }
    }

    //[Export(typeof(EditorFormatDefinition))]
    //[ClassificationType(ClassificationTypeNames = "Comment")]
    //[Name("Comment")]
    //[UserVisible(false)]
    //[Order(After = Priority.Default)]
    //internal sealed class Comment : ClassificationFormatDefinition
    //{
    //    public Comment()
    //    {
    //        DisplayName = "TTL Comment";
    //        BackgroundColor = Colors.Blue;
    //    }
    //}

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "ParseError")]
    [Name("ParseError")]
    [UserVisible(false)]
    [Order(After = Priority.High)]
    internal sealed class ParseError : ClassificationFormatDefinition
    {
        public ParseError()
        {
            DisplayName = "TTL Issue";
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
            DisplayName = "TTL C# Inline";
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
            DisplayName = "TTL Default Output on definition";
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
            DisplayName = "TTL definition template accept type";
            ForegroundColor = Colors.Brown;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Text")]
    [Name("Text")]
    [UserVisible(false)]
    [Order(After = Priority.Default)]
    internal sealed class Text : ClassificationFormatDefinition
    {
        public Text()
        {
            DisplayName = "TTL line termination symbol";
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
            DisplayName = "TTL output start";
            ForegroundColor = Colors.CadetBlue;
            IsBold = true;
        }
    }
}
