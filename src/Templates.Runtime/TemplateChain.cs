using System;
using System.Collections.Generic;
using System.Globalization;
using Templates.Core.Data;
using Templates.Exceptions;
using Templates.Helpers;

namespace Templates.Runtime {
    public class TemplateChain: IDisposable {
        /// <summary>
        /// Property link in destination type to additional data
        /// </summary>
        private readonly PropertyGateDelegate _additionalData;

        /// <summary>
        /// Property link in destination type to main data
        /// </summary>
        private readonly PropertyGateDelegate _data;

        private readonly List<TemplateItem> _itemsToExecute;

        public TemplateChain (PropertyGateDelegate data, PropertyGateDelegate additionalData)
        {
            _itemsToExecute = new List<TemplateItem>();
            _data = data;
            _additionalData = additionalData;
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

        public string ProcessData (object source)
        {
            object data = _data.GetValueOrDefault(source);
            object additionalData = _additionalData.GetValueOrDefault(source);
            foreach (TemplateItem item in _itemsToExecute) {
                data = item.Extension.ProcessData(data, additionalData);
                if (data != null && item.ReturnType != null && !item.ReturnType.IsType(data)) {
                    throw new TemplateProcessingException
                        (string.Format
                             (CultureInfo.InvariantCulture, "Returned data type not valid. Needed [{0}] Got [{1}]", item.ReturnType.FullName,
                              data.GetType().FullName));
                }
            }
            return data as string;
        }

        #region Implementation of IDisposable

        public void Dispose ()
        {
            if (_itemsToExecute != null) {
                foreach (TemplateItem templateItem in _itemsToExecute) {
                    if (templateItem.Extension != null)
                        templateItem.Extension.Dispose();
                }
            }
        }

        #endregion
    }
}