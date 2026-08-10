using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Typing
{
    internal sealed class PropSlotInfo
    {
        public string Name;
        public ITypeSymbol Type;
        public string TypeFq;
        public bool HasDefault;
        public object DefaultValue;   // decoded literal (pre-conversion CLR value)

        /// <summary>The default's own declared type, where the declaration named one. Metadata hands an enum
        /// constant over as its underlying primitive, so the value alone cannot say which of the two the
        /// runtime will box. Null for a template-declared prop, whose default is a parsed literal.</summary>
        public ITypeSymbol DefaultSourceType;
        public int Index;
    }

    internal sealed class PropLayoutInfo
    {
        public readonly List<PropSlotInfo> Slots = new List<PropSlotInfo>();
        public readonly Dictionary<string, PropSlotInfo> ByName =
            new Dictionary<string, PropSlotInfo>(System.StringComparer.Ordinal);
        public bool Failed;
        public int Count => Slots.Count;
    }
}
