using System;
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
                string name, bool propertyIsNullableConstructible = true, string receiverTypeName = null)
            {
                ReceiverIsValueType = receiverIsValueType;
                PropertyIsNonNullableValue = propertyIsNonNullableValue;
                PropertyTypeName = propertyTypeName;
                Name = name;
                PropertyIsNullableConstructible = propertyIsNullableConstructible;
                ReceiverTypeName = receiverTypeName;
            }

            public bool ReceiverIsValueType { get; }
            public bool PropertyIsNonNullableValue { get; }
            public string PropertyTypeName { get; }
            public string Name { get; }

            /// <summary>The receiver's type, needed only where the hop has to bind the receiver to a local to read
            /// it once.</summary>
            public string ReceiverTypeName { get; }

            /// <summary>Whether <c>T?</c> exists for this property's type. A ref struct — <c>Span&lt;T&gt;</c> and
            /// friends — cannot be made nullable, so <c>?.</c> on one does not compile.</summary>
            public bool PropertyIsNullableConstructible { get; }
        }

        /// <summary>Writes the null-safe accessor for the typed <paramref name="hops"/> rooted at
        /// <paramref name="rootExpr"/> (already cast to the first receiver type).
        /// <para><paramref name="allocateLocal"/> hands back a name unused anywhere else in the generated file. Only
        /// a hop onto a ref struct needs one, and it needs it badly — see below.</para></summary>
        public static string Write(string rootExpr, IReadOnlyList<HopEmit> hops, Func<string> allocateLocal)
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
                        // Unless the widening is impossible: a ref struct has no nullable form, so `?.` on one is a
                        // compile error in the consumer's project and the null test has to be written out. A type
                        // pattern does it while naming the receiver once. Spelling the receiver twice instead — a
                        // plain `x == null ? default : x.P` — evaluates everything above the hop twice, which the
                        // engine does not, so a property whose answer moves between the test and the read gave the
                        // two tiers different answers: a getter returning null on its second call turned a rendered
                        // value into a NullReferenceException. It also doubled the reads, and doubled them again per
                        // such hop.
                        if (hop.PropertyIsNullableConstructible)
                        {
                            current = $"({current}?.{hop.Name} ?? default({hop.PropertyTypeName}))";
                        }
                        else
                        {
                            var receiver = allocateLocal();
                            current = $"({current} is {hop.ReceiverTypeName} {receiver} " +
                                      $"? {receiver}.{hop.Name} : default({hop.PropertyTypeName}))";
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
