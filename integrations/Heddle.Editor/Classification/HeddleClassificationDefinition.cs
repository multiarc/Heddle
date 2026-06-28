using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Heddle.Editor.Classification {
    internal static class HeddleClassificationDefinition {
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("StartHeddle")]
        internal static ClassificationTypeDefinition StartHeddle = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("EndHeddle")]
        internal static ClassificationTypeDefinition EndHeddle = null;

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
        [Name("OutStart")]
        internal static ClassificationTypeDefinition OutStart = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("LineTermination")]
        internal static ClassificationTypeDefinition LineTermination = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("MemberSelector")]
        internal static ClassificationTypeDefinition MemberSelector = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("RootReference")]
        internal static ClassificationTypeDefinition RootReference = null;
    }
}
