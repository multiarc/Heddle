using System;
using System.Globalization;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
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


        protected override object ProcessDataInternal(in Scope scope)
        {
            var parentScope = scope.Parent();
            var dateFormat = GetInnerResult(parentScope);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "t";

            if (scope.ModelData is DateTime date)
            {
                return date.ToString(dateFormat, CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        protected override void RenderDataInternal(in Scope scope)
        {
            var parentScope = scope.Parent();
            var dateFormat = GetInnerResult(parentScope);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "t";

            if (scope.ModelData is DateTime date)
            {
#if NET6_0_OR_GREATER
                scope.Renderer.Render(date, dateFormat, CultureInfo.InvariantCulture);   // phase 8 D10 (still DateTime, not TimeSpan)
#else
                scope.Renderer.Render(date.ToString(dateFormat, CultureInfo.InvariantCulture));
#endif
            }
        }
    }
}