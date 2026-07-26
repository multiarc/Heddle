using System.Collections.Generic;
using System.Text;
using Heddle.Language.Members;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// Emits null-safe member-path accessor as C# using <c>ModelParameter.BuildNullSafePropertyChain</c>
    /// semantics: direct access for value receivers; <c>default(T)</c> via conditional for null references.
    /// </summary>
    internal static class MemberPathWriter
    {
        internal readonly struct HopEmit
        {
            public HopEmit(bool receiverIsValueType, bool propertyIsNonNullableValue, string propertyTypeName, string name)
            {
                ReceiverIsValueType = receiverIsValueType;
                PropertyIsNonNullableValue = propertyIsNonNullableValue;
                PropertyTypeName = propertyTypeName;
                Name = name;
            }

            public bool ReceiverIsValueType { get; }
            public bool PropertyIsNonNullableValue { get; }
            public string PropertyTypeName { get; }
            public string Name { get; }
        }

        /// <summary>Writes the null-safe accessor for the typed <paramref name="hops"/> rooted at
        /// <paramref name="rootExpr"/> (already cast to the first receiver type).</summary>
        public static string Write(string rootExpr, IReadOnlyList<HopEmit> hops)
        {
            var current = rootExpr;
            foreach (var hop in hops)
            {
                // Must stay in sync with MemberHopRule.Form (shared by parameter and expression tiers).
                switch (MemberHopRule.Form(hop.ReceiverIsValueType, hop.PropertyIsNonNullableValue))
                {
                    case HopForm.Direct:
                        current = current + "." + hop.Name;
                        break;
                    case HopForm.NullDefaultConditional:
                        current = $"({current} == null ? default({hop.PropertyTypeName}) : {current}.{hop.Name})";
                        break;
                    default:
                        current = current + "?." + hop.Name;
                        break;
                }
            }

            return current;
        }

        public static string Display(IReadOnlyList<string> segments)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < segments.Count; i++)
            {
                if (i != 0)
                    sb.Append('.');
                sb.Append(segments[i]);
            }

            return sb.ToString();
        }
    }
}
