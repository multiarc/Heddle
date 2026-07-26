using System.Collections.Generic;
using Heddle.Data;

namespace Heddle.Language.Binding
{
    /// <summary>One decoded <c>[Prop]</c> declaration, layer-tagged. <typeparamref name="TType"/> is whatever the
    /// consuming tier calls a type; the core only ever asks <see cref="ITypeFacts{TType}"/> about it.</summary>
    internal sealed class PropDeclaration<TType>
    {
        /// <summary>Declared name; may be null/empty — that is <see cref="PropFault.NameInvalid"/>, not a reason
        /// to drop the declaration (dropping it would silently treat the owner as parameter-less).</summary>
        public string Name;

        public TType Type;

        /// <summary>Base-chain layer index, outermost base first. A repeated name at a HIGHER level is an
        /// inherited re-declaration; at the SAME level it is a duplicate.</summary>
        public int Level;

        /// <summary>Derived optionality: a default value was given, or the declaration is explicitly optional.</summary>
        public bool HasDefault;

        /// <summary>The declared default (null both for "no default" — see <see cref="HasDefault"/> — and for an
        /// explicit null default).</summary>
        public object DefaultValue;

        /// <summary>Opaque payload the caller uses to carry its own per-declaration state (a position, the
        /// default's source type) through the core without the core knowing about it.</summary>
        public object Tag;
    }

    /// <summary>One resolved layout slot. <see cref="Index"/> is the wire format between the generator's frozen
    /// <c>object[]</c> prototype and the runtime's <c>ExtensionParameterCarrier</c>.</summary>
    internal sealed class PropSlot<TType>
    {
        public string Name;
        public TType Type;
        public int Index;
        public bool HasDefault;
        public object DefaultBoxed;

        /// <summary>The declaration that last wrote this slot — carries the caller's <see cref="PropDeclaration{TType}.Tag"/>.</summary>
        public PropDeclaration<TType> Declaration;
    }

    /// <summary>The per-tier hooks the core cannot express type-agnostically.</summary>
    internal interface IPropLayoutSink<TType>
    {
        /// <summary>Reports one fault. Both tiers must keep <b>accumulating and continuing</b> — the runtime's
        /// shape — so a declaration list with two faults reports both, in declaration order.</summary>
        void Fault(PropFault fault, PropDeclaration<TType> declaration, TType relatedType, string relatedDisplay);

        /// <summary>Applies the default-conversion rule. Returns false (having reported nothing) when the
        /// default cannot be converted; the core then raises <see cref="PropFault.DefaultNotConvertible"/> so the
        /// fault ordering stays owned here. <paramref name="sourceDisplay"/> names the default's own type for the
        /// shared message.</summary>
        bool TryConvertDefault(PropDeclaration<TType> declaration, TType targetType, out object converted,
            out string sourceDisplay);
    }

    /// <summary>The sole implementation of extension prop-layout sequencing and slot indexing; slot indices are a wire format.</summary>
    internal static class PropLayoutCore
    {
        /// <summary>The runtime's layer-walk stop rule, named so both adapters cite one place: the base chain is
        /// walked outermost-first and stops <b>at</b> <c>System.Object</c> (which is never a layer).</summary>
        internal const bool StopsAtObject = true;

