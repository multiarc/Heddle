using System.Net;

namespace Templates.Core {
    public abstract class AbstractHtmlExtension: AbstractExtension {
        
        public sealed override object ProcessData (object data, object chained, object parent)
        {
            var obj = ProcessDataInternal(data, chained, parent);
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

        protected abstract object ProcessDataInternal(object value, object chainedResult, object parent);
    }
}