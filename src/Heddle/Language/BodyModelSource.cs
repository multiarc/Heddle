namespace Heddle.Language
{
    /// <summary>Where a nested body's <c>ModelData</c> comes from.
    /// <para>The two roles left are the two the build can <b>read</b> rather than predict: the type an extension's
    /// own hook chose for the body, taken off a real engine compile, and the call's own data value, which a
    /// <c>[ChildTemplateHost]</c> extension's author declares by carrying that attribute. Everything else a body
    /// might be typed by is the hook's business at static-init, and the build emits it type-agnostically.</para>
    /// </summary>
    internal enum BodyModelSource
    {
        /// <summary>The host's own positional data value.</summary>
        Data,

        /// <summary>The body's model, named outright: the type the observed engine compile reports for the body's
        /// span.</summary>
        Observed
    }
}
