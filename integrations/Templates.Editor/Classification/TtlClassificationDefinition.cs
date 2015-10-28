using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Templates.Editor.Classification {
    internal static class TtlClassificationDefinition {
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("StartTtl")]
        internal static ClassificationTypeDefinition StartTtl = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("EndTtl")]
        internal static ClassificationTypeDefinition EndTtl = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("StartExtensionsBlock")]
        internal static ClassificationTypeDefinition StartExtensionsBlock = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("EndExtensionsBlock")]
        internal static ClassificationTypeDefinition EndExtensionsBlock = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("StartSubextension")]
        internal static ClassificationTypeDefinition StartSubextension = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("EndSubextension")]
        internal static ClassificationTypeDefinition EndSubextension = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("LParen")]
        internal static ClassificationTypeDefinition StartParameter = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("RParen")]
        internal static ClassificationTypeDefinition EndParameter = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Identifier")]
        internal static ClassificationTypeDefinition Identifier = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Comment")]
        internal static ClassificationTypeDefinition Comment = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("ParseError")]
        internal static ClassificationTypeDefinition ParseError = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("ExtensionDelimeter")]
        internal static ClassificationTypeDefinition ExtensionDelimeter = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("CSharpStart")]
        internal static ClassificationTypeDefinition CSharpStart = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("DefaultOut")]
        internal static ClassificationTypeDefinition DefaultOut = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("DefinitionType")]
        internal static ClassificationTypeDefinition DefinitionType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Text")]
        internal static ClassificationTypeDefinition Text = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("OutStart")]
        internal static ClassificationTypeDefinition OutStart = null;
    }
}
