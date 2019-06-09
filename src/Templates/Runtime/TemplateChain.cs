using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Runtime {
    internal class TemplateChain: IDataProcessor {

        private readonly List<TemplateItem> _itemsToExecute;

        public TemplateChain ()
        {
            _itemsToExecute = new List<TemplateItem>();
        }

        public ExType RenderType
        {
            get;
            private set;
        }

        public void Add (TemplateItem templateItem)
        {
            if (templateItem == null)
                throw new ArgumentNullException(nameof(templateItem));

            _itemsToExecute.Add(templateItem);
            RenderType = templateItem.ReturnType;
        }

        public int Count => _itemsToExecute.Count;

        public object ProcessData(in Scope scope)
        {
            object result = scope.ChainedData;
            foreach (var item in _itemsToExecute)
            {
                var chainedScope = scope.Chain(result);
                result = item.ProcessData(chainedScope);
            }
            return result;
        }

        public void RenderData(in Scope scope)
        {
            var result = scope.ChainedData;

            var lastItem = _itemsToExecute[_itemsToExecute.Count - 1];

            foreach (var item in _itemsToExecute)
            {
                var chainedScope = scope.Chain(result);

                if (ReferenceEquals(lastItem, item))
                {
                    item.RenderData(chainedScope);
                }
                else
                {
                    result = item.ProcessData(chainedScope);
                }
            }
        }

        public BlockPosition Position { get; set; }

        public ReadOnlyCollection<TemplateItem> ItemsToExecute => new ReadOnlyCollection<TemplateItem>(_itemsToExecute);

        #region Implementation of IDisposable

        public void Dispose ()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_itemsToExecute != null)
                {
                    foreach (TemplateItem templateItem in _itemsToExecute)
                    {
                        templateItem.Dispose();
                    }
                }
                GC.SuppressFinalize(this);
            }
        }

        #endregion
    }
}