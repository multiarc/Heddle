using System.Collections.Generic;
using System.Text;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// Emits a member-path accessor as C# reproducing the runtime member tier hop by hop (phase 7 generated-code
    /// protocol rule 6; <c>ModelParameter.BuildNullSafePropertyChain</c>): a hop off a value-typed receiver accesses
    /// directly; a hop off a reference receiver yields <c>default(propertyType)</c> when the receiver is null — which
    /// is <c>?.</c> for a reference/nullable property type and the explicit conditional for a non-nullable value
    /// property type (where <c>?.</c> would produce <c>Nullable&lt;T&gt;</c>/boxed-null instead of boxed default).
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
                if (hop.ReceiverIsValueType)
                {
                    current = current + "." + hop.Name;
                }
                else if (hop.PropertyIsNonNullableValue)
                {
                    current = $"({current} == null ? default({hop.PropertyTypeName}) : {current}.{hop.Name})";
                }
                else
                {
                    current = current + "?." + hop.Name;
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
