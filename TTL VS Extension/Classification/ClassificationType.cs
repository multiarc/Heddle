using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Templates.Core.CompilerServices;

namespace TTL.Classification {
    internal static class OrdinaryClassificationDefinition {
        #region Type definition

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name (ParserConfiguration.StartTTL)]
        internal static ClassificationTypeDefinition TTLStart;

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name (ParserConfiguration.EndTTL)]
        internal static ClassificationTypeDefinition TTLEnd;

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name (ParserConfiguration.StartExtensionsBlock)]
        internal static ClassificationTypeDefinition TTLStartExtension;

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name (ParserConfiguration.EndExtensionsBlock)]
        internal static ClassificationTypeDefinition TTLEndExtension;

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name (ParserConfiguration.ExtensionDelimeter)]
        internal static ClassificationTypeDefinition TTLExtensionDelimeter;

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name (ParserConfiguration.StartParameter)]
        internal static ClassificationTypeDefinition TTLStartParameter;

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name (ParserConfiguration.EndParameter)]
        internal static ClassificationTypeDefinition TTLEndParameter;

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name ("ID")]
        internal static ClassificationTypeDefinition TTLIdentifier;

        /// <summary>
        /// Defines the "ordinary" classification type.
        /// </summary>
        [Export (typeof (ClassificationTypeDefinition))]
        [Name ("Block")]
        internal static ClassificationTypeDefinition TTL;

        #endregion
    }
}