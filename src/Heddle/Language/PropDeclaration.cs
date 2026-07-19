using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// One prop declaration from a definition header (<c>name: Type [= literal]</c>). Immutable after parse.
    /// </summary>
    public sealed class PropDeclaration
    {
        public PropDeclaration(string name, string typeName, bool hasDefault, object defaultValue, BlockPosition position)
        {
            Name = name;
            TypeName = typeName;
            HasDefault = hasDefault;
            DefaultValue = defaultValue;
            Position = position;
        }

        /// <summary>The prop name (ordinal, case-sensitive).</summary>
        public string Name { get; }

        /// <summary>The declared type name, resolved at compile time like <c>DefinitionItem.ModelType</c>.</summary>
        public string TypeName { get; }

        /// <summary>True when the declaration carries <c>= literal</c>; false means required.</summary>
        public bool HasDefault { get; }

        /// <summary>The decoded default literal (pre-conversion CLR value, <c>null</c> for the null literal);
        /// meaningful only when <see cref="HasDefault"/> is true.</summary>
        public object DefaultValue { get; }

        /// <summary>Declaration position (<c>DefinitionItem.Position</c> convention — offset-relative parse
        /// coordinates).</summary>
        public BlockPosition Position { get; }
    }
}
