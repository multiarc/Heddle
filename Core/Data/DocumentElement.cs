using FastStrings.Core;
using Templates.Core.CompilerServices;

namespace Templates.Core.Data {
    /// <summary>
    /// Recognized template in the source template
    /// </summary>
    internal class DocumentElement {
        /// <summary>
        /// Template chain to execute
        /// </summary>
        internal readonly TemplateChain TemplateBlock;

        /// <summary>
        /// Position in the source (start and end)
        /// </summary>
        internal Position Position;

        public DocumentElement (PropertyGateDelegate data, PropertyGateDelegate additionalData)
        {
            TemplateBlock = new TemplateChain(data, additionalData);
        }
    }
}