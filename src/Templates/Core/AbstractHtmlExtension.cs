using System.Net;
using Templates.Data;

namespace Templates.Core
{
    public abstract class AbstractHtmlExtension : AbstractExtension
    {

        public sealed override object ProcessData(ref Scope scope)
        {
            var obj = ProcessDataInternal(ref scope);
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

        public sealed override void RenderData(ref Scope scope)
        {
            if (DirectRender)
            {
                scope = scope.RenderProxy(new HtmlEncodedRenderer(scope.Renderer));
            }

            RenderDataInternal(ref scope);
        }

        protected abstract object ProcessDataInternal(ref Scope scope);

        protected abstract void RenderDataInternal(ref Scope scope);
    }
}