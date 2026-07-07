using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// Build-time custom-extension binder (phase 7 D9 / WI6): scans the compilation's own and referenced assemblies
    /// for <c>[Heddle.Attributes.ExtensionName]</c> types deriving from <c>AbstractExtension</c>, mapping each name to
    /// the concrete type the generated code constructs. Extensions are <b>bound, never inlined</b> (D9) — a logic or
    /// security fix in the extension package reaches precompiled templates by updating the reference, no regeneration.
    /// <para>Two refusals are recorded per D22: a bound extension <b>outside the engine assembly</b> that overrides
    /// the compile-time hooks <c>InitStart</c>/<c>CompleteInit</c> cannot be reproduced by <c>Bind</c> (which
    /// reproduces the <i>base</i> behavior only) → <c>HED7015</c>; and an engine-assembly extension with such an
    /// override that the emitter has no pinned knowledge of stays a safe dynamic fallback (never a mis-emit).</para>
    /// </summary>
    internal sealed class ExtensionBinder
    {
        internal readonly struct Info
        {
            public Info(string globalName, string aqnSansVersion, string assemblyName, bool overridesHook,
                bool isEngineAssembly)
            {
                GlobalName = globalName;
                AqnSansVersion = aqnSansVersion;
                AssemblyName = assemblyName;
                OverridesHook = overridesHook;
                IsEngineAssembly = isEngineAssembly;
            }

            /// <summary><c>global::</c>-qualified type name for the generated <c>new …()</c>.</summary>
            public string GlobalName { get; }

            /// <summary>AQN sans version (<c>Ns.Type, Assembly</c>) for the manifest <c>ExtensionBindings</c> row.</summary>
            public string AqnSansVersion { get; }

            public string AssemblyName { get; }

            /// <summary>The type (or a base below <c>AbstractExtension</c>) overrides <c>InitStart</c>/<c>CompleteInit</c>.</summary>
            public bool OverridesHook { get; }

            public bool IsEngineAssembly { get; }
        }

        private readonly Dictionary<string, Info> _byName;

        private ExtensionBinder(Dictionary<string, Info> byName) => _byName = byName;

        public bool TryResolve(string name, out Info info) => _byName.TryGetValue(name, out info);

        public static ExtensionBinder Build(Compilation compilation)
        {
            var byName = new Dictionary<string, Info>(System.StringComparer.Ordinal);
            if (compilation == null)
                return new ExtensionBinder(byName);

            var nameAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ExtensionNameAttribute");
            var abstractExtension = compilation.GetTypeByMetadataName("Heddle.Core.AbstractExtension");
            if (nameAttr == null || abstractExtension == null)
                return new ExtensionBinder(byName);

            var assemblies = new List<IAssemblySymbol> { compilation.Assembly };
            assemblies.AddRange(compilation.SourceModule.ReferencedAssemblySymbols);

            foreach (var assembly in assemblies)
                CollectTypes(assembly.GlobalNamespace, nameAttr, abstractExtension, byName);

            return new ExtensionBinder(byName);
        }

        private static void CollectTypes(INamespaceSymbol ns, INamedTypeSymbol nameAttr,
            INamedTypeSymbol abstractExtension, Dictionary<string, Info> byName)
        {
            foreach (var type in ns.GetTypeMembers())
                InspectType(type, nameAttr, abstractExtension, byName);
            foreach (var child in ns.GetNamespaceMembers())
                CollectTypes(child, nameAttr, abstractExtension, byName);
        }

        private static void InspectType(INamedTypeSymbol type, INamedTypeSymbol nameAttr,
            INamedTypeSymbol abstractExtension, Dictionary<string, Info> byName)
        {
            if (type.IsAbstract || type.TypeKind != TypeKind.Class || !DerivesFrom(type, abstractExtension))
                return;

            var names = new List<string>();
            foreach (var attr in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, nameAttr))
                    continue;
                if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is string n)
                    names.Add(n);
            }

            if (names.Count == 0)
                return;

            var global = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var bare = global.StartsWith("global::", System.StringComparison.Ordinal)
                ? global.Substring("global::".Length)
                : global;
            var assemblyName = type.ContainingAssembly?.Identity.Name ?? string.Empty;
            var info = new Info(global, bare + ", " + assemblyName, assemblyName,
                OverridesCompileTimeHook(type, abstractExtension),
                string.Equals(assemblyName, "Heddle", System.StringComparison.Ordinal));

            foreach (var name in names)
            {
                if (name.Length == 0)
                    continue; // the unnamed carrier is emitted directly, not through the custom binder.
                // First registration wins (mirrors the engine's LoadExtensions precedence closure enough for the
                // build-time bind target; divergence from a runtime [ExtensionReplace] is what the gauntlet polices).
                if (!byName.ContainsKey(name))
                    byName[name] = info;
            }
        }

        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var t = type.BaseType; t != null; t = t.BaseType)
                if (SymbolEqualityComparer.Default.Equals(t, baseType))
                    return true;
            return false;
        }

        private static bool OverridesCompileTimeHook(INamedTypeSymbol type, INamedTypeSymbol abstractExtension)
        {
            for (var t = type; t != null && !SymbolEqualityComparer.Default.Equals(t, abstractExtension); t = t.BaseType)
            {
                foreach (var member in t.GetMembers())
                {
                    if (member is IMethodSymbol method && method.IsOverride &&
                        (method.Name == "InitStart" || method.Name == "CompleteInit"))
                        return true;
                }
            }

            return false;
        }
    }
}
