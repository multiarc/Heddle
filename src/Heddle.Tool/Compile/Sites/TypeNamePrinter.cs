using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Data;

namespace Heddle.Tool.Compile.Sites
{
    /// <summary>C# spellings for the types the printer must name from the consumer's assembly, plus
    /// the nameability verdicts from P3-R2: a non-public type, an internal member of another
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
                if (!TrySpell(underlying, out inner, out innerWhy))
                {
                    why = innerWhy;
                    return false;
                }

                spelling = inner + "?";
                return true;
            }

            if (type.IsArray)
            {
                string element;
                string elementWhy;
                if (!TrySpell(type.GetElementType(), out element, out elementWhy))
                {
                    why = elementWhy;
                    return false;
                }

                int rank = type.GetArrayRank();
                spelling = element + "[" + new string(',', rank - 1) + "]";
                return true;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                var arguments = type.GetGenericArguments();
                var spelled = new string[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                {
                    string argument;
                    string argumentWhy;
                    if (!TrySpell(arguments[i], out argument, out argumentWhy))
                    {
                        why = argumentWhy;
                        return false;
                    }

                    spelled[i] = argument;
                }

                string definition;
                string definitionWhy;
                if (!TrySpellDefinition(type.GetGenericTypeDefinition(), out definition, out definitionWhy))
                {
                    why = definitionWhy;
                    return false;
                }

                spelling = definition + "<" + string.Join(", ", spelled) + ">";
                return true;
            }

            if (type.IsGenericType)
            {
                why = "open generic type '" + type.Name + "'";
                return false;
            }

            return TrySpellDefinition(type, out spelling, out why);
        }

        private static bool TrySpellDefinition(Type type, out string spelling, out string why)
        {
            spelling = null;
            why = null;
            string name = type.Name;
            if (string.IsNullOrEmpty(name) || name.IndexOf('<') >= 0 || name.IndexOf('>') >= 0)
            {
                why = "type with no C# spelling";
                return false;
            }

            if (!IsNameable(type))
            {
                why = "non-public type '" + (type.FullName ?? type.Name) + "'";
                return false;
            }

            var parts = new List<string>();
            for (var current = type; current != null; current = current.DeclaringType)
                parts.Add(current.Name);
            parts.Reverse();
            string dotted = string.Join(".", parts.ToArray());
            string ns = type.Namespace;
            spelling = "global::" + (string.IsNullOrEmpty(ns) ? dotted : ns + "." + dotted);
            return true;
        }

        private static bool IsNameable(Type type)
        {
            for (var current = type; current != null; current = current.DeclaringType)
            {
                if (current.IsNested)
                {
                    if (!current.IsNestedPublic)
                        return false;
                }
                else if (!current.IsPublic)
                {
                    return false;
                }
            }

            return true;
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
