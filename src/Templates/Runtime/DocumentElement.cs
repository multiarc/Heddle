using System;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Runtime
{
    /// <summary>
    /// Recognized template in the source template
    /// </summary>
    internal class DocumentElement : IDataProcessor
    {
        /// <summary>
        /// Template chain to execute
        /// </summary>
        private readonly TemplateChain _callChain;

        /// <summary>
        /// Position in the source (start and end)
        /// </summary>
        public BlockPosition Position { get; set; }

        public DocumentElement(BlockPosition position)
        {
            Position = position;
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

        public object ProcessData(in Scope scope)
        {
            return CallChain.ProcessData(scope);
        }

        public void RenderData(in Scope scope)
        {
            CallChain.RenderData(scope);
        }
    }
}