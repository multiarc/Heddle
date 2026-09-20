using System;
using System.Reflection;
using Heddle.Strings;

namespace Heddle.Helpers
{
    internal static class TypeNameHelper
    {
        private static readonly string[][] Keywords = new string[][] {
            null,
            new string[] {
                "as",
                "do",
                "if",
                "in",
                "is",
            },
            new string[] {
                "for",
                "int",
                "new",
                "out",
                "ref",
                "try",
            },
            new string[] {
                "base",
                "bool",
                "byte",
                "case",
                "char",
                "else",
                "enum",
                "goto",
                "lock",
                "long",
                "null",
                "this",
                "true",
                "uint",
                "void",
            },
            new string[] {
                "break",
                "catch",
                "class",
                "const",
                "event",
                "false",
                "fixed",
                "float",
                "sbyte",
                "short",
                "throw",
                "ulong",
                "using",
                "while",
            },
            new string[] {
                "double",
                "extern",
                "object",
                "params",
                "public",
                "return",
                "sealed",
                "sizeof",
                "static",
                "string",
                "struct",
                "switch",
                "typeof",
                "unsafe",
                "ushort",
            },
            new string[] {
                "checked",
                "decimal",
                "default",
                "finally",
                "foreach",
                "private",
                "virtual",
            },
            new string[] {
                "abstract",
                "continue",
                "delegate",
                "explicit",
                "implicit",
                "internal",
                "operator",
                "override",
                "readonly",
                "volatile",
            },
            new string[] {
                "__arglist",
                "__makeref",
                "__reftype",
                "interface",
                "namespace",
                "protected",
                "unchecked",
            },
            new string[] {
                "__refvalue",
                "stackalloc",
            },
        };

        /// <summary>
        /// The reflection-free C# spelling of a type's name: nested types joined with <c>.</c>, generic arity
        /// expanded, identifiers escaped where they collide with keywords. Does not use keyword aliases—that
        /// responsibility is <see cref="CSharpTypeNames"/>'s alone. Used in error and generated code which is
        /// string-compared, so it must stay consistent.
        /// </summary>
        public static string GetBaseTypeOutput(Type typeRef)
        {
            if (typeRef.Name.Length == 0)
                return "void";

            ExStringBuilder sb = new ExStringBuilder();

            string baseType = typeRef.Name;

            int lastIndex = 0;
            int currentTypeArgStart = 0;
            for (int i = 0; i < baseType.Length; i++)
            {
                switch (baseType[i])
                {
                    case '+':
                    case '.':
                        sb.Append((string) CreateEscapedIdentifier(baseType.Substring(lastIndex, i - lastIndex)));
                        sb.Append(".");
                        i++;
                        lastIndex = i;
                        break;

                    case '`':
                        sb.Append((string) CreateEscapedIdentifier(baseType.Substring(lastIndex, i - lastIndex)));
                        i++;    // skip the '
                        int numTypeArgs = 0;
                        while (i < baseType.Length && baseType[i] >= '0' && baseType[i] <= '9')
                        {
                            numTypeArgs = numTypeArgs * 10 + (baseType[i] - '0');
                            i++;
                        }

                        GetTypeArgumentsOutput(typeRef.GetTypeInfo().GenericTypeArguments, currentTypeArgStart, numTypeArgs, sb);
                        currentTypeArgStart += numTypeArgs;

                        // Arity can appear mid-nested-type-name followed by . or +, so skip it.
                        if (i < baseType.Length && (baseType[i] == '+' || baseType[i] == '.'))
                        {
                            sb.Append(".");
                            i++;
                        }

                        lastIndex = i;
                        break;
                }
            }

            if (lastIndex < baseType.Length)
                sb.Append((string) CreateEscapedIdentifier(baseType.Substring(lastIndex)));

            return sb.ToString();
        }

