using System.Collections.Generic;

namespace Heddle.Language.Members
{
    /// <summary>
    /// The facts a type system must supply for the shared member walk. Roslyn implements it over
    /// <c>ITypeSymbol</c>/<c>IPropertySymbol</c>; the runtime implements it over <c>Type</c>/<c>PropertyInfo</c>.
    /// Neither implementation gets to own the walk <i>order</i> — that lives in <see cref="MemberPathWalk"/>.
    /// </summary>
    internal interface ITypeModel<TType, TMember>
    {
        /// <summary>Whether the receiver is a <c>dynamic</c> scope — the walk splits into a dynamic hop there.</summary>
        bool IsDynamic(TType type);

        /// <summary>The properties <b>declared</b> by this exact type under the given ordinal, case-sensitive name.
        /// Inherited members are never included; the walk visits base types itself.</summary>
        IEnumerable<TMember> DeclaredProperties(TType type, string name);

        MemberFacts FactsOf(TMember member);

        TType TypeOf(TMember member);

        /// <summary>The base type, or the type-system's "no base" value (null for both adapters).</summary>
        TType BaseOf(TType type);

        bool IsInterface(TType type);

        /// <summary>The base-interface closure of an interface root. The reflection adapter deliberately returns
        /// nothing: <c>Type.GetProperty</c> on an interface does not search base interfaces, and under the OQ1
        /// ruling that narrower runtime behavior is normative until a breaking window widens it.</summary>
        IEnumerable<TType> BaseInterfaces(TType type);
    }

    /// <summary>
    /// The member-path walk order, stated once (phase 4 D7 / 04 F1). Before this file the generator and the runtime
    /// each carried a hand-written resolver, and the runtime's own doc comment claimed to be "the single source of
    /// member-path resolution semantics" while the generator was a second source with six verified divergences.
    /// <para>Order: the receiver type, then its base chain most-derived-first, taking the <b>first</b> accessible
    /// property with the requested name — which makes <c>new</c>-shadowing resolve deterministically to the
    /// most-derived accessible member instead of throwing <c>AmbiguousMatchException</c>. For an interface root the
    /// interface itself is searched, then whatever base-interface closure the adapter chooses to supply.</para>
    /// </summary>
    internal static class MemberPathWalk
    {
        /// <summary>Finds the one property a path segment binds to, or reports not-found.</summary>
        public static bool TryFind<TType, TMember>(ITypeModel<TType, TMember> model, TType receiver, string name,
            out TMember found)
        {
            found = default;
            if (model == null || receiver == null || string.IsNullOrEmpty(name))
                return false;

            bool declaredOnReceiver = true;
            for (var current = receiver; current != null; current = model.BaseOf(current))
            {
                if (TryFindDeclared(model, current, name, declaredOnReceiver, out found))
                    return true;
                declaredOnReceiver = false;
            }

            if (model.IsInterface(receiver))
            {
                foreach (var baseInterface in model.BaseInterfaces(receiver))
                {
                    if (TryFindDeclared(model, baseInterface, name, declaredOnReceiver: false, out found))
                        return true;
                }
            }

            found = default;
            return false;
        }

        private static bool TryFindDeclared<TType, TMember>(ITypeModel<TType, TMember> model, TType type, string name,
            bool declaredOnReceiver, out TMember found)
        {
            foreach (var member in model.DeclaredProperties(type, name))
            {
                if (MemberVisibility.IsAccessible(model.FactsOf(member), declaredOnReceiver))
                {
                    found = member;
                    return true;
                }
            }

            found = default;
            return false;
        }
    }
}
