using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        [Name("StartParameter")]
        internal static ClassificationTypeDefinition StartParameter = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("EndParameter")]
        internal static ClassificationTypeDefinition EndParameter = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("ValidIdentifier")]
        internal static ClassificationTypeDefinition ValidIdentifier = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("Space")]
        internal static ClassificationTypeDefinition Space = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("ExtensionDelimeter")]
        internal static ClassificationTypeDefinition ExtensionDelimeter = null;
    }
}
