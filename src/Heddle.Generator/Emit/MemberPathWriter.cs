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
            public HopEmit(bool receiverIsValueType, bool propertyIsNonNullableValue, string propertyTypeName,
                string name, bool propertyIsNullableConstructible = true)
            {
                ReceiverIsValueType = receiverIsValueType;
                PropertyIsNonNullableValue = propertyIsNonNullableValue;
                PropertyTypeName = propertyTypeName;
                Name = name;
                PropertyIsNullableConstructible = propertyIsNullableConstructible;
            }

            public bool ReceiverIsValueType { get; }
            public bool PropertyIsNonNullableValue { get; }
            public string PropertyTypeName { get; }
            public string Name { get; }

            /// <summary>Whether <c>T?</c> exists for this property's type. A ref struct — <c>Span&lt;T&gt;</c> and
            /// friends — cannot be made nullable, so <c>?.</c> on one does not compile.</summary>
            public bool PropertyIsNullableConstructible { get; }
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
                        // `?.` on a non-nullable value member widens to Nullable<T>, so the null case is written back
                        // out with `??`. Naming the receiver once matters: a conditional spelling would evaluate
                        // everything to its left twice, and the engine evaluates it once.
                        // Unless the widening is impossible: a ref struct has no nullable form, and `?.` on one is a
                        // compile error in the consumer's project. There the receiver is spelled twice, which is what
                        // this form has always done for such a type — correctness first, and no ref-struct hop can
                        // be deep enough for the duplication to matter, since it can only ever be the last one.
                        current = hop.PropertyIsNullableConstructible
                            ? $"({current}?.{hop.Name} ?? default({hop.PropertyTypeName}))"
                            : $"({current} == null ? default({hop.PropertyTypeName}) : {current}.{hop.Name})";
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
