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

        private static string CreateEscapedIdentifier(string name)
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