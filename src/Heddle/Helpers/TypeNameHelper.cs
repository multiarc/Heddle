using System;
using System.Reflection;
using Heddle.Strings;

namespace Heddle.Helpers
{
    internal static class TypeNameHelper
    {
        private static readonly string[][] Keywords = new string[][] {
            null,           // 1 character 
            new string[] {  // 2 characters
                "as",
                "do",
                "if",
                "in",
                "is",
            },
            new string[] {  // 3 characters
                "for",
                "int",
                "new",
                "out",
                "ref",
                "try",
            },
            new string[] {  // 4 characters
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
            new string[] {  // 5 characters
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
            new string[] {  // 6 characters
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
            new string[] {  // 7 characters 
                "checked",
                "decimal",
                "default",
                "finally",
                "foreach",
                "private",
                "virtual",
            },
            new string[] {  // 8 characters 
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
            new string[] {  // 9 characters
                "__arglist",
                "__makeref",
                "__reftype",
                "interface",
                "namespace",
                "protected",
                "unchecked",
            },
            new string[] {  // 10 characters
                "__refvalue",
                "stackalloc",
            },
        };

        /// <summary>The reflection-free C# spelling of a type's own name (nested types joined with <c>.</c>,
        /// generic arity expanded, identifiers verbatim-escaped where they collide with a keyword).
        /// <para>Phase 6 D7 note: this method used to open with a <c>switch</c> mapping <c>"system.int32"</c> and
        /// friends to their C# keywords — the repo's fifth alias table. It was unreachable: the subject is
        /// <see cref="Type.Name"/>, which is never namespace-qualified, so <c>typeof(int)</c> always fell to the
        /// default branch and spelled itself <c>Int32</c>. The dead branch is gone rather than repaired: this
        /// spelling feeds <c>ExType.ToString()</c> across error text and generated code, so turning it into a
        /// keyword mapping is a behavior change for a different owner to make deliberately. The live alias table
        /// is <see cref="CSharpTypeNames"/>.</para>
        /// <para><b>Phase 3 ruling (the owner deciding, as phase 6 asked):</b> the removal stands — this method is
        /// <b>not</b> becoming a keyword mapping. Three reasons. (1) The display direction already has exactly one
        /// owner, <c>CSharpTypeNames.TryGetDisplayName</c>; reinstating a mapping here would recreate the fifth
        /// alias table the program exists to remove. (2) The rest of this spelling is namespace-qualified
        /// (<c>Ns.Type&lt;Arg&gt;</c>), so aliasing only the fifteen primitives would make one string internally
        /// inconsistent. (3) <c>ExType.ToString()</c> is compared as a <i>string</i> on at least one compiler path
        /// and quoted in pinned error text, and phase 3's own build-tier diagnostic spelling was deliberately
        /// aligned <i>to</i> the non-aliased form (<c>SymbolTypeFacts.Display</c>) so the two tiers' shared
        /// messages agree — aliasing here would re-open that gap for no functional gain. A caller that wants
        /// keyword display should ask <see cref="CSharpTypeNames"/> for it.</para></summary>
        public static string GetBaseTypeOutput(Type typeRef)
        {
            if (typeRef.Name.Length == 0)
                return "void";

            // replace + with . for nested classes.
            //
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

                        // Arity can be in the middle of a nested type name, so we might have a . or + after it. 
                        // Skip it if so. 
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
            // Any identifier started with two consecutive underscores are 
            // reserved by CSharp.
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

                // it's possible that we call GetTypeArgumentsOutput with an empty typeArguments collection.  This is the case
                // for open types, so we want to just output the brackets and commas. 
                if (i < typeArguments.Length)
                    sb.Append(typeArguments[i].GetTypeOutput());
            }
            sb.Append(">");
        }
    }
}