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
    [DataType(typeof (DateTime))]
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
            string dateFormat = GetInnerResult(ref parentScope);
            if (string.IsNullOrEmpty(dateFormat))
                dateFormat = "t";
            if (!(scope.ModelData is DateTime))
                return string.Empty;
            return ((DateTime) scope.ModelData).ToString(dateFormat, CultureInfo.InvariantCulture);
        }
    }
}