        /// <summary>A dotted name — a namespace — with each part that C# reserves escaped.</summary>
        internal static string EscapeDottedName(string dotted)
        {
            if (string.IsNullOrEmpty(dotted))
                return dotted;
            var parts = dotted.Split('.');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Length == 0 ? parts[i] : CreateEscapedIdentifier(parts[i].Trim());
            return string.Join(".", parts);
        }

        /// <summary>The one escaping of an identifier every writer of C# source here goes through.</summary>
        internal static string CreateEscapedIdentifier(string name)
        {
            // Identifiers starting with two underscores are reserved by C#.
            if (IsKeyword(name) || IsPrefixTwoUnderscore(name))
            {
                return "@" + name;
            }
            return name;
        }

        private static bool IsPrefixTwoUnderscore(string value)
        {
            if (value.Length < 3)
            {
                return false;
            }
            return ((value[0] == '_') && (value[1] == '_') && (value[2] != '_'));
        }

        private static bool IsKeyword(string value)
        {
            return FixedStringLookup.Contains(Keywords, value, false);
        }

        /// <summary>
        /// The spelling a C# compiler resolves from anywhere: <c>global::</c>-rooted, every enclosing type named,
        /// each level's generic arguments spelled the same way, array ranks outermost first. Unlike
        /// <see cref="GetBaseTypeOutput"/> — the display name, which messages and tests compare as text — this
        /// one leans on no <c>using</c>: a nested type's simple name resolves through no namespace import, and
        /// a generic argument's namespace may be imported nowhere.
        /// </summary>
        public static string GetCSharpReference(Type type)
        {
            if (type.IsArray)
            {
                var ranks = new System.Text.StringBuilder();
                var innermost = type;
                while (innermost.IsArray)
                {
                    ranks.Append('[').Append(',', innermost.GetArrayRank() - 1).Append(']');
                    innermost = innermost.GetElementType();
                }

                return GetCSharpReference(innermost) + ranks;
            }

            if (type.IsGenericParameter)
                return CreateEscapedIdentifier(type.Name);
            if (type == typeof(void))
                return "void";

            var chain = new System.Collections.Generic.List<Type>();
            for (var current = type; current != null; current = current.DeclaringType)
                chain.Insert(0, current);
            var arguments = type.GetTypeInfo().IsGenericType ? type.GetTypeInfo().GenericTypeArguments : new Type[0];
            var sb = new System.Text.StringBuilder("global::");
            if (!string.IsNullOrEmpty(type.Namespace))
                sb.Append(EscapeDottedName(type.Namespace)).Append('.');

            int consumed = 0;
            for (int i = 0; i < chain.Count; i++)
            {
                string name = chain[i].Name;
                int tick = name.IndexOf('`');
                if (i > 0)
                    sb.Append('.');
                sb.Append(CreateEscapedIdentifier(tick < 0 ? name : name.Substring(0, tick)));
                // A nested type carries the arguments of every enclosing type as one flat list; each level
                // takes the slice its own arity adds.
                int upTo = Math.Min(chain[i].GetTypeInfo().IsGenericType
                    ? chain[i].GetTypeInfo().GenericTypeParameters.Length + chain[i].GetTypeInfo().GenericTypeArguments.Length
                    : consumed, arguments.Length);
                if (upTo > consumed)
                {
                    sb.Append('<');
                    for (int a = consumed; a < upTo; a++)
                        sb.Append(a > consumed ? ", " : string.Empty).Append(GetCSharpReference(arguments[a]));
                    sb.Append('>');
                    consumed = upTo;
                }
            }

            return sb.ToString();
        }

        private static void GetTypeArgumentsOutput(Type[] typeArguments, int start, int length, ExStringBuilder sb)
        {
            sb.Append("<");
            bool first = true;
            for (int i = start; i < start + length; i++)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(", ");
                }

                // For open types, typeArguments may be empty; output brackets and commas regardless.
                if (i < typeArguments.Length)
                    sb.Append(typeArguments[i].GetTypeOutput());
            }
            sb.Append(">");
        }
    }
}