using System;
using FastStrings.Web;
using Templates.Core;
using Templates.Core.CompilerServices;

namespace Templates {
    public abstract class AbstractExtension: IExtension {
        protected TTLTemplate InnerTTLTemplate;
        private bool _directRender;

        private string _innerResult = string.Empty;

        #region IExtension Members

        public virtual object ProcessData (object value, object additionalValue)
        {
            return _directRender
                       ? HtmlEncode.Encode(ProcessDataInternal(value, additionalValue) as string)
                       : ProcessDataInternal(value, additionalValue);
        }

        public void ParseParameter (string parameter, Type dataType, Type additionalType, bool directRender)
        {
            _directRender = directRender;
        }

        public virtual Type InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (string.IsNullOrEmpty(parameter))
                InnerTTLTemplate = null;
            else {
                InnerTTLTemplate = new TTLTemplate(parameter, new CompileContext(context, additionalType));
                _innerResult = InnerTTLTemplate.Cached;
                if (!string.IsNullOrEmpty(_innerResult))
                    InnerTTLTemplate = null;
            }
            return typeof (string);
        }

        #endregion

        protected string GetInnerResult (object data)
        {
            if (InnerTTLTemplate != null)
                return InnerTTLTemplate.GenerateString(data);
            return _innerResult;
        }

        protected virtual object ProcessDataInternal (object value, object additionalValue)
        {
            return null;
        }

        #region Implementation of IDisposable

        public void Dispose ()
        {
            if (InnerTTLTemplate != null)
                InnerTTLTemplate.Dispose();
        }

        #endregion
    }
}