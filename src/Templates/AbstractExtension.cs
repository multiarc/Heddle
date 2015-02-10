using System;
using System.Net;
using Templates.Helpers;
using Templates.Runtime;
using Templates.Strings.Web;


namespace Templates {
    public abstract class AbstractExtension: IExtension {
        protected TtlTemplate InnerTtlTemplate;
        private bool _directRender;

        private string _innerResult = string.Empty;

        #region IExtension Members

        public virtual object ProcessData (object value, object additionalValue)
        {
            var obj = ProcessDataInternal(value, additionalValue);
            if (_directRender)
            {
                var dataToEncode = obj as string;
                if (!string.IsNullOrEmpty(dataToEncode))
                {
                    return WebUtility.HtmlEncode(dataToEncode);
                }
                return obj;
            }
            return obj;
        }

        public void ParseParameter (string parameter, Type dataType, Type additionalType, bool directRender)
        {
            _directRender = directRender;
        }

        public virtual Type InitializeInnerTemplate(string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (string.IsNullOrEmpty(parameter))
                InnerTtlTemplate = null;
            else {
                InnerTtlTemplate = new TtlTemplate(parameter, new CompileContext(context, additionalType));
                _innerResult = InnerTtlTemplate.Cached;
                if (!string.IsNullOrEmpty(_innerResult))
                    InnerTtlTemplate = null;
            }
            return typeof (string);
        }

        public void ParseInnerTemplate(CompileContext newContext)
        {
            InnerTtlTemplate = new TtlTemplate(newContext);
        }

        #endregion

        protected string GetInnerResult (object data)
        {
            if (InnerTtlTemplate != null)
                return InnerTtlTemplate.GenerateString(data);
            return _innerResult;
        }

        protected virtual object ProcessDataInternal (object value, object additionalValue)
        {
            return null;
        }

        private bool _disposing;

        #region Implementation of IDisposable

        public void Dispose ()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposing && !disposing) {
                _disposing = true;
                if (InnerTtlTemplate != null)
                    InnerTtlTemplate.Dispose();
            }
        }

        #endregion
    }
}