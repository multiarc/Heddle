using System;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Runtime {
    /// <summary>
    /// Recognized template in the source template
    /// </summary>
    internal class DocumentElement : IDisposable {
        /// <summary>
        /// Template chain to execute
        /// </summary>
        private readonly TemplateChain _callChain;

        /// <summary>
        /// Position in the source (start and end)
        /// </summary>
        public BlockPosition BlockPosition;

        public DocumentElement (BlockPosition position)
        {
            BlockPosition = position;
            _callChain = new TemplateChain();
        }

        /// <summary>
        /// Template chain to execute
        /// </summary>
        public TemplateChain CallChain => _callChain;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _callChain.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}