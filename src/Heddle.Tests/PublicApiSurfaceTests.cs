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
    /// A binary-compatibility gate: reflects the public API surface and pins it against golden files.
    /// Detects any public removals or signature changes.
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
            // ScopeRenderer has TFM-conditional members, so golden is pinned to net8.0+ with lighter check on older TFMs.
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
                // Exclude BCL interfaces; they vary by TFM and break determinism.
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
