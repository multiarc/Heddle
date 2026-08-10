using System.Net;
using System.Text.Encodings.Web;
using Heddle.Data;

namespace Heddle.Core
{
    public abstract class AbstractHtmlExtension : AbstractExtension
    {

        public sealed override object ProcessData(in Scope scope)
        {
            var obj = ProcessDataInternal(scope);
            if (DirectRender && obj != null)
            {
                // Use configured encoder or fall back to WebUtility for legacy compatibility.
                var encoder = (scope.Renderer as IEncoderCarrier)?.Encoder;
                var dataToEncode = obj as string;
                if (!string.IsNullOrEmpty(dataToEncode))
                {
                    return encoder == null ? WebUtility.HtmlEncode(dataToEncode) : encoder.Encode(dataToEncode);
                }

                var text = obj.ToString();
                return encoder == null ? WebUtility.HtmlEncode(text) : encoder.Encode(text);
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