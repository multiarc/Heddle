using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    /// <summary>
    /// <para>String Template</para>
    /// <para>Optional parameter represents default value if data name is not set in template or the object value is null</para>
    /// </summary>
    [ExtensionName("string")]
    [DataType(typeof (string))]
    [EncodeOutput]
    public class StringExtension : AbstractHtmlExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal(ref Scope scope)
        {
            var model = scope.ModelData;
            if (model == null)
            {
                var parentScope = scope.Parent();
                return GetInnerResult(ref parentScope);
            }
            if (!(model is string))
            {
                try
                {
                    model = Convert.ChangeType(model, typeof (string), CultureInfo.InvariantCulture);
                }
                catch (InvalidCastException)
                {
                    return model.ToString();
                }
            }
            return model;
        }
    }
}