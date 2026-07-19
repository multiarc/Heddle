using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// Discovers declaratively exported host functions (phase 7 D21 / OQ1 resolution): the assembly-level
    /// <c>Heddle.Attributes.ExportFunctionsAttribute</c> on the compilation's own assembly and its referenced
    /// assemblies. Each container is a <c>public static</c> class; every public static method is one function named
    /// <c>MethodInfo.Name.ToLowerInvariant()</c> (phase 6 D24). Generated calls bind <b>directly</b> to the
    /// discovered container (no shim, no runtime registry), and the manifest records the container as AQN sans
    /// version — the exact shape the gauntlet compares against the live registry.
    /// </summary>
    internal sealed class FunctionExportResolver
    {
        internal sealed class ExportEntry
        {
            public ExportEntry(string containerGlobalName, string containerAqnSansVersion,
                IReadOnlyDictionary<string, string> methodNamesByFunction, int overloadCount)
            {
                ContainerGlobalName = containerGlobalName;
                ContainerAqnSansVersion = containerAqnSansVersion;
                MethodNamesByFunction = methodNamesByFunction;
                OverloadCount = overloadCount;
            }

            /// <summary><c>global::</c>-qualified container type name for the generated call site.</summary>
            public string ContainerGlobalName { get; }

            /// <summary>AQN sans version (<c>Ns.Type, Assembly</c>) for the manifest <c>FunctionBindings</c> row.</summary>
            public string ContainerAqnSansVersion { get; }

            /// <summary>The concrete (cased) method name to emit for this function (the C# compiler resolves the
            /// overload); keyed by the lowercase function name.</summary>
            public IReadOnlyDictionary<string, string> MethodNamesByFunction { get; }

            /// <summary>Overload count for the function within this container — the manifest row's per-target count.</summary>
            public int OverloadCount { get; }
        }

        private readonly Dictionary<string, ExportEntry> _byFunctionName;

        private FunctionExportResolver(Dictionary<string, ExportEntry> byFunctionName)
        {
            _byFunctionName = byFunctionName;
        }

        public bool TryGet(string functionName, out ExportEntry entry) =>
            _byFunctionName.TryGetValue(functionName, out entry);

        public bool Any => _byFunctionName.Count != 0;

        public static FunctionExportResolver Build(Compilation compilation)
        {
            var byName = new Dictionary<string, ExportEntry>(System.StringComparer.Ordinal);
            if (compilation == null)
                return new FunctionExportResolver(byName);

            var exportAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ExportFunctionsAttribute");
            if (exportAttr == null)
                return new FunctionExportResolver(byName);

            var assemblies = new List<IAssemblySymbol> { compilation.Assembly };
            assemblies.AddRange(compilation.SourceModule.ReferencedAssemblySymbols);

            foreach (var assembly in assemblies)
            {
                foreach (var attr in assembly.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, exportAttr))
                        continue;

                    foreach (var container in ContainerTypes(attr))
                        AddContainer(container, byName);
                }
            }

            return new FunctionExportResolver(byName);
        }

        private static IEnumerable<INamedTypeSymbol> ContainerTypes(AttributeData attr)
        {
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol single)
                {
                    yield return single;
                }
                else if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var item in arg.Values)
                        if (item.Kind == TypedConstantKind.Type && item.Value is INamedTypeSymbol many)
                            yield return many;
                }
            }
        }

        private static void AddContainer(INamedTypeSymbol container, Dictionary<string, ExportEntry> byName)
        {
            if (container == null || container.DeclaredAccessibility != Accessibility.Public ||
                !container.IsStatic)
                return;

            // function name (lowercase) -> concrete method name; and per-function overload count.
            var methodNames = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var overloadCounts = new Dictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var member in container.GetMembers())
            {
                if (!(member is IMethodSymbol method) || method.MethodKind != MethodKind.Ordinary)
                    continue;
                if (method.DeclaredAccessibility != Accessibility.Public || !method.IsStatic)
                    continue;

                var fnName = method.Name.ToLowerInvariant();
                methodNames[fnName] = method.Name;
                overloadCounts[fnName] = overloadCounts.TryGetValue(fnName, out var c) ? c + 1 : 1;
            }

            if (methodNames.Count == 0)
                return;

            var globalName = container.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var bareName = globalName.StartsWith("global::", System.StringComparison.Ordinal)
                ? globalName.Substring("global::".Length)
                : globalName;
            var aqn = bareName + ", " + container.ContainingAssembly.Identity.Name;

            foreach (var pair in methodNames)
            {
                // Later declarations do not override earlier ones (attribute declaration order, D24) —
                // a function name already bound to a container stays with it.
                if (byName.ContainsKey(pair.Key))
                    continue;
                var single = new Dictionary<string, string>(System.StringComparer.Ordinal) { [pair.Key] = pair.Value };
                byName[pair.Key] = new ExportEntry(globalName, aqn, single, overloadCounts[pair.Key]);
            }
        }
    }
}
