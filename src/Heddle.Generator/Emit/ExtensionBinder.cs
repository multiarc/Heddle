using System.Collections.Generic;
using Heddle.Attributes;
using Heddle.Generator.Binding;
using Heddle.Language.Binding;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// Build-time custom-extension binder: scans the compilation's own and referenced assemblies for
    /// <c>[Heddle.Attributes.ExtensionName]</c> types deriving from <c>AbstractExtension</c>, mapping each name to
    /// the concrete type the generated code constructs. Extensions are <b>bound, never inlined</b> — a logic or
    /// security fix in the extension package reaches precompiled templates by updating the reference, no regeneration.
    /// <para>Two refusals are recorded: a bound extension <b>outside the engine assembly</b> that overrides
    /// the compile-time hooks <c>InitStart</c>/<c>CompleteInit</c> cannot be reproduced by <c>Bind</c> (which
    /// reproduces the <i>base</i> behavior only) → <c>HED7015</c>; and an engine-assembly extension with such an
    /// override that the emitter has no pinned knowledge of stays a safe dynamic fallback (never a mis-emit).</para>
    /// </summary>
    internal sealed class ExtensionBinder
    {
        /// <summary>One decoded <c>[Prop]</c> declaration of a parameter-declaring extension,
        /// read over the base-type chain (outermost base first — <c>[Prop]</c> is <c>Inherited = true</c> and
        /// Roslyn does not surface inherited attributes). The emitter's <c>ResolveExtensionPropLayout</c> builds
        /// its parallel layout (and detects every malformed condition → HED7017) from these.</summary>
        internal sealed class PropParameter
        {
            /// <summary>Declared parameter name (constructor argument 0); may be <c>null</c> when the argument
            /// was <c>null</c> — a malformed declaration <c>ResolveExtensionPropLayout</c> turns into HED7017.</summary>
            public string Name;

            /// <summary>The declared parameter type symbol (<c>typeof</c> constructor argument 1); may be
            /// <c>null</c> when the argument was <c>null</c>, which the shared prop-layout core reports as
            /// <see cref="Heddle.Data.PropFault.TypeUnusable"/>.</summary>
            public ITypeSymbol Type;

            /// <summary>Derived optionality: <c>Default != null || Optional</c>.</summary>
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
        private static readonly IReadOnlyList<ITypeSymbol> EmptyDataTypes = new ITypeSymbol[0];

        internal readonly struct Info
        {
            public Info(string globalName, string bareTypeName, string aqnSansVersion, string assemblyName,
                bool overridesHook,
                bool isEngineAssembly, BranchRole? role, bool hasScopeChannel,
                bool hasEncodeOutput = false, bool hasNotEncode = false, bool isZeroOutput = false,
                IReadOnlyList<PropParameter> parameters = null, INamedTypeSymbol typeSymbol = null,
                IReadOnlyList<ITypeSymbol> acceptedDataTypes = null)
            {
                TypeSymbol = typeSymbol;
                AcceptedDataTypes = acceptedDataTypes ?? EmptyDataTypes;
                GlobalName = globalName;
                BareTypeName = bareTypeName;
                AqnSansVersion = aqnSansVersion;
                AssemblyName = assemblyName;
                OverridesHook = overridesHook;
                IsEngineAssembly = isEngineAssembly;
                Role = role;
                HasScopeChannel = hasScopeChannel;
                HasEncodeOutput = hasEncodeOutput;
                HasNotEncode = hasNotEncode;
                IsZeroOutput = isZeroOutput;
                Parameters = parameters ?? EmptyParameters;
            }

            /// <summary>The bound extension's type symbol — the assignability edge the shared registration
            /// precedence rule needs when a later candidate claims the same name.</summary>
            public INamedTypeSymbol TypeSymbol { get; }

            /// <summary>The types the extension declares it accepts as its call value — every <c>[DataType]</c> over
            /// the base chain, which is what <c>HeddleCompiler</c> reads with <c>inherit: true</c> and checks the
            /// call value against before it compiles the call at all. Empty means the extension accepts anything.
            /// </summary>
            public IReadOnlyList<ITypeSymbol> AcceptedDataTypes { get; }

            /// <summary><c>global::</c>-qualified type name for the generated <c>new …()</c>.</summary>
            public string GlobalName { get; }

            /// <summary>The CLR full type name (<c>Ns.Outer+Inner</c>) from the shared
            /// <see cref="Heddle.Precompiled.AqnFormatter"/> — the manifest binding row's type half. It is
            /// <b>not</b> the <c>global::</c>-stripped display string, which spells a nested type with a dot and a
            /// generic container with angle brackets, neither of which reflection ever produces.</summary>
            public string BareTypeName { get; }

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

            /// <summary>The type (or a base) carries <c>[EncodeOutput]</c> — the symbolic
            /// mirror of <c>InitializeTemplate</c>'s <c>IsHaveAttribute&lt;EncodeOutputAttribute&gt;(true)</c>.
            /// Feeds the derived render type of both the parameterized and the plain custom bind.</summary>
            public bool HasEncodeOutput { get; }

            /// <summary>The type (or a base) carries <c>[NotEncode]</c>. Structurally dead
            /// (the attribute targets properties only) — retained to mirror the dynamic tier's expression
            /// verbatim; both dead branches agree.</summary>
            public bool HasNotEncode { get; }

            /// <summary>The type (or a base) carries <c>[ZeroOutput]</c> — it emits nothing
            /// and its block is removed from the piece stream, the declarative form of the runtime's
            /// null-<c>InitStart</c> protocol. False against an older engine reference that predates the
            /// attribute, which keeps the emitter on its built-in name list.</summary>
            public bool IsZeroOutput { get; }

            /// <summary>The decoded <c>[Prop]</c> declarations, base-chain outermost-first;
            /// empty for a parameter-less extension (or against an older engine reference).</summary>
            public IReadOnlyList<PropParameter> Parameters { get; }

            /// <summary>Reads the channel — hosting bodies must provision a locals frame.</summary>
            public bool IsBranchParticipant =>
                Role == BranchRole.Continuation || Role == BranchRole.Terminal;
        }

        private readonly Dictionary<string, Info> _byName;
        private readonly Dictionary<string, string> _unbindable;
        private readonly List<string> _driftTypes;

        private ExtensionBinder(Dictionary<string, Info> byName, Dictionary<string, string> unbindable,
            List<string> driftTypes)
        {
            _byName = byName;
            _unbindable = unbindable;
            _driftTypes = driftTypes;
        }

        public bool TryResolve(string name, out Info info) => _byName.TryGetValue(name, out info);

        /// <summary>The name resolves to a type under the <b>runtime's</b> discovery predicate
        /// (implements <c>IExtension</c> and carries an inherited <c>[ExtensionName]</c>) but the generator cannot
        /// reproduce its render protocol, or two unrelated types claim it. The recorded reason feeds the degrade
        /// message; such a call is never <c>HED7006</c>, because the runtime <em>will</em> find it.</summary>
        public bool TryGetUnbindableReason(string name, out string reason) =>
            _unbindable.TryGetValue(name, out reason);

        /// <summary>True when the name resolves to something under the runtime's own discovery rule — bindable or
        /// not. <c>HED7006</c> ("the runtime will not find it either") fires only when this is false.</summary>
        public bool IsKnownToRuntime(string name) =>
            name != null && (_byName.ContainsKey(name) || _unbindable.ContainsKey(name));

        /// <summary>Display names of extension types classified as
        /// <see cref="BranchRole.Continuation"/>/<see cref="BranchRole.Terminal"/> that do <b>not</b> carry
        /// <c>[ScopeChannel]</c> — they cannot read the branch state at render time (HED7016).</summary>
        public IReadOnlyList<string> DriftTypes => _driftTypes;

        /// <summary>One type that satisfies the <b>runtime's</b> discovery predicate — implements
        /// <c>IExtension</c> and carries an <c>[ExtensionName]</c> read with <c>inherit: true</c>. Discovery and
        /// <i>bindability</i> are two independent axes: the generator can only reproduce the render protocol of a
        /// non-abstract class deriving from <c>AbstractExtension</c>, so a candidate that fails that test is
        /// discovered (the runtime finds it) but not bound (the call degrades to dynamic).</summary>
        private sealed class Candidate
        {
            public INamedTypeSymbol Type;
            public List<string> Names;
            public bool Replaces;
            public bool Bindable;
            public string UnbindableReason;
            public int OrderingKey;
        }

        public static ExtensionBinder Build(Compilation compilation)
        {
            var byName = new Dictionary<string, Info>(System.StringComparer.Ordinal);
            var unbindable = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var driftTypes = new List<string>();
            if (compilation == null)
                return new ExtensionBinder(byName, unbindable, driftTypes);

            var nameAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ExtensionNameAttribute");
            var abstractExtension = compilation.GetTypeByMetadataName("Heddle.Core.AbstractExtension");
            if (nameAttr == null || abstractExtension == null)
                return new ExtensionBinder(byName, unbindable, driftTypes);

            // Attribute symbols may be null against an older engine reference; reads degrade safely.
            var symbols = new AttrSymbols
            {
                NameAttr = nameAttr,
                AbstractExtension = abstractExtension,
                ExtensionInterface = compilation.GetTypeByMetadataName("Heddle.Runtime.IExtension"),
                ReplaceAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ExtensionReplaceAttribute"),
                DataTypeAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.DataTypeAttribute"),
                ChainedTypeAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ChainedTypeAttribute"),
                RoleAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.BranchRoleAttribute"),
                ScopeChannelAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ScopeChannelAttribute"),
                EncodeOutputAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.EncodeOutputAttribute"),
                NotEncodeAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.NotEncodeAttribute"),
                ZeroOutputAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ZeroOutputAttribute"),
                PropAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.PropAttribute")
            };

            // Engine assembly first; the host's assemblies then walk same order as runtime to let subclasses override built-ins.
            var assemblies = new List<IAssemblySymbol>();
            var engine = abstractExtension.ContainingAssembly;
            if (engine != null)
                assemblies.Add(engine);
            if (!SymbolEqualityComparer.Default.Equals(compilation.Assembly, engine))
                assemblies.Add(compilation.Assembly);
            foreach (var referenced in compilation.SourceModule.ReferencedAssemblySymbols)
                if (!SymbolEqualityComparer.Default.Equals(referenced, engine))
                    assemblies.Add(referenced);

            var exportAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ExportExtensionsAttribute");
            var candidates = new List<Candidate>();
            foreach (var assembly in assemblies)
            {
                var perAssembly = new List<Candidate>();
                CollectExported(assembly, SymbolEqualityComparer.Default.Equals(assembly, engine), exportAttr,
                    symbols, perAssembly);
                candidates.AddRange(StableOrderBy(perAssembly, c => c.OrderingKey));
            }

            foreach (var candidate in StableOrderBy(candidates, c => c.Replaces ? 1 : 0))
                Register(candidate, symbols, byName, unbindable, driftTypes);

            return new ExtensionBinder(byName, unbindable, driftTypes);
        }

        /// <summary>Stable order by a small integer key — <c>List.Sort</c> is unstable and LINQ is not
        /// available to netstandard2.0 without pulling in the whole namespace.</summary>
        private static List<Candidate> StableOrderBy(List<Candidate> source, System.Func<Candidate, int> key)
        {
            var indexed = new List<KeyValuePair<int, Candidate>>(source.Count);
            for (int i = 0; i < source.Count; i++)
                indexed.Add(new KeyValuePair<int, Candidate>(i, source[i]));
            indexed.Sort((a, b) =>
            {
                var byKey = key(a.Value).CompareTo(key(b.Value));
                return byKey != 0 ? byKey : a.Key.CompareTo(b.Key);
            });

            var result = new List<Candidate>(source.Count);
            foreach (var pair in indexed)
                result.Add(pair.Value);
            return result;
        }

        private static void Register(Candidate candidate, AttrSymbols symbols, Dictionary<string, Info> byName,
            Dictionary<string, string> unbindable, List<string> driftTypes)
        {
            Info info = default;
            bool built = false;

            foreach (var name in candidate.Names)
            {
                if (name.Length == 0)
                    continue;

                if (!candidate.Bindable)
                {
                    // Name must not reach HED7006 — runtime will find it.
                    if (!byName.ContainsKey(name) && !unbindable.ContainsKey(name))
                        unbindable[name] = candidate.UnbindableReason;
                    continue;
                }

                if (!built)
                {
                    info = BuildInfo(candidate.Type, symbols);
                    built = true;

                    if (info.IsBranchParticipant && !info.HasScopeChannel)
                        driftTypes.Add(info.GlobalName);
                }

                bool hasIncumbent = byName.TryGetValue(name, out var incumbent);
                var verdict = ExtensionRegistrationRules.ResolveForBuild(
                    hasIncumbent,
                    candidate.Replaces,
                    hasIncumbent && SymbolTypeFacts.HierarchyAssignable(IncumbentType(incumbent, candidate), candidate.Type),
                    hasIncumbent && SymbolTypeFacts.HierarchyAssignable(candidate.Type, IncumbentType(incumbent, candidate)));

                switch (verdict)
                {
                    case ExtensionRegistrationVerdict.Register:
                    case ExtensionRegistrationVerdict.Replace:
                        byName[name] = info;
                        unbindable.Remove(name);
                        break;
                    case ExtensionRegistrationVerdict.KeepIncumbent:
                        break;
                    default:
                        // Host wiring error: degrade to dynamic instead of failing the build.
                        byName.Remove(name);
                        unbindable[name] = "extension name '" + name + "' is claimed by unrelated types (" +
                                           incumbent.BareTypeName + ", " + SymbolTypeIdentity.FullName(candidate.Type) +
                                           ") — the runtime would raise TemplateOverrideException";
                        break;
                }
            }
        }

        private static INamedTypeSymbol IncumbentType(Info incumbent, Candidate candidate) =>
            incumbent.TypeSymbol ?? candidate.Type;

        private sealed class AttrSymbols
        {
            public INamedTypeSymbol NameAttr;
            public INamedTypeSymbol AbstractExtension;
            public INamedTypeSymbol ExtensionInterface;
            public INamedTypeSymbol ReplaceAttr;
            public INamedTypeSymbol DataTypeAttr;
            public INamedTypeSymbol ChainedTypeAttr;
            public INamedTypeSymbol RoleAttr;
            public INamedTypeSymbol ScopeChannelAttr;
            public INamedTypeSymbol EncodeOutputAttr;
            public INamedTypeSymbol NotEncodeAttr;
            public INamedTypeSymbol ZeroOutputAttr;
            public INamedTypeSymbol PropAttr;
        }

        /// <summary>
        /// The <b>discovery scope</b> of one assembly, as the runtime's <c>TemplateFactory.ObtainExtensions</c> defines it.
        /// <list type="bullet">
        /// <item><description>The <b>engine</b> assembly is scanned whole and unconditionally — that is
        /// <c>LoadBaseExtensions</c>, and <c>Heddle</c> carries no <c>[ExportExtensions]</c> on
        /// itself.</description></item>
        /// <item><description>Any other assembly contributes <b>only</b> what its
        /// <c>[assembly: ExportExtensions(...)]</c> attributes name; the parameterless <c>All</c> form contributes
        /// the whole assembly and short-circuits its remaining attributes, mirroring the runtime's
        /// <c>break</c>.</description></item>
        /// <item><description>An assembly with <b>no</b> such attribute contributes nothing.</description></item>
        /// </list>
        /// <para>Unconditional scans (used previously) bound extensions the runtime would never register: the
        /// manifest recorded a name the live registry cannot resolve, and the gauntlet's extension-identity check
        /// turned every render of every such template into a silent, permanent fallback.</para>
        /// </summary>
        private static void CollectExported(IAssemblySymbol assembly, bool isEngine, INamedTypeSymbol exportAttr,
            AttrSymbols symbols, List<Candidate> candidates)
        {
            if (isEngine)
            {
                CollectTypes(assembly.GlobalNamespace, symbols, candidates);
                return;
            }

            if (exportAttr == null)
                return;   // an engine reference predating the attribute — nothing outside it can be exported

            foreach (var attr in assembly.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, exportAttr))
                    continue;

                // Parameterless constructor = All; stops reading this assembly's attributes.
                if (attr.ConstructorArguments.Length == 0)
                {
                    CollectTypes(assembly.GlobalNamespace, symbols, candidates);
                    return;
                }

                foreach (var exported in ExportedTypes(attr))
                    InspectType(exported, symbols, candidates);
            }
        }

        /// <summary>The types an <c>[ExportExtensions(...)]</c> occurrence names. The runtime hands exactly these to
        /// <c>LoadExtensions</c>, which applies the same discovery predicate — so a named type that is not an
        /// extension contributes nothing, and nested types are <b>not</b> walked into (only the named type itself
        /// is offered).</summary>
        private static IEnumerable<INamedTypeSymbol> ExportedTypes(AttributeData attr)
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

        /// <summary>Walks namespaces and nested types; omitting nested types caused runtime/build divergence.</summary>
        private static void CollectTypes(INamespaceSymbol ns, AttrSymbols symbols, List<Candidate> candidates)
        {
            foreach (var type in ns.GetTypeMembers())
                CollectTypeAndNested(type, symbols, candidates);
            foreach (var child in ns.GetNamespaceMembers())
                CollectTypes(child, symbols, candidates);
        }

        private static void CollectTypeAndNested(INamedTypeSymbol type, AttrSymbols symbols, List<Candidate> candidates)
        {
            InspectType(type, symbols, candidates);
            foreach (var nested in type.GetTypeMembers())
                CollectTypeAndNested(nested, symbols, candidates);
        }

        private static void InspectType(INamedTypeSymbol type, AttrSymbols symbols, List<Candidate> candidates)
        {
            if (!ImplementsExtension(type, symbols))
                return;

            var names = ReadExtensionNames(type, symbols.NameAttr);
            if (names.Count == 0)
                return;

            // Bindability (generator's constraint, applied after discovery): code reproduces AbstractExtension protocol only.
            string unbindableReason = null;
            if (type.IsAbstract || type.TypeKind != TypeKind.Class)
                unbindableReason = "extension type '" + SymbolTypeIdentity.FullName(type) + "' is not instantiable";
            else if (!DerivesFrom(type, symbols.AbstractExtension))
                unbindableReason = "extension type '" + SymbolTypeIdentity.FullName(type) +
                                   "' implements IExtension directly (not via AbstractExtension)";

            candidates.Add(new Candidate
            {
                Type = type,
                Names = names,
                Replaces = HasDeclaredAttribute(type, symbols.ReplaceAttr),
                Bindable = unbindableReason == null,
                UnbindableReason = unbindableReason,
                OrderingKey = ExtensionRegistrationRules.OrderingKey(
                    HasInterfaceTypeArgument(type, symbols.DataTypeAttr),
                    HasInterfaceTypeArgument(type, symbols.ChainedTypeAttr))
            });
        }

        private static Info BuildInfo(INamedTypeSymbol type, AttrSymbols symbols)
        {
            var global = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var assemblyName = SymbolTypeIdentity.AssemblyNameOf(type);
            return new Info(global, SymbolTypeIdentity.FullName(type), SymbolTypeIdentity.AqnSansVersion(type),
                assemblyName,
                OverridesCompileTimeHook(type, symbols.AbstractExtension),
                string.Equals(assemblyName, "Heddle", System.StringComparison.Ordinal),
                ReadBranchRole(type, symbols.RoleAttr),
                HasAttribute(type, symbols.ScopeChannelAttr),
                HasAttribute(type, symbols.EncodeOutputAttr),
                HasAttribute(type, symbols.NotEncodeAttr),
                HasAttribute(type, symbols.ZeroOutputAttr),
                ReadPropParameters(type, symbols.PropAttr),
                type,
                ReadDataTypes(type, symbols.DataTypeAttr));
        }

        /// <summary>Reads every <c>[DataType]</c> over the base-type chain — the attribute is repeatable and the
        /// runtime reads it with <c>inherit: true</c>, so a subclass accepts what its base accepted as well as what
        /// it declares itself.</summary>
        private static IReadOnlyList<ITypeSymbol> ReadDataTypes(INamedTypeSymbol type, INamedTypeSymbol attrType)
        {
            if (attrType == null)
                return EmptyDataTypes;

            List<ITypeSymbol> accepted = null;
            for (var t = type; t != null; t = t.BaseType)
            foreach (var attr in t.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType) ||
                    attr.ConstructorArguments.Length != 1 ||
                    !(attr.ConstructorArguments[0].Value is ITypeSymbol accepts))
                    continue;
                accepted ??= new List<ITypeSymbol>();
                accepted.Add(accepts);
            }

            return (IReadOnlyList<ITypeSymbol>) accepted ?? EmptyDataTypes;
        }

        /// <summary>The symbol-side twin of <c>Type.IsImplement&lt;IExtension&gt;()</c> — the transitive interface
        /// set. Falls back to the <c>AbstractExtension</c> derivation test when the interface symbol is
        /// unresolvable (an older engine reference).</summary>
        private static bool ImplementsExtension(INamedTypeSymbol type, AttrSymbols symbols)
        {
            if (symbols.ExtensionInterface == null)
                return DerivesFrom(type, symbols.AbstractExtension);

            foreach (var iface in type.AllInterfaces)
                if (SymbolEqualityComparer.Default.Equals(iface, symbols.ExtensionInterface))
                    return true;
            return false;
        }

        /// <summary>Reads <c>[ExtensionName]</c> over the base-type chain — the attribute is
        /// <c>Inherited = true</c> and the runtime reads it with <c>inherit: true</c>, so
        /// <c>class MyIf : IfExtension</c> registers under <c>"if"</c>. The base-chain walk unifies the reading of
        /// <c>[ExtensionName]</c>, <c>[BranchRole]</c>, <c>[ScopeChannel]</c> and <c>[Prop]</c>.
        /// <para>Most-derived layer first, matching reflection's <c>inherit: true</c> enumeration order for a
        /// class-targeted attribute; the runtime's own dictionary keys the names, so order affects only the
        /// sequence in which one type's several names are offered.</para></summary>
        private static List<string> ReadExtensionNames(INamedTypeSymbol type, INamedTypeSymbol nameAttr)
        {
            var names = new List<string>();
            for (var t = type; t != null; t = t.BaseType)
            {
                foreach (var attr in t.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, nameAttr))
                        continue;
                    if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is string n &&
                        !names.Contains(n))
                        names.Add(n);
                }
            }

            return names;
        }

        /// <summary>Declared-only attribute presence — <c>[ExtensionReplace]</c> is the one attribute the runtime
        /// reads <b>without</b> <c>inherit: true</c> (<c>TemplateFactory.LoadExtensions</c>), so a subclass of a
        /// replacing extension does not itself replace.</summary>
        private static bool HasDeclaredAttribute(INamedTypeSymbol type, INamedTypeSymbol attrType)
        {
            if (attrType == null)
                return false;
            foreach (var attr in type.GetAttributes())
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType))
                    return true;
            return false;
        }

        /// <summary>The runtime's ordering probe: does any inherited <c>[DataType]</c>/<c>[ChainedType]</c> name an
        /// interface? (<c>LoadExtensions</c>' <c>OrderBy</c>/<c>ThenBy</c>.)</summary>
        private static bool HasInterfaceTypeArgument(INamedTypeSymbol type, INamedTypeSymbol attrType)
        {
            if (attrType == null)
                return false;

            for (var t = type; t != null; t = t.BaseType)
                foreach (var attr in t.GetAttributes())
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType) &&
                        attr.ConstructorArguments.Length >= 1 &&
                        attr.ConstructorArguments[0].Value is ITypeSymbol arg &&
                        arg.TypeKind == TypeKind.Interface)
                        return true;

            return false;
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

        /// <summary>Decodes the extension's <c>[Prop]</c> declarations over the base-type chain,
        /// outermost base first ([Prop] is <c>Inherited = true</c>; Roslyn surfaces no inherited attributes) —
        /// the same layer order <c>PropLayout.ResolveFromExtension</c> walks on the dynamic tier. Degrades to an
        /// empty list when the attribute symbol is unresolvable (older engine reference).</summary>
        private static IReadOnlyList<PropParameter> ReadPropParameters(INamedTypeSymbol type,
            INamedTypeSymbol propAttr)
        {
            if (propAttr == null)
                return EmptyParameters;

            // Must stop at System.Object to match the dynamic tier's layer-count logic.
            var layers = new List<INamedTypeSymbol>();
            for (var t = type; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
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
                    // Null names are admitted; they diverge from the dynamic tier only if skipped.
                    var name = attr.ConstructorArguments[0].Value as string;

                    // Type usability uses SymbolTypeFacts.IsUsableAsPropType to match the dynamic tier.
                    var typeSymbol = attr.ConstructorArguments[1].Value as ITypeSymbol;

                    var parameter = new PropParameter
                    {
                        Name = name,
                        Type = typeSymbol,
                        Level = level
                    };

                    foreach (var namedArg in attr.NamedArguments)
                    {
                        if (namedArg.Key == "Default" && !namedArg.Value.IsNull)
                        {
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
