using System;
using System.Net;
using Templates.Data;
using Templates.Language;
using Templates.Runtime;

namespace Templates {
    public abstract class AbstractHtmlExtension: IExtension {
        private bool _directRender;

        private string _innerResult = string.Empty;

        #region IExtension Members

        public object ProcessData (object value, object chainedResult)
        {
            var obj = ProcessDataInternal(value, chainedResult);
            if (_directRender)
            {
                var dataToEncode = obj as string;
                if (!string.IsNullOrEmpty(dataToEncode))
                {
                    return WebUtility.HtmlEncode(dataToEncode);
                }
                return WebUtility.HtmlEncode(obj.ToString());
            }
            return obj;
        }

        public void SetUpRenderType (RenderType renderType)
        {
            _directRender = renderType == RenderType.Encode;
        }

        public virtual Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            SubTemplate = string.IsNullOrEmpty(parameterTemplate)
                ? null
                : TtlCompiler.Compile(parameterTemplate, new CompileContext(context, dataType), parseContext);
            if (SubTemplate?.Empty ?? true)
            {
                _innerResult = parameterTemplate;
                SubTemplate?.Dispose();
                SubTemplate = null;
            }
            return typeof (string);
        }

        public virtual void CompleteInit(CompileContext newContext, ParseContext parseContext)
        {
            
        }

        public RuntimeDocument SubTemplate { get; protected set; }

        #endregion

        protected string GetInnerResult (object data, object chainedResult)
        {
            return SubTemplate?.ProcessData(data, chainedResult) ?? _innerResult;
        }

        protected abstract object ProcessDataInternal(object value, object chainedResult);

        #region Implementation of IDisposable

        public void Dispose ()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                SubTemplate?.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        #endregion
    }
}