using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Runtime {
    /// <summary>
    /// Recognized template in the source template
    /// </summary>
    public class DocumentElement {
        /// <summary>
        /// Template chain to execute
        /// </summary>
        public readonly TemplateChain TemplateBlock;

        /// <summary>
        /// Position in the source (start and end)
        /// </summary>
        public Position Position;

        public DocumentElement (PropertyGateDelegate data, PropertyGateDelegate additionalData)
        {
            TemplateBlock = new TemplateChain(data, additionalData);
        }
    }
}