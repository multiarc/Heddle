using System.Net;
using Templates.Data;

namespace Templates.Core
{
    public abstract class AbstractHtmlExtension : AbstractExtension
    {

        public sealed override object ProcessData(in Scope scope)
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

        public sealed override void RenderData(in Scope scope)
        {
            if (DirectRender)
            {
                var newScope = scope.RenderProxy(new HtmlEncodedRenderer(scope.Renderer));
                RenderDataInternal(newScope);
            }
            else
            {
                RenderDataInternal(scope);
            }
        }

        protected abstract object ProcessDataInternal(in Scope scope);

        protected abstract void RenderDataInternal(in Scope scope);
    }
}