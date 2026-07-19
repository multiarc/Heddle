using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// One named-content-region declaration of a component body (phase 7 D2/D3): a public
    /// <c>&lt;:name&gt;</c> / <c>&lt;:name :: Type&gt;</c> region or a private inner <c>&lt;name&gt;</c>
    /// definition. Mirrors <see cref="PropDeclaration"/>. Immutable after parse; parse-model-pure
    /// (shared with the generator's compile-linked sources).
    /// </summary>
    internal sealed class RegionDeclaration
    {
        internal RegionDeclaration(string name, string typeName, bool isPublic, BlockPosition position)
        {
            Name = name;
            TypeName = typeName;
            IsPublic = isPublic;
            Position = position;
        }

        /// <summary>The region name (ordinal, case-sensitive).</summary>
        internal string Name { get; }

        /// <summary>The declared model type name (<c>&lt;:item :: Article&gt;</c>), or <c>"object"</c> when omitted.</summary>
        internal string TypeName { get; }

        /// <summary><c>&lt;:name&gt;</c> ⇒ true; a component's private inner <c>&lt;name&gt;</c> ⇒ false.</summary>
        internal bool IsPublic { get; }

        /// <summary>Declaration position (<see cref="DefinitionItem.Position"/> convention).</summary>
        internal BlockPosition Position { get; }
    }
}
