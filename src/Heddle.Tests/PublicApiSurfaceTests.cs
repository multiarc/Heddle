using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Heddle.Data;
using Heddle.Language;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The phase 6 D1 binary-compatibility gate: a sorted, normalized reflection dump of the public surface of
    /// <c>Heddle</c> and <c>Heddle.Language</c>, pinned as golden files. The zero-breaking-change proof for the
    /// <c>Heddle.LanguageServices</c> extraction — any public removal or signature change fails this test. The
    /// <c>Heddle</c> golden includes phase 6 D24's two additive members (<c>ExportFunctionsAttribute</c>,
    /// <c>FunctionRegistry.RegisterFrom</c>); <c>Heddle.Language</c> gains nothing.
    /// </summary>
    public class PublicApiSurfaceTests
    {
        [Fact]
        public void HeddlePublicSurfaceMatchesGolden()
        {
            AssertSurface(typeof(HeddleTemplate).Assembly, "public-api-heddle.txt");
        }

        [Fact]
        public void HeddleLanguagePublicSurfaceMatchesGolden()
        {
            AssertSurface(typeof(HeddleParser).Assembly, "public-api-heddle-language.txt");
        }

        private static void AssertSurface(Assembly assembly, string goldenName)
        {
            var actual = DumpPublicSurface(assembly);
            var actualPath = Path.Combine("TestTemplate", goldenName + ".actual");
            File.WriteAllText(actualPath, actual);
#if NET8_0_OR_GREATER
            // The golden is captured on net8.0+ (the facade's own TFMs). ScopeRenderer carries a TFM-conditional
            // member (TotalLength on net8+, TotalCount on net6/netstandard), a pre-existing per-TFM surface
            // difference — so the byte-exact snapshot is pinned on net8.0+ and older TFMs get the lighter check.
            var goldenPath = Path.Combine("TestTemplate", goldenName);
            Assert.True(File.Exists(goldenPath),
                $"Public-surface golden '{goldenName}' is missing; the current surface was written to '{actualPath}'.");
            var golden = File.ReadAllText(goldenPath).Replace("\r\n", "\n");
            Assert.Equal(golden, actual.Replace("\r\n", "\n"));
#else
            Assert.False(string.IsNullOrWhiteSpace(actual));
#endif
        }

        /// <summary>Produces a deterministic, sorted line-per-member dump of every exported type in the assembly.</summary>
        internal static string DumpPublicSurface(Assembly assembly)
        {
            var lines = new List<string>();
            foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                var header = new StringBuilder();
                header.Append("TYPE ").Append(Friendly(type));
                if (type.BaseType != null && type.BaseType != typeof(object))
                    header.Append(" : ").Append(Friendly(type.BaseType));
                // Only Heddle's own interfaces are part of the surface under test; BCL interface implementations
                // (e.g. enums gaining System.ISpanFormattable across runtimes) are runtime contracts that vary by
                // TFM and would make the snapshot non-deterministic.
                var interfaces = type.GetInterfaces()
                    .Where(i => (i.IsPublic || i.IsNestedPublic) && (i.FullName?.StartsWith("Heddle") ?? false))
                    .Select(Friendly).OrderBy(s => s, StringComparer.Ordinal).ToList();
                if (interfaces.Count > 0)
                    header.Append(" impl ").Append(string.Join(", ", interfaces));
                lines.Add(header.ToString());

                var members = new List<string>();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                           BindingFlags.DeclaredOnly;
                foreach (var member in type.GetMembers(flags))
                {
                    switch (member)
                    {
                        case ConstructorInfo ctor:
                            members.Add($"  CTOR({Parameters(ctor.GetParameters())})");
                            break;
                        case MethodInfo method when !method.IsSpecialName:
                            members.Add(
                                $"  METHOD {Friendly(method.ReturnType)} {method.Name}({Parameters(method.GetParameters())})");
                            break;
                        case PropertyInfo property:
                            var accessors = new StringBuilder();
                            if (property.GetGetMethod() != null) accessors.Append("get;");
                            if (property.GetSetMethod() != null) accessors.Append("set;");
                            members.Add($"  PROP {Friendly(property.PropertyType)} {property.Name} {{{accessors}}}");
                            break;
                        case FieldInfo field:
                            members.Add($"  FIELD {Friendly(field.FieldType)} {field.Name}");
                            break;
                        case EventInfo evt:
                            members.Add($"  EVENT {Friendly(evt.EventHandlerType)} {evt.Name}");
                            break;
                    }
                }

                members.Sort(StringComparer.Ordinal);
                lines.AddRange(members);
            }

            return string.Join("\n", lines) + "\n";
        }

        private static string Parameters(ParameterInfo[] parameters)
        {
            return string.Join(", ", parameters.Select(p =>
            {
                var prefix = p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : string.Empty;
                return prefix + Friendly(p.ParameterType);
            }));
        }

        private static string Friendly(Type type)
        {
            if (type == null)
                return "void";
            if (type.IsByRef)
                type = type.GetElementType();
            if (type.IsArray)
                return Friendly(type.GetElementType()) + "[]";
            if (!type.IsGenericType)
                return type.FullName ?? type.Name;
            var name = type.GetGenericTypeDefinition().FullName;
            var tick = name.IndexOf('`');
            if (tick >= 0)
                name = name.Substring(0, tick);
            var args = type.GetGenericArguments().Select(Friendly);
            return $"{name}<{string.Join(", ", args)}>";
        }
    }
}
