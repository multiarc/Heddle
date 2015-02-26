using Templates.Attributes;

namespace Templates.Extensions {
    /// <summary>
    /// <para>Empty Template</para>
    /// <para>Represents universal template for all undetermined types</para>
    /// </summary>
    [Name ("")]
    [EncodeOutput]
    public class EmptyExtension: AbstractHtmlExtension {
        protected override object ProcessDataInternal (object value, object chainedResult)
        {
            if (SubTemplate != null)
            {
                return GetInnerResult(value, chainedResult);
            }
            if (value != null)
            {
                var s = value as string;
                if (s != null)
                    return s;
                return value.ToString();
            }
            return string.Empty;
        }
    }
}