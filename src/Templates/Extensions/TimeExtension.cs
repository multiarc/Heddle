using System;
using System.Globalization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    /// <summary>
    /// <para>Time Template</para>
    /// <para>Optional Parameter represents string to format</para>
    /// </summary>
    [ExtensionName("time")]
    [DataType(typeof(DateTime))]
    [EncodeOutput]
    public class TimeExtension : AbstractHtmlExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }


        protected override object ProcessDataInternal(ref Scope scope)
        {
            var parentScope = scope.Parent();
            var dateFormat = GetInnerResult(ref parentScope);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "t";

            if (scope.ModelData is DateTime date)
            {
                return date.ToString(dateFormat, CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        protected override void RenderDataInternal(ref Scope scope)
        {
            var parentScope = scope.Parent();
            var dateFormat = GetInnerResult(ref parentScope);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "t";

            if (scope.ModelData is DateTime date)
            {
                scope.Render(date.ToString(dateFormat, CultureInfo.InvariantCulture));
            }
        }
    }
}