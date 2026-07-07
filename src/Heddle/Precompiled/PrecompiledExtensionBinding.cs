namespace Heddle.Precompiled
{
    /// <summary>A named registry extension bound at generation time (phase 7 D9): the call name and the extension's
    /// assembly-qualified type name <b>without version</b>. Extension logic is never inlined, so the manifest records
    /// only <i>what</i> to bind; the per-request gauntlet compares this against the live <c>TemplateFactory</c>
    /// registry so an <c>[ExtensionReplace]</c> override is a detected divergence rather than a silent one.</summary>
    public readonly struct PrecompiledExtensionBinding
    {
        public PrecompiledExtensionBinding(string name, string extensionTypeName)
        {
            Name = name;
            ExtensionTypeName = extensionTypeName;
        }

        public string Name { get; }

        public string ExtensionTypeName { get; }
    }
}
