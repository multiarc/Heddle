using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Templates.Attributes;
using Templates.Runtime;
using Templates.Strings.Core;

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
    [Type (typeof (IEnumerable))]
    public class ListExtension: AbstractExtension {
        public override Type InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            if (dataType == null)
                throw new ArgumentNullException("dataType");

            Type underliyingType = dataType.GetGenericArguments().FirstOrDefault() ?? typeof (object);
            return base.InitializeInnerTemplate(parameter, null, underliyingType, context);
        }

        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            if (value == null)
                return string.Empty;
            var builder = new ExStringBuilder();
            if (!(value is IEnumerable))
                return string.Empty;
            var enumerable = (IEnumerable<object>) value;
            foreach (object item in enumerable)
                builder.Append(GetInnerResult(item));
            return builder.ToString();
        }
    }
}