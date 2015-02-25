using System;
using System.Collections.Generic;
using Templates.Data;

namespace Templates.Runtime {
    internal class TemplateChain: IDisposable {

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

        //public void PushFirst(TemplateItem templateItem)
        //{
        //    if (templateItem == null)
        //        throw new ArgumentNullException("templateItem");

        //    _itemsToExecute.Insert(0, templateItem);
        //    RenderType = templateItem.ReturnType;
        //}

        public object ProcessData (object data, object chainedResult)
        {
            foreach (TemplateItem item in _itemsToExecute)
            {
                chainedResult = item.Extension.ProcessData(item.Parameter.GetParameterResult(data, chainedResult), chainedResult);
#if DEBUG
                if (data != null && item.ReturnType != null && !item.ReturnType.IsType(data)) {
                    throw new TemplateProcessingException
                        (string.Format
                             (CultureInfo.InvariantCulture, "Returned data type not valid. Needed [{0}] Got [{1}]", item.ReturnType.FullName,
                              data.GetType().FullName));
                }
#endif
            }
            return chainedResult;
        }

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
                        if (templateItem.Extension != null)
                            templateItem.Extension.Dispose();
                    }
                }
            }
        }

        #endregion
    }
}