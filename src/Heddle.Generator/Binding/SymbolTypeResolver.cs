using System.Collections.Generic;
using Heddle.Language.Binding;
using Heddle.Language.Members;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The emitter's symbol-metadata member resolver — the <see cref="ISymbol"/> counterpart of the runtime's
    /// reflection-based <c>MemberPathResolver</c>. Used to <b>type</b> member hops so the emitter picks the right
    /// null-safety form, not to report diagnostics. Same posture as the runtime member tier: properties only,
    /// case-sensitive, readable, getter public-or-internal, not <c>[Hidden]</c>.
    /// </summary>
    internal sealed class SymbolTypeResolver
    {
        private readonly Compilation _compilation;

        public SymbolTypeResolver(Compilation compilation) => _compilation = compilation;

        internal enum PathKind { Resolved, DynamicHop, Failed }

        internal readonly struct Hop
        {
            public Hop(ITypeSymbol receiver, ITypeSymbol property, string name)
            {
                Receiver = receiver;
                Property = property;
                Name = name;
            }

            public ITypeSymbol Receiver { get; }
            public ITypeSymbol Property { get; }
            public string Name { get; }
        }

        internal sealed class PathResolution
        {
            public PathKind Kind;
            public List<Hop> Hops = new List<Hop>();
            public int DynamicIndex = -1;
            public ITypeSymbol ResultType;
        }

        /// <summary>The Roslyn-side projection of the shared alias table. It cannot reuse the
        /// <see cref="System.Type"/> values <c>CSharpTypeNames</c> carries, so it is an adapter — but its
        /// <b>key set</b> is asserted equal to <c>CSharpTypeNames.AliasNames</c> in full. Adding an alias to one
        /// side without the other is a red build rather than a silent divergence between what a template may
        /// write and what the build tier binds.
        /// <para><c>dynamic</c> is now a key here, mapped to <c>System.Object</c> — which is exactly what the
        /// runtime's alias table resolves it to (<c>CSharpTypeNames</c> maps it to <c>typeof(object)</c>). Leaving
        /// it out was a build-tier-only refusal of a spelling the run tier accepts, and "match the runtime exactly"
        /// leaves no room for it.</para></summary>
        internal static readonly Dictionary<string, SpecialType> Keywords = new Dictionary<string, SpecialType>
        {
            ["bool"] = SpecialType.System_Boolean, ["byte"] = SpecialType.System_Byte,
            ["sbyte"] = SpecialType.System_SByte, ["char"] = SpecialType.System_Char,
            ["decimal"] = SpecialType.System_Decimal, ["double"] = SpecialType.System_Double,
            ["float"] = SpecialType.System_Single, ["int"] = SpecialType.System_Int32,
            ["uint"] = SpecialType.System_UInt32, ["long"] = SpecialType.System_Int64,
            ["ulong"] = SpecialType.System_UInt64, ["short"] = SpecialType.System_Int16,
            ["ushort"] = SpecialType.System_UInt16, ["object"] = SpecialType.System_Object,
            ["string"] = SpecialType.System_String, ["dynamic"] = SpecialType.System_Object,
        };

        /// <summary>The last <see cref="ResolveModelType"/> call's failure class — <c>Ambiguous</c> is the
        /// build-time twin of the runtime's "the type name is ambigous" throw and surfaces as <c>HED7023</c>;
        /// everything else stays a quiet degrade (or <c>HED7007</c> where the caller already guards).</summary>
        public TypeSpellingFault LastFault { get; private set; }

        /// <summary>
        /// Model-type resolution now runs the <b>shared</b> spelling parser (<see cref="TypeSpelling"/>) over
        /// the <b>runtime's</b> lookup rule (<see cref="SymbolTypeIndex"/>). Three things change:
        /// <list type="bullet">
        /// <item><description>generic, array, tuple and dotted-nested spellings resolve at all — the build tier
        /// supported none of them, so whole feature areas silently never precompiled;</description></item>
        /// <item><description>the unilateral implicit <c>System</c>/<c>System.Collections.Generic</c> fallback is
        /// gone: the runtime has no implicit namespaces, and a name resolvable only through them used to bind on
        /// one tier and not the other;</description></item>
        /// <item><description>a name several types answer to, unsettled by the <c>@using</c> imports, is
        /// <see cref="TypeSpellingFault.Ambiguous"/> — the runtime's own verdict — rather than a pick.</description></item>
        /// </list>
        /// </summary>
        public ITypeSymbol ResolveModelType(string text, IReadOnlyList<string> usings)
        {
            LastFault = TypeSpellingFault.None;
            if (string.IsNullOrEmpty(text))
                return null;

            // Nullable suffix (T?): resolve the underlying, then construct System.Nullable<T> for value types.
            if (text.Length > 1 && text[text.Length - 1] == '?')
            {
                var underlying = ResolveModelType(text.Substring(0, text.Length - 1).Trim(), usings);
                if (underlying == null)
                    return null;
                if (!underlying.IsValueType)
                    return underlying;   // T? on a reference type is just T
                var nullable = _compilation.GetTypeByMetadataName("System.Nullable`1");
                return nullable != null ? nullable.Construct(underlying) : underlying;
            }

            var lookup = new SymbolTypeLookup(_compilation, usings);
            if (TypeSpelling.TryResolve(text, lookup, out var resolved, out var fault))
                return resolved;

            LastFault = fault;
            return null;
        }

        /// <summary>The Roslyn <see cref="ITypeLookup{TType}"/> adapter: keyword aliases, then the runtime's
        /// index rule, then the generic/array construction Roslyn expresses natively.</summary>
        private sealed class SymbolTypeLookup : ITypeLookup<ITypeSymbol>
        {
            private readonly Compilation _compilation;
            private readonly IReadOnlyList<string> _usings;

            internal SymbolTypeLookup(Compilation compilation, IReadOnlyList<string> usings)
            {
                _compilation = compilation;
                _usings = usings ?? new string[0];
            }

            public bool TryResolveSimple(string name, int backtickArity, out ITypeSymbol type,
                out TypeSpellingFault fault)
            {
                fault = TypeSpellingFault.None;
                if (backtickArity == 0 && Keywords.TryGetValue(name, out var special))
                {
                    type = _compilation.GetSpecialType(special);
                    return true;
                }

                if (SymbolTypeIndex.For(_compilation).TryResolve(name, _usings, out var named, out fault))
                {
                    type = named;
                    return true;
                }

                type = null;
                return false;
            }

            public ITypeSymbol MakeArray(ITypeSymbol elementType) =>
                _compilation.CreateArrayTypeSymbol(elementType);

            public bool TryMakeGeneric(ITypeSymbol definition, IReadOnlyList<ITypeSymbol> arguments,
                out ITypeSymbol constructed)
            {
                constructed = null;
                if (!(definition is INamedTypeSymbol named) || named.Arity != arguments.Count)
                    return false;

                var array = new ITypeSymbol[arguments.Count];
                for (int i = 0; i < arguments.Count; i++)
                    array[i] = arguments[i];
                constructed = named.ConstructedFrom.Construct(array);
                return true;
            }

            public bool TryGetValueTupleDefinition(int arity, out ITypeSymbol definition)
            {
                definition = _compilation.GetTypeByMetadataName("System.ValueTuple`" + arity);
                return definition != null;
            }
        }

        /// <summary>
        /// Whether <b>any</b> type whose name matches <paramref name="text"/> (its final dotted segment) exists in the
        /// compilation or its referenced assemblies. Milestone 2 uses this as the <c>HED7007</c> guard: the runtime
        /// resolves a bare model type name by scanning loaded assemblies, so a name that <see cref="ResolveModelType"/>
        /// cannot bind (no namespace, no <c>@using</c>) may still be a perfectly valid type the dynamic path renders.
        /// Only when <b>no</b> such type exists anywhere is the declaration a genuine typo worth erroring.
        /// </summary>
        public bool TypeNameExistsAnywhere(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            var name = text;
            if (name.Length > 1 && name[name.Length - 1] == '?')
                name = name.Substring(0, name.Length - 1).Trim();
            if (Keywords.ContainsKey(name))
                return true;
            var dot = name.LastIndexOf('.');
            var simple = dot >= 0 ? name.Substring(dot + 1) : name;
            if (simple.Length == 0)
                return false;

            if (NamespaceContainsType(_compilation.GlobalNamespace, simple))
                return true;
            foreach (var reference in _compilation.References)
            {
                if (_compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asm &&
                    NamespaceContainsType(asm.GlobalNamespace, simple))
                    return true;
            }

            return false;
        }

        private static bool NamespaceContainsType(INamespaceSymbol ns, string simpleName)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (string.Equals(type.Name, simpleName, System.StringComparison.Ordinal))
                    return true;
            }

            foreach (var child in ns.GetNamespaceMembers())
            {
                if (NamespaceContainsType(child, simpleName))
                    return true;
            }

            return false;
        }

        public PathResolution ResolvePath(ITypeSymbol start, IReadOnlyList<string> segments)
        {
            var result = new PathResolution();
            ITypeSymbol current = start;
            for (int i = 0; i < segments.Count; i++)
            {
                if (current == null)
                {
                    result.Kind = PathKind.Failed;
                    result.DynamicIndex = i;
                    return result;
                }

                if (current.TypeKind == TypeKind.Dynamic)
                {
                    result.Kind = PathKind.DynamicHop;
                    result.DynamicIndex = i;
                    return result;
                }

                var prop = FindProperty(current, segments[i]);
                if (prop == null)
                {
                    result.Kind = PathKind.Failed;
                    result.DynamicIndex = i;
                    return result;
                }

                result.Hops.Add(new Hop(current, prop.Type, segments[i]));
                current = prop.Type;
            }

            result.Kind = PathKind.Resolved;
            result.ResultType = current;
            return result;
        }

        /// <summary>
        /// Member lookup is now the <b>shared</b> <see cref="MemberPathWalk"/> driven through the Roslyn
        /// <see cref="SymbolMemberModel"/> adapter, closing three verified divergences — all of which ran in the
        /// dangerous direction, because the same resolver drives <i>emission</i>, so extra permissiveness became
        /// emitted typed code the dynamic tier rejects:
        /// <list type="number">
        /// <item><description><c>Accessibility.ProtectedOrInternal</c> was accepted here and rejected by the
        /// runtime's <c>getter.IsAssembly || getter.IsPublic</c>. The runtime's narrower sandbox is normative
        /// and the generator tightens;</description></item>
        /// <item><description>an <c>internal</c> getter declared on a <b>base</b> class was accepted here, while
        /// <c>Type.GetProperty</c> never surfaces an inherited non-public property — the shared table's
        /// <c>declaredOnReceiver</c> row;</description></item>
        /// <item><description>the interface root's <c>AllInterfaces</c> walk surfaced base-interface members
        /// <c>Type.GetProperty</c> on an interface does not — the adapter now supplies no base-interface closure,
        /// matching the reflection adapter.</description></item>
        /// </list>
        /// </summary>
        private static IPropertySymbol FindProperty(ITypeSymbol type, string name) =>
            MemberPathWalk.TryFind(SymbolMemberModel.Instance, type, name, out var found) ? found : null;

        /// <summary>The Roslyn adapter of the shared member model.</summary>
        private sealed class SymbolMemberModel : ITypeModel<ITypeSymbol, IPropertySymbol>
        {
            internal static readonly SymbolMemberModel Instance = new SymbolMemberModel();

            /// <summary>The real attribute, by <b>fully-qualified metadata name</b>. The old check matched any
            /// attribute merely <i>named</i> <c>HiddenAttribute</c>, from any namespace, so a foreign attribute of
            /// that name hid a member at build time that the runtime exposed. Matching any namespace is not sound.</summary>
            private const string HiddenAttributeFullName = "Heddle.Attributes.HiddenAttribute";

            public bool IsDynamic(ITypeSymbol type) => type != null && type.TypeKind == TypeKind.Dynamic;

            public IEnumerable<IPropertySymbol> DeclaredProperties(ITypeSymbol type, string name)
            {
                foreach (var member in type.GetMembers(name))
                    if (member is IPropertySymbol property)
                        yield return property;
            }

            public MemberFacts FactsOf(IPropertySymbol member)
            {
                var getter = member.GetMethod;
                return new MemberFacts(
                    canRead: getter != null,
                    access: ToMemberAccess(getter?.DeclaredAccessibility ?? Accessibility.Private),
                    hasHidden: HasHidden(member),
                    isStatic: member.IsStatic);
            }

            public ITypeSymbol TypeOf(IPropertySymbol member) => member.Type;

            public ITypeSymbol BaseOf(ITypeSymbol type) => type.BaseType;

            public bool IsInterface(ITypeSymbol type) => type.TypeKind == TypeKind.Interface;

            /// <summary>Deliberately empty, mirroring the reflection adapter: <c>Type.GetProperty</c> on an
            /// interface does not search base interfaces, and that narrower behavior matches what the runtime does.</summary>
            public IEnumerable<ITypeSymbol> BaseInterfaces(ITypeSymbol type)
            {
                yield break;
            }

            private static bool HasHidden(IPropertySymbol member)
            {
                foreach (var attribute in member.GetAttributes())
                {
                    var attributeClass = attribute.AttributeClass;
                    if (attributeClass == null)
                        continue;
                    var ns = attributeClass.ContainingNamespace;
                    var qualified = ns == null || ns.IsGlobalNamespace
                        ? attributeClass.MetadataName
                        : ns.ToDisplayString() + "." + attributeClass.MetadataName;
                    if (string.Equals(qualified, HiddenAttributeFullName, System.StringComparison.Ordinal))
                        return true;
                }

                return false;
            }

            private static MemberAccess ToMemberAccess(Accessibility accessibility)
            {
                switch (accessibility)
                {
                    case Accessibility.Public: return MemberAccess.Public;
                    case Accessibility.Internal: return MemberAccess.Internal;
                    case Accessibility.ProtectedOrInternal: return MemberAccess.ProtectedOrInternal;
                    case Accessibility.Protected: return MemberAccess.Protected;
                    case Accessibility.ProtectedAndInternal: return MemberAccess.ProtectedAndInternal;
                    default: return MemberAccess.Private;
                }
            }
        }

        /// <summary>The <c>Nullable&lt;T&gt;</c> test, unified to avoid multiple spellings. Both
        /// <c>ConstructedFrom</c> and <c>OriginalDefinition</c> now go through the one Roslyn <c>ITypeFacts</c>
        /// adapter.</summary>
        public static bool IsNonNullableValueType(ITypeSymbol type)
        {
            if (type == null || !type.IsValueType)
                return false;
            return !NullableProbe.TryGetNullableUnderlying(type, out _);
        }

        private static readonly SymbolTypeFacts NullableProbe = new SymbolTypeFacts(null);

        public static string FullyQualified(ITypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
