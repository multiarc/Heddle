using System;
using System.Net;
using Templates.Data;

namespace Templates.Core {
    public abstract class AbstractHtmlExtension: AbstractExtension {
        
        public sealed override object ProcessData (Scope scope)
        {
            var obj = ProcessDataInternal(scope);
            if (DirectRender && obj != null)
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

        protected abstract object ProcessDataInternal(Scope scope);
    }
}