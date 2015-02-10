using System;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Runtime {
    /// <summary>
    /// Recognized template in the source template
    /// </summary>
    public class DocumentElement : IDisposable {
        /// <summary>
        /// Template chain to execute
        /// </summary>
        public readonly TemplateChain TemplateBlock;

        /// <summary>
        /// Position in the source (start and end)
        /// </summary>
        public BlockPosition BlockPosition;

        public DocumentElement (PropertyGateDelegate data, PropertyGateDelegate additionalData)
        {
            TemplateBlock = new TemplateChain(data, additionalData);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                TemplateBlock.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}