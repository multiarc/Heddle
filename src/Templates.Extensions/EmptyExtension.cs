using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Empty Template</para>
    /// <para>Represents universal template for all undetermined types</para>
    /// </summary>
    [Name ("")]
    [DataType (typeof (object))]
    [DirectRender]
    public class EmptyExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            if (value == null)
                return GetInnerResult(additionalValue);
            return value.ToString();
        }
    }
}