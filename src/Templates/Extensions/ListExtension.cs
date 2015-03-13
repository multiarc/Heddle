using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Language;
using Templates.Runtime;
using Templates.Strings;

namespace Templates.Extensions {
    /// <summary>
    /// <para>List Template</para>
    /// <para>Optional parameter is sub-template (fully incluisive) wich represents one of element of the list.</para>
    /// <para>Data should be formatted as ordinary source template. Required <see cref="IEnumerable{T}"/> interface implemented for source data to be serialized.</para>
    /// <para>For Example:</para>
    /// <para>
    ///     <code>
    ///         <para>&lt;%&lt;list&gt;</para>
    ///             <para>CustomerList</para>
    ///             <para>[Birth Date: &lt;%&lt;date&gt;BirthDate[yyyy-MM-dd]%&gt; Name: &lt;%&lt;string&gt;Name%&gt;&lt;br /&gt;]</para>
    ///         <para>%&gt;</para>
    ///     </code>
    /// </para>
    /// <para>Will produce:</para>
    /// <para>Birth Date: 1970-02-22 Name: Alex</para>
    /// <para>Birth Date: 1976-04-15 Name: Anna</para>
    /// <para>...</para>
    /// </summary>
    [Name ("list")]
    [DataType (typeof (IEnumerable))]
    public class ListExtension: AbstractExtension {
        public override ExType InitStart(string parameterTemplate, ExType dataType, ExType chainedType, CompileContext context, ParseContext parseContext)        
        {
            if (dataType == null)
                throw new ArgumentNullException("dataType");
            if (dataType.IsDynamic) {
                return base.InitStart(parameterTemplate, ExType.Dynamic, chainedType, context, parseContext);
            }
            ExType underliyingType = (ExType)dataType.Type.GetGenericArguments().FirstOrDefault() ?? ExType.Dynamic;
            return base.InitStart(parameterTemplate, underliyingType, chainedType, context, parseContext);
        }

        public override object ProcessData(object value, object chainedResult)
        {
            if (value == null)
                return string.Empty;
            var builder = new ExStringBuilder();
            if (!(value is IEnumerable))
                return string.Empty;
            var enumerable = (IEnumerable<object>) value;
            foreach (object item in enumerable)
                builder.Append(GetInnerResult(item, null));
            return builder.ToString();
        }
    }
}