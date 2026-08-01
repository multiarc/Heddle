using System;
using System.Globalization;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// Formats a DateTime value. Optional parameter is the date format string; defaults to "d".
    /// Example: <code>&lt;%&lt;date&gt;BirthDate[yyyy-MM-dd]%&gt;</code>
    /// </summary>
    [ExtensionName("date")]
    [DataType(typeof(DateTime))]
    [EncodeOutput]
    public class DateExtension : AbstractHtmlExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        protected override object ProcessDataInternal(in Scope scope)
        {
            var parentData = scope.Parent();
            var dateFormat = GetInnerResult(parentData);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "d";
            if (!(scope.ModelData is DateTime))
                return string.Empty;
            return ((DateTime) scope.ModelData).ToString(dateFormat, CultureInfo.InvariantCulture);
        }

        protected override void RenderDataInternal(in Scope scope)
        {
            var parentData = scope.Parent();
            var dateFormat = GetInnerResult(parentData);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "d";
            if (scope.ModelData is DateTime date)
            {
#if NET8_0_OR_GREATER
                scope.Renderer.Render(date, dateFormat, CultureInfo.InvariantCulture);
#else
                scope.Renderer.Render(date.ToString(dateFormat, CultureInfo.InvariantCulture));
#endif
            }
        }
    }
}