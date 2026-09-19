using System.Collections.Generic;

namespace Heddle.Language.Members
{
    /// <summary>
    /// The facts a type system must supply for the shared member walk, implemented over
    /// <c>Type</c>/<c>PropertyInfo</c>. No implementation gets to own the walk <i>order</i> — that lives in <see cref="MemberPathWalk"/>.
    /// </summary>
    internal interface ITypeModel<TType, TMember>
    {
        /// <summary>Whether the receiver is a <c>dynamic</c> scope — the walk splits into a dynamic hop there.</summary>
        bool IsDynamic(TType type);

        /// <summary>The properties <b>declared</b> by this exact type under the given ordinal, case-sensitive name.
        /// Inherited members are never included; the walk visits base types itself.</summary>
        IEnumerable<TMember> DeclaredProperties(TType type, string name);

        /// <summary>Whether this exact type declares a member of any <b>other</b> kind (field, method, event,
        /// nested type) under the given ordinal, case-sensitive name. Such a declaration hides an inherited
        /// property of that name exactly as a property would.</summary>
        bool DeclaresNonPropertyMember(TType type, string name);

        MemberFacts FactsOf(TMember member);

        TType TypeOf(TMember member);

        /// <summary>The base type, or the type-system's "no base" value (null for both adapters).</summary>
        TType BaseOf(TType type);

        bool IsInterface(TType type);

        /// <summary>The base-interface closure of an interface root. The reflection adapter deliberately returns
        /// nothing: <c>Type.GetProperty</c> on an interface does not search base interfaces, and this narrower
        /// behavior is normative.</summary>
        IEnumerable<TType> BaseInterfaces(TType type);
    }

    /// <summary>The canonical member-path walk order: receiver type, then base chain most-derived-first. The
    /// <b>most-derived declaration</b> of the requested name decides: an accessible property binds, and anything
    /// else — a <c>[Hidden]</c> override, a non-public or static <c>new</c> property, a field or method of that
    /// name — ends the walk as not-found. Walking past it would bind the base declaration the derived type
    /// replaced, and an overridden getter dispatches to the very member the derived type withheld. This also
    /// makes new-shadowing resolve deterministically rather than throwing AmbiguousMatchException.</summary>
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
                if (TryFindDeclared(model, current, name, declaredOnReceiver, out found, out bool declared))
                    return true;
                if (declared)
                    return false;
                declaredOnReceiver = false;
            }

            if (model.IsInterface(receiver))
            {
                foreach (var baseInterface in model.BaseInterfaces(receiver))
                {
                    if (TryFindDeclared(model, baseInterface, name, declaredOnReceiver: false, out found, out _))
                        return true;
                }
            }

            found = default;
            return false;
        }

        private static bool TryFindDeclared<TType, TMember>(ITypeModel<TType, TMember> model, TType type, string name,
            bool declaredOnReceiver, out TMember found, out bool declared)
        {
            declared = false;
            foreach (var member in model.DeclaredProperties(type, name))
            {
                declared = true;
                if (MemberVisibility.IsAccessible(model.FactsOf(member), declaredOnReceiver))
                {
                    found = member;
                    return true;
                }
            }

            if (!declared)
                declared = model.DeclaresNonPropertyMember(type, name);
            found = default;
            return false;
        }
    }
}
