using Templates.Attributes;
using Templates.Data;

namespace Templates.Extensions {
    /// <summary>
    /// <para>System Template</para>
    /// <para>It cannot get any data.</para>
    /// <para>Optional parameter represents string wich contains just strings to replace with.</para>
    /// <para>For example:</para>
    /// <para>
    ///     <code>
    ///         &lt;%&lt;system&gt;[bps bpe bs be sbs sbe]%&gt;
    ///     </code>
    /// </para>
    /// <para>Will produce: &lt;% %&gt; &lt; &gt; [ ]</para>
    /// </summary>
    [Name ("system")]
    [Type (typeof (object))]
    [DirectRender]
    public class SystemExtension: AbstractExtension {
        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            return SystemPatternStrings.ReplaceAll(GetInnerResult(value));
        }
    }
}