using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Heddle.Runtime.Expressions;

namespace Heddle.Language.Members
{
    /// <summary>The canonical member-path walk order: receiver type, then base chain most-derived-first. The
    /// <b>most-derived declaration</b> of the requested name decides: an accessible property binds, and anything
    /// else — a <c>[Hidden]</c> override, a non-public or static <c>new</c> property, a field or method of that
    /// name — ends the walk as not-found. Walking past it would bind the base declaration the derived type
    /// replaced, and an overridden getter dispatches to the very member the derived type withheld. This also
    /// makes new-shadowing resolve deterministically rather than throwing AmbiguousMatchException.
    /// <para>The base-interface closure of an interface receiver is deliberately not walked:
    /// <c>Type.GetProperty</c> on an interface does not search base interfaces, and this narrower behavior is
    /// normative.</para></summary>
    internal static class MemberPathWalk
    {
        /// <summary>Finds the one property a path segment binds to, or reports not-found.</summary>
        public static bool TryFind(Type receiver, string name, out PropertyInfo found)
        {
            found = null;
            if (receiver == null || string.IsNullOrEmpty(name))
                return false;

            bool declaredOnReceiver = true;
            for (var current = receiver; current != null; current = current.BaseType)
            {
                if (TryFindDeclared(current, name, declaredOnReceiver, out found, out bool declared))
                    return true;
                if (declared)
                    return false;
                declaredOnReceiver = false;
            }

            found = null;
            return false;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection over a model type; model types reach the engine through [HeddleModelAssembly]/typeof parameters annotated DynamicallyAccessedMemberTypes.All, which keeps their members through a trimmed publish.")]
        private static bool TryFindDeclared(Type type, string name, bool declaredOnReceiver, out PropertyInfo found,
            out bool declared)
        {
            declared = false;
            foreach (var property in type.GetProperties(MemberPathResolver.DeclaredBindingFlags))
            {
                if (!string.Equals(property.Name, name, StringComparison.Ordinal))
                    continue;

                declared = true;
                if (MemberVisibility.IsAccessible(MemberPathResolver.FactsOf(property), declaredOnReceiver))
                {
                    found = property;
                    return true;
                }
            }

            if (!declared)
                declared = MemberPathResolver.DeclaresNonPropertyMember(type, name);
            found = null;
            return false;
        }
    }
}
