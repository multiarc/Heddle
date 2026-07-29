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

            // Whether `current` ends in a `?.` whose null result would propagate outward. C# gives one `?.` authority
            // over everything to its right in the same member-access chain: `a?.B.C` yields null when `a` is null and
            // never reads `C` at all. The engine has no such rule — it defaults the hop that failed and reads `C` off
            // that default — so the propagation has to be stopped before the next plain `.`.
            var propagatesNull = false;

            foreach (var hop in hops)
            {
                // Must stay in sync with MemberHopRule.Form (shared by parameter and expression tiers).
                switch (MemberHopRule.Form(hop.ReceiverIsValueType, hop.PropertyIsNonNullableValue))
                {
                    case HopForm.Direct:
                        // Parenthesising ends the chain: `(a?.B).C` reads `C` off `default(B)` when `a` is null,
                        // which is the hop the engine performs. Without it the two tiers disagree on a value both
                        // can render — `Inner.Maybe.HasValue` over a null `Inner` is False to the engine and empty
                        // here — and where the engine throws on `default(T).Value`, this quietly rendered nothing.
                        if (propagatesNull)
                        {
                            current = "(" + current + ")";
                            propagatesNull = false;
                        }

                        current = current + "." + hop.Name;
                        break;
                    case HopForm.NullDefaultConditional:
                        // `?.` on a non-nullable value member widens to Nullable<T>, so the null case is written back
                        // out with `??`. Naming the receiver once matters: a conditional spelling would evaluate
                        // everything to its left twice, and the engine evaluates it once.
                        // Unless the widening is impossible: a ref struct has no nullable form, and `?.` on one is a
                        // compile error in the consumer's project. There the receiver is spelled twice — correctness
                        // first. What gets duplicated is the prefix, once, and only for a ref-struct hop off a
                        // reference receiver; hops after it append to the finished conditional rather than into both
                        // of its arms. Reaching a second such hop means going ref struct, back out to a reference,
                        // and into another ref struct, which no real model does.
                        if (hop.PropertyIsNullableConstructible)
                        {
                            current = $"({current}?.{hop.Name} ?? default({hop.PropertyTypeName}))";
                        }
                        else
                        {
                            // The read half spells the receiver out again, so a propagating chain would carry its
                            // `?.` onto a type with no nullable form — CS8978 in the consumer's build. Closing the
                            // chain first costs the parentheses and nothing else.
                            if (propagatesNull)
                                current = "(" + current + ")";
                            current = $"({current} == null ? default({hop.PropertyTypeName}) : {current}.{hop.Name})";
                        }

                        propagatesNull = false;
                        break;
                    default:
                        current = current + "?." + hop.Name;
                        propagatesNull = true;
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
