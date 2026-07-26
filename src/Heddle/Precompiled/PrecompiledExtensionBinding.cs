namespace Heddle.Precompiled
{
    /// <summary>A named registry extension bound at generation time: the call name and the extension's
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
        /// A fingerprint of the extension's resolved <c>[Prop]</c> slot layout — the ordered
        /// <c>name:&lt;slot type AQN&gt;</c> pairs, joined with <c>|</c> — or <c>null</c> for extensions with no parameters.
        /// Enables detection of layout disagreements (e.g., a referenced extension package changing its <c>[Prop]</c> set
        /// without regeneration). The check is vacuous when <c>null</c>, so schema evolution stays backward compatible.
        /// </summary>
        public string PropLayoutFingerprint { get; }
    }
}
