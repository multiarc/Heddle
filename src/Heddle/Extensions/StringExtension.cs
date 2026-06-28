using System;
using System.Globalization;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>String Template</para>
    /// <para>Optional parameter represents default value if data name is not set in template or the object value is null</para>
    /// </summary>
    [ExtensionName("string")]
    [DataType(typeof(string))]
    [EncodeOutput]
    public class StringExtension : AbstractHtmlExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
            {
                var parentScope = scope.Parent();
                return GetInnerResult(parentScope);
            }

            if (model is string)
                return model;

            try
            {
                model = Convert.ChangeType(model, typeof(string), CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException)
            {
                return model.ToString();
            }

            return model;
        }

        protected override void RenderDataInternal(in Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
            {
                var parentScope = scope.Parent();
                RenderInnerResult(parentScope);
                return;
            }

            if (model is string data)
            {
                scope.Renderer.Render(data);
                return;
            }

            try
            {
                var str = (string) Convert.ChangeType(model, typeof(string), CultureInfo.InvariantCulture);
                scope.Renderer.Render(str);
            }
            catch (InvalidCastException)
            {
                scope.Renderer.Render(model.ToString());
            }
        }
    }
}