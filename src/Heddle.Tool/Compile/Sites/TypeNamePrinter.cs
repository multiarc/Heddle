using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Data;

namespace Heddle.Tool.Compile.Sites
{
    /// <summary>C# spellings for the types the printer must name from the consumer's assembly, plus
    /// the nameability verdicts: a non-public type, an internal member of another
    /// assembly, an [Obsolete(error: true)] member, or a type with no C# spelling (a
    /// compiler-generated name, an open generic, a pointer) is declined, never guessed.</summary>
    internal static class TypeNamePrinter
    {
        internal static string DynamicSpelling(ExType type) =>
            type != null && type.IsDynamic ? "dynamic" : null;

        internal static bool TrySpell(ExType type, out string spelling, out string why)
        {
            spelling = null;
            why = null;
            if (type == null)
            {
                spelling = "object";
                return true;
            }

            if (type.IsDynamic)
            {
                spelling = "dynamic";
                return true;
            }

            return TrySpell(type.Type, out spelling, out why);
        }

        internal static bool TrySpell(Type type, out string spelling, out string why)
        {
            bool nonPublic = false;
            return TrySpell(type, null, ref nonPublic, out spelling, out why);
        }

        /// <summary>The spelling for an entry-point wrapper's model type. The wrapper is declared in the
        /// consumer's own assembly, so unlike a printed site it may name an internal type — but only one the
        /// consumer provably sees: a type of the assembly named <paramref name="consumerAssembly"/> itself
        /// (the project's own models), or of an assembly whose <c>InternalsVisibleTo</c> names it. Naming any
        /// other internal type is CS0122 in the consumer's build, so it has no spelling here and the caller
        /// degrades; with no consumer name nothing internal is provable. <paramref name="isPublic"/> says
        /// whether the wrapper may be public. A private or protected nested type never has a spelling outside
        /// its declaring type.</summary>
        internal static bool TrySpellModel(Type type, string consumerAssembly, out string spelling,
            out bool isPublic, out string why)
        {
            bool nonPublic = false;
            bool spelled = TrySpell(type, consumerAssembly ?? string.Empty, ref nonPublic, out spelling, out why);
            isPublic = !nonPublic;
            return spelled;
        }

        private static bool TrySpell(Type type, string internalsFor, ref bool nonPublic, out string spelling,
            out string why)
        {
            spelling = null;
            why = null;
            if (type == null)
            {
                spelling = "object";
                return true;
            }

            if (type == typeof(void))
            {
                spelling = "void";
                return true;
            }

            if (type == typeof(object))
            {
                spelling = "object";
                return true;
            }

            if (type == typeof(string))
            {
                spelling = "string";
                return true;
            }

            if (type == typeof(bool))
            {
                spelling = "bool";
                return true;
            }

            if (type == typeof(char))
            {
                spelling = "char";
                return true;
            }

            if (type == typeof(sbyte))
            {
                spelling = "sbyte";
                return true;
            }

            if (type == typeof(byte))
            {
                spelling = "byte";
                return true;
            }

            if (type == typeof(short))
            {
                spelling = "short";
                return true;
            }

            if (type == typeof(ushort))
            {
                spelling = "ushort";
                return true;
            }

            if (type == typeof(int))
            {
                spelling = "int";
                return true;
            }

            if (type == typeof(uint))
            {
                spelling = "uint";
                return true;
            }

            if (type == typeof(long))
            {
                spelling = "long";
                return true;
            }

            if (type == typeof(ulong))
            {
                spelling = "ulong";
                return true;
            }

            if (type == typeof(float))
            {
                spelling = "float";
                return true;
            }

            if (type == typeof(double))
            {
                spelling = "double";
                return true;
            }

            if (type == typeof(decimal))
            {
                spelling = "decimal";
                return true;
            }

            if (type.IsGenericParameter)
            {
                why = "open generic parameter '" + type.Name + "'";
                return false;
            }

            if (type.IsPointer)
            {
                why = "pointer type";
                return false;
            }

            if (type.IsByRef)
            {
                why = "by-ref type";
                return false;
            }

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                string inner;
                string innerWhy;
                if (!TrySpell(underlying, internalsFor, ref nonPublic, out inner, out innerWhy))
                {
                    why = innerWhy;
                    return false;
                }

                spelling = inner + "?";
                return true;
            }

            if (type.IsArray)
            {
                // C# reads rank specifiers outermost first — an array of int[,] is int[][,] — so they are
                // gathered on the way in and follow the innermost element type in that order.
                var ranks = new System.Text.StringBuilder();
                var innermost = type;
                while (innermost.IsArray)
                {
                    ranks.Append('[').Append(',', innermost.GetArrayRank() - 1).Append(']');
                    innermost = innermost.GetElementType();
                }

                string element;
                string elementWhy;
                if (!TrySpell(innermost, internalsFor, ref nonPublic, out element, out elementWhy))
                {
                    why = elementWhy;
                    return false;
                }

                spelling = element + ranks;
                return true;
            }

            if (type.IsGenericType && type.ContainsGenericParameters)
            {
                why = "open generic type '" + type.Name + "'";
                return false;
            }

            return TrySpellNamed(type, internalsFor, ref nonPublic, out spelling, out why);
        }

