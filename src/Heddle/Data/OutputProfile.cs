namespace Heddle.Data
{
    /// <summary>
    /// <para>Selects how the unnamed <c>@(...)</c> output emits values.</para>
    /// <para>The effective profile for a compile comes from <see cref="TemplateOptions.OutputProfile"/>
    /// (host default) and the <c>@profile()</c> directive (template override). File extensions carry no
    /// semantics.</para>
    /// </summary>
    public enum OutputProfile
    {
        /// <summary>Raw text output — today's behavior. Default in 1.x.</summary>
        Text = 0,

        /// <summary>The unnamed <c>@(...)</c> output HTML-encodes its value; <c>@raw(...)</c> opts out.
        /// Becomes the default in the 2.0 window.</summary>
        Html = 1
    }
}
