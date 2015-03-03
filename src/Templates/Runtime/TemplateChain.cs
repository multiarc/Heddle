using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Strings.Core;

namespace Templates.Runtime {
    internal class TemplateChain: IDataProcessor {

        private readonly List<TemplateItem> _itemsToExecute;

        public TemplateChain ()
        {
            _itemsToExecute = new List<TemplateItem>();
        }

        public Type RenderType
        {
            get;
            private set;
        }

        public void Add (TemplateItem templateItem)
        {
            if (templateItem == null)
                throw new ArgumentNullException("templateItem");

            _itemsToExecute.Add(templateItem);
            RenderType = templateItem.ReturnType;
        }

        public int Count => _itemsToExecute.Count;

        public object ProcessData (object data, object chainedResult)
        {
            return _itemsToExecute.Aggregate(chainedResult, (current, item) => item.ProcessData(data, current));
        }

        public BlockPosition Position { get; set; }

        public ReadOnlyCollection<TemplateItem> ItemsToExecute => _itemsToExecute.AsReadOnly();

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
                        templateItem.Extension?.Dispose();
                    }
                }
                GC.SuppressFinalize(this);
            }
        }

        #endregion
    }
}