        /// <summary>Spells a named type through its declaring chain. Reflection names a generic with an
        /// arity suffix and hands a nested type the type arguments of every enclosing type as one flat
        /// list, so each part drops its suffix and takes its own slice of that list.</summary>
        private static bool TrySpellNamed(Type type, string internalsFor, ref bool nonPublic, out string spelling,
            out string why)
        {
            spelling = null;
            why = null;
            if (!IsNameable(type, internalsFor, ref nonPublic))
            {
                why = "non-public type '" + (type.FullName ?? type.Name) + "'";
                return false;
            }

            var chain = new List<Type>();
            for (var current = type; current != null; current = current.DeclaringType)
                chain.Add(current);
            chain.Reverse();

            var arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            var parts = new List<string>(chain.Count);
            int consumed = 0;
            foreach (var part in chain)
            {
                string name = part.Name;
                if (string.IsNullOrEmpty(name) || name.IndexOf('<') >= 0 || name.IndexOf('>') >= 0)
                {
                    why = "type with no C# spelling";
                    return false;
                }

                int tick = name.IndexOf('`');
                if (tick >= 0)
                    name = name.Substring(0, tick);
                name = Heddle.Helpers.TypeNameHelper.CreateEscapedIdentifier(name);
                int upTo = part.IsGenericType ? part.GetGenericArguments().Length : consumed;
                if (upTo > arguments.Length)
                    upTo = arguments.Length;
                if (upTo > consumed)
                {
                    var spelled = new string[upTo - consumed];
                    for (int i = consumed; i < upTo; i++)
                    {
                        string argument;
                        string argumentWhy;
                        if (!TrySpell(arguments[i], internalsFor, ref nonPublic, out argument, out argumentWhy))
                        {
                            why = argumentWhy;
                            return false;
                        }

                        spelled[i - consumed] = argument;
                    }

                    name += "<" + string.Join(", ", spelled) + ">";
                    consumed = upTo;
                }

                parts.Add(name);
            }

            string dotted = string.Join(".", parts.ToArray());
            string ns = type.Namespace;
            spelling = "global::" + (string.IsNullOrEmpty(ns)
                ? dotted
                : Heddle.Helpers.TypeNameHelper.EscapeDottedName(ns) + "." + dotted);
            return true;
        }

        private static bool IsNameable(Type type, string internalsFor, ref bool nonPublic)
        {
            for (var current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsNested ? current.IsNestedPublic : current.IsPublic)
                    continue;
                bool assemblyVisible = current.IsNested
                    ? current.IsNestedAssembly || current.IsNestedFamORAssem
                    : current.IsNotPublic;
                if (!assemblyVisible || !GrantsInternals(current.Assembly, internalsFor))
                    return false;
                nonPublic = true;
            }

            return true;
        }

        /// <summary>Whether <paramref name="consumer"/> can name <paramref name="declaring"/>'s internal types:
        /// it is that assembly, or is named by one of its <c>InternalsVisibleTo</c> grants. A grant carrying a
        /// public key is honoured only by a consumer signed with that key, which is not knowable here, so it
        /// proves nothing.</summary>
        private static bool GrantsInternals(Assembly declaring, string consumer)
        {
            if (string.IsNullOrEmpty(consumer))
                return false;
            if (string.Equals(declaring.GetName().Name, consumer, StringComparison.OrdinalIgnoreCase))
                return true;
            foreach (var attribute in declaring.GetCustomAttributesData())
            {
                if (attribute.AttributeType.FullName !=
                        "System.Runtime.CompilerServices.InternalsVisibleToAttribute" ||
                    attribute.ConstructorArguments.Count == 0)
                    continue;
                var grant = attribute.ConstructorArguments[0].Value as string;
                if (grant != null && string.Equals(grant.Trim(), consumer, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>The engine binds readable non-hidden instance properties with an assembly-or-public
        /// getter; generated code in the consumer's assembly can only name the public spelling. A
        /// non-public getter is declined (never guessed from another assembly's internals).</summary>
        internal static bool IsCallableFromConsumer(PropertyInfo property)
        {
            if (property == null || !property.CanRead)
                return false;
            var getter = property.GetGetMethod(true);
            if (getter == null || getter.IsStatic)
                return false;
            return getter.IsPublic;
        }

        internal static bool IsObsoleteError(MemberInfo member)
        {
            if (member == null)
                return false;
            var obsolete = Attribute.GetCustomAttribute(member, typeof(ObsoleteAttribute), false)
                as ObsoleteAttribute;
            return obsolete != null && obsolete.IsError;
        }

        /// <summary>Re-resolves the recorded hop against the live declaring type, most-derived first
        /// (the engine's MemberPathWalk order), so an [Obsolete(error: true)] or static spelling is
        /// judged against the property the engine bound.</summary>
        internal static PropertyInfo ResolveHop(Type declaring, string name)
        {
            if (declaring == null || string.IsNullOrEmpty(name))
                return null;
            for (var current = declaring; current != null && current != typeof(object);
                current = current.BaseType)
            {
                foreach (var property in current.GetProperties(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (string.Equals(property.Name, name, StringComparison.Ordinal))
                        return property;
                }
            }

            return null;
        }
    }
}
