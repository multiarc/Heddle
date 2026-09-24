using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Runtime.Expressions;

namespace Heddle.Language.Binding
{
    /// <summary>One decoded <c>[Prop]</c> declaration, layer-tagged.</summary>
    internal sealed class PropDeclaration
    {
        /// <summary>Declared name; may be null/empty — that is <see cref="PropFault.NameInvalid"/>, not a reason
        /// to drop the declaration (dropping it would silently treat the owner as parameter-less).</summary>
        public string Name;

        public Type Type;

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

    /// <summary>One resolved layout slot. <see cref="Index"/> is the wire format between the build's frozen
    /// <c>object[]</c> prototype and the runtime's <c>ExtensionParameterCarrier</c>.</summary>
    internal sealed class PropSlot
    {
        public string Name;
        public Type Type;
        public int Index;
        public bool HasDefault;
        public object DefaultBoxed;

        /// <summary>The declaration that last wrote this slot — carries the caller's <see cref="PropDeclaration.Tag"/>.</summary>
        public PropDeclaration Declaration;
    }

    /// <summary>The per-caller hooks the core cannot express over the declarations alone.</summary>
    internal interface IPropLayoutSink
    {
        /// <summary>Reports one fault. A sink must keep <b>accumulating and continuing</b> — the runtime's
        /// shape — so a declaration list with two faults reports both, in declaration order.</summary>
        void Fault(PropFault fault, PropDeclaration declaration, Type relatedType, string relatedDisplay);

        /// <summary>Applies the default-conversion rule. Returns false (having reported nothing) when the
        /// default cannot be converted; the core then raises <see cref="PropFault.DefaultNotConvertible"/> so the
        /// fault ordering stays owned here. <paramref name="sourceDisplay"/> names the default's own type for the
        /// shared message.</summary>
        bool TryConvertDefault(PropDeclaration declaration, Type targetType, out object converted,
            out string sourceDisplay);
    }

    /// <summary>The sole implementation of extension prop-layout sequencing and slot indexing; slot indices are a wire format.</summary>
    internal static class PropLayoutCore
    {
        /// <summary>Builds the slot list. Returns the slots in index order; <paramref name="faulted"/> is true when
        /// any declaration produced a fault (the layout is refused in that case, but only after reporting
        /// every fault).</summary>
        internal static List<PropSlot> Build(IReadOnlyList<PropDeclaration> declarations, IPropLayoutSink sink,
            out bool faulted)
        {
            var slots = new List<PropSlot>();
            var byName = new Dictionary<string, PropSlot>(System.StringComparer.Ordinal);
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
                    sink.Fault(PropFault.NameInvalid, declaration, null, null);
                    faulted = true;
                    continue;
                }

                if (HeddleDiagnosticCatalog.PropFaults.IsReserved(declaration.Name))
                {
                    sink.Fault(PropFault.NameReserved, declaration, null, null);
                    faulted = true;
                    continue;
                }

                if (!seenAtLevel.Add(declaration.Name))
                {
                    sink.Fault(PropFault.DuplicateAtLevel, declaration, null, null);
                    faulted = true;
                    continue;
                }

                if (!ReflectionTypeFacts.IsUsableAsPropType(declaration.Type))
                {
                    sink.Fault(PropFault.TypeUnusable, declaration, null, null);
                    faulted = true;
                    continue;
                }

                if (byName.TryGetValue(declaration.Name, out var existing))
                {
                    if (!ReflectionTypeFacts.IsAssignableFrom(existing.Type, declaration.Type))
                    {
                        sink.Fault(PropFault.RedeclarationNotAssignable, declaration, existing.Type,
                            ReflectionTypeFacts.Display(existing.Type));
                        faulted = true;
                        continue;
                    }

                    existing.Type = declaration.Type;
                    existing.Declaration = declaration;
                    if (!ApplyDefault(declaration, sink, existing))
                        faulted = true;
                }
                else
                {
                    var slot = new PropSlot
                    {
                        Name = declaration.Name,
                        Type = declaration.Type,
                        Index = slots.Count,
                        Declaration = declaration
                    };
                    if (!ApplyDefault(declaration, sink, slot))
                        faulted = true;
                    slots.Add(slot);
                    byName.Add(declaration.Name, slot);
                }
            }

            return slots;
        }

        private static bool ApplyDefault(PropDeclaration declaration, IPropLayoutSink sink, PropSlot slot)
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
        /// from the live extension type; the AQN comes from the one <c>AqnFormatter</c>, so equal layouts give
        /// byte-equal strings. <c>null</c> for an empty layout, which is what makes the gauntlet check vacuous
        /// where there is nothing to check.
        /// </summary>
        internal static string Fingerprint(IReadOnlyList<PropSlot> slots)
        {
            if (slots == null || slots.Count == 0)
                return null;

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < slots.Count; i++)
            {
                if (i != 0)
                    builder.Append('|');
                builder.Append(slots[i].Name).Append(':').Append(ReflectionTypeFacts.FormatAqn(slots[i].Type));
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
