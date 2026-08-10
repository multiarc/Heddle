// Deliberately in the global namespace, and the only fixture that is. The runtime's type index keys a full name on
// `type.Namespace + "." + shortName`, and Type.Namespace is null here — so this type answers to a spelling with a
// leading dot and to nothing else, which is a spelling no namespaced fixture can produce.

/// <summary>A model type with no namespace, for the type-name spellings only such a type has.</summary>
public sealed class GlobalNamespaceModel
{
    public string Name { get; set; }

    /// <summary>Nested, so the dotted alias of a nested name meets the leading dot of a namespace-less one.</summary>
    public sealed class Inner
    {
        public string Tag { get; set; }
    }
}
