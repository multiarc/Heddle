using System;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Runtime
{
    /// <summary>
    /// One template block recognized in the source, owning the chain of extensions it expands to.
    /// </summary>
    internal class DocumentElement : IDataProcessor
    {
        private readonly TemplateChain _callChain;

        public BlockPosition Position { get; set; }

        public DocumentElement(BlockPosition position)
        {
            Position = position;
            _callChain = new TemplateChain();
        }

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