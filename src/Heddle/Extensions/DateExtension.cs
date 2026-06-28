using System;
using System.Globalization;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Extensions
{
    /// <summary>
    /// <para>Date template</para>
    /// <para>Optional parameter represents date formatting</para>
    /// <para>For Example:</para>
    /// <para>
    ///     <code>
    ///         &lt;%&lt;date&gt;BirthDate[yyyy-MM-dd]%&gt;
    ///     </code>
    /// </para>
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
                scope.Renderer.Render(date.ToString(dateFormat, CultureInfo.InvariantCulture));
            }
        }
    }
}