        /// <summary>Builds the slot list. Returns the slots in index order; <paramref name="faulted"/> is true when
        /// any declaration produced a fault (both tiers refuse the layout in that case, but only after reporting
        /// every fault).</summary>
        internal static List<PropSlot<TType>> Build<TType>(IReadOnlyList<PropDeclaration<TType>> declarations,
            ITypeFacts<TType> facts, IPropLayoutSink<TType> sink, out bool faulted)
        {
            var slots = new List<PropSlot<TType>>();
            var byName = new Dictionary<string, PropSlot<TType>>(System.StringComparer.Ordinal);
            faulted = false;

            if (declarations == null)
                return slots;

            int currentLevel = int.MinValue;
            HashSet<string> seenAtLevel = null;

            for (int i = 0; i < declarations.Count; i++)
            {
                var declaration = declarations[i];
                if (declaration.Level != currentLevel)
                {
                    currentLevel = declaration.Level;
                    seenAtLevel = new HashSet<string>(System.StringComparer.Ordinal);
                }

                if (IsNullOrWhiteSpace(declaration.Name))
                {
                    sink.Fault(PropFault.NameInvalid, declaration, default(TType), null);
                    faulted = true;
                    continue;
                }

                if (HeddleDiagnosticCatalog.PropFaults.IsReserved(declaration.Name))
                {
                    sink.Fault(PropFault.NameReserved, declaration, default(TType), null);
                    faulted = true;
                    continue;
                }

                if (!seenAtLevel.Add(declaration.Name))
                {
                    sink.Fault(PropFault.DuplicateAtLevel, declaration, default(TType), null);
                    faulted = true;
                    continue;
                }

                if (!facts.IsUsableAsPropType(declaration.Type))
                {
                    sink.Fault(PropFault.TypeUnusable, declaration, default(TType), null);
                    faulted = true;
                    continue;
                }

                if (byName.TryGetValue(declaration.Name, out var existing))
                {
                    if (!facts.IsAssignableFrom(existing.Type, declaration.Type))
                    {
                        sink.Fault(PropFault.RedeclarationNotAssignable, declaration, existing.Type,
                            facts.Display(existing.Type));
                        faulted = true;
                        continue;
                    }

                    existing.Type = declaration.Type;
                    existing.Declaration = declaration;
                    if (!ApplyDefault(declaration, facts, sink, existing))
                        faulted = true;
                }
                else
                {
                    var slot = new PropSlot<TType>
                    {
                        Name = declaration.Name,
                        Type = declaration.Type,
                        Index = slots.Count,
                        Declaration = declaration
                    };
                    if (!ApplyDefault(declaration, facts, sink, slot))
                        faulted = true;
                    slots.Add(slot);
                    byName.Add(declaration.Name, slot);
                }
            }

            return slots;
        }

        private static bool ApplyDefault<TType>(PropDeclaration<TType> declaration, ITypeFacts<TType> facts,
            IPropLayoutSink<TType> sink, PropSlot<TType> slot)
        {
            if (!declaration.HasDefault)
            {
                slot.HasDefault = false;
                slot.DefaultBoxed = null;
                return true;
            }

            if (!sink.TryConvertDefault(declaration, slot.Type, out var converted, out var sourceDisplay))
            {
                sink.Fault(PropFault.DefaultNotConvertible, declaration, slot.Type,
                    sourceDisplay ?? (declaration.DefaultValue == null ? "null" : null));
                slot.HasDefault = false;
                slot.DefaultBoxed = null;
                return false;
            }

            slot.HasDefault = true;
            slot.DefaultBoxed = converted;
            return true;
        }

        /// <summary>
        /// The slot layout as one string — ordered <c>name:&lt;slot type AQN&gt;</c> pairs joined
        /// with <c>|</c>. The manifest carries the build tier's value and the gauntlet recomputes the run tier's
        /// from the live extension type; the AQN comes from the one <c>AqnFormatter</c> through each side's
        /// <see cref="ITypeFacts{TType}"/>, so equal layouts give byte-equal strings. <c>null</c> for an empty
        /// layout, which is what makes the gauntlet check vacuous where there is nothing to check.
        /// </summary>
        internal static string Fingerprint<TType>(IReadOnlyList<PropSlot<TType>> slots, ITypeFacts<TType> facts)
        {
            if (slots == null || slots.Count == 0 || facts == null)
                return null;

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < slots.Count; i++)
            {
                if (i != 0)
                    builder.Append('|');
                builder.Append(slots[i].Name).Append(':').Append(facts.FormatAqn(slots[i].Type));
            }

            return builder.ToString();
        }

        private static bool IsNullOrWhiteSpace(string value)
        {
            if (value == null || value.Length == 0)
                return true;
            for (int i = 0; i < value.Length; i++)
                if (!char.IsWhiteSpace(value[i]))
                    return false;
            return true;
        }
    }
}
