using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.LanguageServices
{
    /// <summary>One prop of a definition (phase 5 declaration, resolved).</summary>
    public sealed class PropInfo
    {
        internal PropInfo(string name, string typeName, ExType type, bool isRequired, object defaultValue,
            int declarationOffset, int declarationLength)
        {
            Name = name;
            TypeName = typeName;
            Type = type;
            IsRequired = isRequired;
            DefaultValue = defaultValue;
            DeclarationOffset = declarationOffset;
            DeclarationLength = declarationLength;
        }

        public string Name { get; }
        public string TypeName { get; }
        public ExType Type { get; }
        public bool IsRequired { get; }
        public object DefaultValue { get; }
        public int DeclarationOffset { get; }
        public int DeclarationLength { get; }
    }

    /// <summary>
    /// One definition visible in a document. <see cref="ModelType"/> is null when unresolved or abstract;
    /// <see cref="IsPinned"/> = <c>:: Type</c> resolving to non-object (D13); <see cref="Props"/> are
    /// inheritance-flattened (phase 5 D6 rules).
    /// </summary>
    public sealed class DefinitionInfo
    {
        internal DefinitionInfo(string name, string sourcePath, int offset, int length, string modelTypeName,
            ExType modelType, bool isPinned, IReadOnlyList<PropInfo> props, string slotTypeName, string baseName)
        {
            Name = name;
            SourcePath = sourcePath;
            Offset = offset;
            Length = length;
            ModelTypeName = modelTypeName;
            ModelType = modelType;
            IsPinned = isPinned;
            Props = props;
            SlotTypeName = slotTypeName;
            BaseName = baseName;
        }

        public string Name { get; }
        public string SourcePath { get; }
        public int Offset { get; }
        public int Length { get; }
        public string ModelTypeName { get; }
        public ExType ModelType { get; }
        public bool IsPinned { get; }
        public IReadOnlyList<PropInfo> Props { get; }
        public string SlotTypeName { get; }
        public string BaseName { get; }
    }
}
