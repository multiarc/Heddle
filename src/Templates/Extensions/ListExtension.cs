using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Helpers;
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
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType)        
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));
            if (dataType.IsDynamic) {
                return base.InitStart(initContext, dataType, chainedType);
            }
            ExType underliyingType = dataType.Type.TryGetElementType(typeof(IEnumerable<>)) ?? ExType.Dynamic;
            return base.InitStart(initContext, underliyingType, chainedType);
        }

        public override object ProcessData(object data, object chained)
        {
            if (data == null)
                return string.Empty;
            var builder = new ExStringBuilder();
            if (!(data is IEnumerable))
                return string.Empty;
            var enumerable = (IEnumerable<object>) data;
            foreach (object item in enumerable)
                builder.Append(GetInnerResult(item, chained));
            return builder.ToString();
        }
    }
}