namespace Heddle.Precompiled
{
    /// <summary>A named registry extension bound at generation time (phase 7 D9): the call name and the extension's
    /// assembly-qualified type name <b>without version</b>. Extension logic is never inlined, so the manifest records
    /// only <i>what</i> to bind; the per-request gauntlet compares this against the live <c>TemplateFactory</c>
    /// registry so an <c>[ExtensionReplace]</c> override is a detected divergence rather than a silent one.</summary>
    public readonly struct PrecompiledExtensionBinding
    {
        public PrecompiledExtensionBinding(string name, string extensionTypeName,
            string propLayoutFingerprint = null)
        {
            Name = name;
            ExtensionTypeName = extensionTypeName;
            PropLayoutFingerprint = propLayoutFingerprint;
        }

        public string Name { get; }

        public string ExtensionTypeName { get; }

        /// <summary>
        /// Phase 3 (OQ4): a fingerprint of the extension's resolved <c>[Prop]</c> slot layout — the ordered
        /// <c>name:&lt;slot type AQN&gt;</c> pairs, joined with <c>|</c> — or <c>null</c> for an extension that
        /// declares no parameters (and for every manifest emitted before schema 4).
        /// <para>Slot <b>indices</b> are the wire format between the generator's frozen <c>object[]</c> prototype
        /// and the runtime's <c>ExtensionParameterCarrier</c>, and until this row existed they were the one
        /// wire-format contract with <b>no</b> gauntlet coverage: the gauntlet checked options, extension identity,
        /// functions and staleness, so a layout disagreement was silent wrong rendered output rather than a
        /// fallback. Sharing <c>PropLayoutCore</c> makes a disagreement much harder; this row makes it
        /// <i>visible</i> when one happens anyway (a referenced extension package changing its <c>[Prop]</c> set
        /// without the templates being regenerated is the live case).</para>
        /// <para>The check is vacuous when the value is <c>null</c>, so older manifests keep passing — the schema
        /// evolution stays inside phase 5's <c>Min</c>/<c>Max</c> compatibility predicate and no
        /// re-precompilation is forced.</para>
        /// </summary>
        public string PropLayoutFingerprint { get; }
    }
}
