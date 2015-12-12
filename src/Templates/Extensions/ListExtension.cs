using System;
using System.Collections;
using System.Collections.Generic;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
using Templates.Helpers;
using Templates.Strings;

namespace Templates.Extensions
{
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
    [ExtensionName("list")]
    [DataType(typeof (IEnumerable))]
    public class ListExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (dataType == null)
                throw new ArgumentNullException(nameof(dataType));
            if (dataType.IsDynamic)
            {
                return base.InitStart(initContext, dataType, chainedType, null);
            }
            ExType underliyingType = dataType.Type.TryGetElementType(typeof (IEnumerable<>)) ?? ExType.Dynamic;
            return base.InitStart(initContext, underliyingType, chainedType, parent);
        }

        public override object ProcessData(Scope scope)
        {
            if (scope.ModelData == null)
                return string.Empty;
            var builder = new ExStringBuilder();
            if (!(scope.ModelData is IEnumerable))
                return string.Empty;
            var enumerable = (IEnumerable) scope.ModelData;
            foreach (object item in enumerable)
                builder.Append(GetInnerResult(scope.Model(item)));
            return builder.ToString();
        }
    }
}