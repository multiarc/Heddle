using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Emit
{
    /// <summary>Mirror of <c>Heddle.Attributes.BranchRole</c>. The generator cannot reference the runtime
    /// <c>Heddle</c> assembly, so the values are pinned here and decoded from the attribute's constructor
    /// argument (read out of Roslyn symbol metadata as a raw <see cref="int"/>); renumbering is a
    /// cross-assembly breaking change.</summary>
    internal enum BranchRole { Opener = 0, Continuation = 1, Terminal = 2 }

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
        /// <summary>Phase 8 (WI5): one decoded <c>[Prop]</c> declaration of a parameter-declaring extension,
        /// read over the base-type chain (outermost base first — <c>[Prop]</c> is <c>Inherited = true</c> and
        /// Roslyn does not surface inherited attributes). The emitter's <c>ResolveExtensionPropLayout</c> builds
        /// its parallel layout (and detects every malformed condition → HED7017) from these.</summary>
        internal sealed class PropParameter
        {
            /// <summary>Declared parameter name (constructor argument 0); may be <c>null</c> when the argument
            /// was <c>null</c> — a malformed declaration <c>ResolveExtensionPropLayout</c> turns into HED7017.</summary>
            public string Name;

            /// <summary>The declared parameter type symbol (<c>typeof</c> constructor argument 1); may be
            /// <c>null</c> when the argument was <c>null</c> — see <see cref="TypeUnusable"/>.</summary>
            public ITypeSymbol Type;

            /// <summary>True when the type argument is unusable (null / unbound generic / pointer) — HED7017's
            /// twin of the dynamic tier's HED5010.</summary>
            public bool TypeUnusable;

            /// <summary>Derived optionality (D3): <c>Default != null || Optional</c>.</summary>
            public bool HasDefault;

            /// <summary>The decoded <c>Default</c> named-argument value (boxed primitive/string/enum-underlying;
            /// an <see cref="ITypeSymbol"/> for a <c>typeof</c> default; <c>null</c> when absent).</summary>
            public object DefaultValue;

            /// <summary>The <c>Default</c> argument's own type symbol (the HED5009-twin conversion source);
            /// <c>null</c> when no default value was given.</summary>
            public ITypeSymbol DefaultType;

            /// <summary><c>Optional = true</c> named argument (D3's null-default marker).</summary>
            public bool Optional;

            /// <summary>Base-chain layer index, outermost-first — a repeated name at a HIGHER level is an
            /// inherited re-declaration; at the SAME level a duplicate declaration.</summary>
            public int Level;
        }

        private static readonly IReadOnlyList<PropParameter> EmptyParameters = new PropParameter[0];

        internal readonly struct Info
        {
            public Info(string globalName, string aqnSansVersion, string assemblyName, bool overridesHook,
                bool isEngineAssembly, BranchRole? role, bool hasScopeChannel,
                bool hasEncodeOutput = false, bool hasNotEncode = false,
                IReadOnlyList<PropParameter> parameters = null)
            {
                GlobalName = globalName;
                AqnSansVersion = aqnSansVersion;
                AssemblyName = assemblyName;
                OverridesHook = overridesHook;
                IsEngineAssembly = isEngineAssembly;
                Role = role;
                HasScopeChannel = hasScopeChannel;
                HasEncodeOutput = hasEncodeOutput;
                HasNotEncode = hasNotEncode;
                Parameters = parameters ?? EmptyParameters;
            }

            /// <summary><c>global::</c>-qualified type name for the generated <c>new …()</c>.</summary>
            public string GlobalName { get; }

            /// <summary>AQN sans version (<c>Ns.Type, Assembly</c>) for the manifest <c>ExtensionBindings</c> row.</summary>
            public string AqnSansVersion { get; }

            public string AssemblyName { get; }

            /// <summary>The type (or a base below <c>AbstractExtension</c>) overrides <c>InitStart</c>/<c>CompleteInit</c>.</summary>
            public bool OverridesHook { get; }

            public bool IsEngineAssembly { get; }

            /// <summary>Branch-set role, when the type (or a base) carries <c>[BranchRole]</c>.</summary>
            public BranchRole? Role { get; }

            /// <summary>The type (or a base) carries <c>[ScopeChannel]</c>.</summary>
            public bool HasScopeChannel { get; }

            /// <summary>Phase 8 (WI5/WI5b): the type (or a base) carries <c>[EncodeOutput]</c> — the symbolic
            /// mirror of <c>InitializeTemplate</c>'s <c>IsHaveAttribute&lt;EncodeOutputAttribute&gt;(true)</c>.
            /// Feeds the derived render type of both the parameterized and the plain custom bind.</summary>
            public bool HasEncodeOutput { get; }

            /// <summary>Phase 8 (WI5/WI5b): the type (or a base) carries <c>[NotEncode]</c>. Structurally dead
            /// (the attribute targets properties only) — retained to mirror the dynamic tier's expression
            /// verbatim; both dead branches agree.</summary>
            public bool HasNotEncode { get; }

            /// <summary>Phase 8 (WI5): the decoded <c>[Prop]</c> declarations, base-chain outermost-first;
            /// empty for a parameter-less extension (or against an older engine reference).</summary>
            public IReadOnlyList<PropParameter> Parameters { get; }

            /// <summary>Reads the channel — hosting bodies must provision a locals frame.</summary>
            public bool IsBranchParticipant =>
                Role == BranchRole.Continuation || Role == BranchRole.Terminal;
        }

        private readonly Dictionary<string, Info> _byName;
        private readonly List<string> _driftTypes;

        private ExtensionBinder(Dictionary<string, Info> byName, List<string> driftTypes)
        {
            _byName = byName;
            _driftTypes = driftTypes;
        }

        public bool TryResolve(string name, out Info info) => _byName.TryGetValue(name, out info);

        /// <summary>D-ROLE-5 drift (§6.5): display names of extension types classified as
        /// <see cref="BranchRole.Continuation"/>/<see cref="BranchRole.Terminal"/> that do <b>not</b> carry
        /// <c>[ScopeChannel]</c> — they cannot read the branch state at render time (HED7016).</summary>
        public IReadOnlyList<string> DriftTypes => _driftTypes;

        public static ExtensionBinder Build(Compilation compilation)
        {
            var byName = new Dictionary<string, Info>(System.StringComparer.Ordinal);
            var driftTypes = new List<string>();
            if (compilation == null)
                return new ExtensionBinder(byName, driftTypes);

            var nameAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ExtensionNameAttribute");
            var abstractExtension = compilation.GetTypeByMetadataName("Heddle.Core.AbstractExtension");
            if (nameAttr == null || abstractExtension == null)
                return new ExtensionBinder(byName, driftTypes);

            // All of these may be null against an older engine reference that predates the attribute — then
            // every Info.Role is null, HasScopeChannel/HasEncodeOutput/HasNotEncode false, and Parameters empty;
            // the reads below degrade safely.
            var symbols = new AttrSymbols
            {
                NameAttr = nameAttr,
                AbstractExtension = abstractExtension,
                RoleAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.BranchRoleAttribute"),
                ScopeChannelAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ScopeChannelAttribute"),
                EncodeOutputAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.EncodeOutputAttribute"),
                NotEncodeAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.NotEncodeAttribute"),
                PropAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.PropAttribute")
            };

            var assemblies = new List<IAssemblySymbol> { compilation.Assembly };
            assemblies.AddRange(compilation.SourceModule.ReferencedAssemblySymbols);

            foreach (var assembly in assemblies)
                CollectTypes(assembly.GlobalNamespace, symbols, byName, driftTypes);

            return new ExtensionBinder(byName, driftTypes);
        }

        private sealed class AttrSymbols
        {
            public INamedTypeSymbol NameAttr;
            public INamedTypeSymbol AbstractExtension;
            public INamedTypeSymbol RoleAttr;
            public INamedTypeSymbol ScopeChannelAttr;
            public INamedTypeSymbol EncodeOutputAttr;
            public INamedTypeSymbol NotEncodeAttr;
            public INamedTypeSymbol PropAttr;
        }

        private static void CollectTypes(INamespaceSymbol ns, AttrSymbols symbols,
            Dictionary<string, Info> byName, List<string> driftTypes)
        {
            foreach (var type in ns.GetTypeMembers())
                InspectType(type, symbols, byName, driftTypes);
            foreach (var child in ns.GetNamespaceMembers())
                CollectTypes(child, symbols, byName, driftTypes);
        }

        private static void InspectType(INamedTypeSymbol type, AttrSymbols symbols,
            Dictionary<string, Info> byName, List<string> driftTypes)
        {
            var nameAttr = symbols.NameAttr;
            var abstractExtension = symbols.AbstractExtension;
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
                string.Equals(assemblyName, "Heddle", System.StringComparison.Ordinal),
                ReadBranchRole(type, symbols.RoleAttr),
                HasAttribute(type, symbols.ScopeChannelAttr),
                HasAttribute(type, symbols.EncodeOutputAttr),
                HasAttribute(type, symbols.NotEncodeAttr),
                ReadPropParameters(type, symbols.PropAttr));

            // D-ROLE-5 drift (§6.5): a Continuation/Terminal that cannot read the channel it depends on. Additive;
            // no built-in violates R11, so this is empty for engine-only compilations.
            if (info.IsBranchParticipant && !info.HasScopeChannel)
                driftTypes.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

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

        /// <summary>Reads <c>[BranchRole]</c> walking the base-type chain — Roslyn's <c>GetAttributes()</c> does
        /// not surface inherited attributes, and the attribute is <c>Inherited = true</c>. An out-of-range
        /// constructor value (a future enum member from a newer engine) yields <c>null</c> — the safe degrade.</summary>
        private static BranchRole? ReadBranchRole(INamedTypeSymbol type, INamedTypeSymbol roleAttr)
        {
            if (roleAttr == null)
                return null;

            for (var t = type; t != null; t = t.BaseType)
                foreach (var attr in t.GetAttributes())
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, roleAttr) &&
                        attr.ConstructorArguments.Length == 1 &&
                        attr.ConstructorArguments[0].Value is int v && v >= 0 && v <= 2)
                        return (BranchRole)v;

            return null;
        }

        /// <summary>Presence-only base-type-chain walk for an <c>Inherited = true</c> attribute (e.g.
        /// <c>[ScopeChannel]</c>); guarded when the attribute symbol is unresolvable (older engine).</summary>
        private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attrType)
        {
            if (attrType == null)
                return false;

            for (var t = type; t != null; t = t.BaseType)
                foreach (var attr in t.GetAttributes())
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType))
                        return true;

            return false;
        }

        /// <summary>Phase 8 (WI5): decodes the extension's <c>[Prop]</c> declarations over the base-type chain,
        /// outermost base first ([Prop] is <c>Inherited = true</c>; Roslyn surfaces no inherited attributes) —
        /// the same layer order <c>PropLayout.ResolveFromExtension</c> walks on the dynamic tier. Degrades to an
        /// empty list when the attribute symbol is unresolvable (older engine reference).</summary>
        private static IReadOnlyList<PropParameter> ReadPropParameters(INamedTypeSymbol type,
            INamedTypeSymbol propAttr)
        {
            if (propAttr == null)
                return EmptyParameters;

            var layers = new List<INamedTypeSymbol>();
            for (var t = type; t != null; t = t.BaseType)
                layers.Add(t);
            layers.Reverse();

            List<PropParameter> result = null;
            for (int level = 0; level < layers.Count; level++)
            {
                foreach (var attr in layers[level].GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, propAttr))
                        continue;
                    if (attr.ConstructorArguments.Length != 2)
                        continue;
                    // A null name is ADMITTED (not skipped): dropping it would silently treat the extension as
                    // parameter-less while the dynamic tier rejects the declaration — ResolveExtensionPropLayout
                    // diagnoses it as HED7017 (the dynamic tier's HED5015 name-validity twin).
                    var name = attr.ConstructorArguments[0].Value as string;

                    var typeSymbol = attr.ConstructorArguments[1].Value as ITypeSymbol;
                    bool unusable = typeSymbol == null ||
                                    typeSymbol.TypeKind == TypeKind.Pointer ||
                                    (typeSymbol is INamedTypeSymbol named && named.IsUnboundGenericType) ||
                                    typeSymbol is IErrorTypeSymbol;

                    var parameter = new PropParameter
                    {
                        Name = name,
                        Type = typeSymbol,
                        TypeUnusable = unusable,
                        Level = level
                    };

                    foreach (var namedArg in attr.NamedArguments)
                    {
                        if (namedArg.Key == "Default" && !namedArg.Value.IsNull)
                        {
                            // A typeof default decodes as an ITypeSymbol value; primitives/strings/enums as their
                            // boxed CLR value. DefaultType is the argument's own type — the HED5009-twin source.
                            parameter.DefaultValue = namedArg.Value.Value;
                            parameter.DefaultType = namedArg.Value.Type;
                        }
                        else if (namedArg.Key == "Optional" && namedArg.Value.Value is bool opt)
                        {
                            parameter.Optional = opt;
                        }
                    }

                    parameter.HasDefault = parameter.DefaultValue != null || parameter.Optional;
                    (result ?? (result = new List<PropParameter>())).Add(parameter);
                }
            }

            return result ?? EmptyParameters;
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
