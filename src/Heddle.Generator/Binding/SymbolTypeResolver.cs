using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Heddle.Language.Binding;
using Heddle.Language.Members;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

        /// <summary><see cref="Inaccessible"/> is a <see cref="Failed"/> this compilation is not entitled to call a
        /// failure: the member is there in metadata and the engine's own visibility policy accepts it, but nothing
        /// generated into the consumer's assembly could name it.</summary>
        internal enum PathKind { Resolved, DynamicHop, Failed, Inaccessible }

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

        /// <summary>The Roslyn-side projection of the shared alias table; key set must match runtime's CSharpTypeNames.
        /// <c>dynamic</c> maps to <c>System.Object</c> to match the runtime exactly.</summary>
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

        /// <summary>The last ResolveModelType failure class.</summary>
        public TypeSpellingFault LastFault { get; private set; }

        /// <summary>Resolves model types using the shared spelling parser over the runtime's lookup rule.</summary>
        public ITypeSymbol ResolveModelType(string text, IReadOnlyList<string> usings)
        {
            LastFault = TypeSpellingFault.None;
            if (string.IsNullOrEmpty(text))
                return null;

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

        /// <summary>The Roslyn ITypeLookup adapter over keyword aliases and the runtime's index rule.</summary>
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

        /// <summary>Checks whether any type named <paramref name="text"/> exists in the compilation or references.</summary>
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
                    result.Kind = HiddenByAccessibility(current, segments[i])
                        ? PathKind.Inaccessible
                        : PathKind.Failed;
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

        /// <summary>Member lookup uses the shared MemberPathWalk through a Roslyn adapter, aligned with runtime behavior.</summary>
        private static IPropertySymbol FindProperty(ITypeSymbol type, string name) =>
            MemberPathWalk.TryFind(SymbolMemberModel.Instance, type, name, out var found) ? found : null;

        /// <summary>Whether generated code in the compilation's own assembly may name <paramref name="symbol"/>.
        /// The emitter writes fully-qualified type names into the consumer's assembly, so a name it may not write is
        /// not a name it may pre-compile.</summary>
        public bool IsAccessibleFromCompilation(ISymbol symbol) =>
            symbol == null || _compilation == null ||
            _compilation.IsSymbolAccessibleWithin(symbol, _compilation.Assembly);

        /// <summary>
        /// Whether the <b>engine</b> would find <paramref name="name"/> on <paramref name="receiver"/> although this
        /// compilation's symbol model has no such member — the member exists in metadata and only accessibility
        /// hides it.
        /// <para>Roslyn imports from a metadata reference only what the importing assembly could legally name, so an
        /// <c>internal</c> member on a model type in a referenced assembly is not <i>inaccessible</i> in the symbol
        /// model, it is <b>absent</b> from it, and indistinguishable from a typo. The engine reads it: reflection
        /// ignores assembly boundaries and the member tier accepts an internal getter declared on the receiver. The
        /// two were reported identically — at error severity — so a valid template failed the consumer's build.</para>
        /// <para>A source receiver is answered without the probe. Roslyn shows a compilation every member of its own
        /// types, so a miss there is genuine, and the probe compilation carries no source to find it in anyway.</para>
        /// </summary>
        private bool HiddenByAccessibility(ITypeSymbol receiver, string name)
        {
            if (receiver == null || _compilation == null)
                return false;
            foreach (var location in receiver.Locations)
                if (location.IsInSource)
                    return false;

            var mapped = MapToFullMetadataView(receiver);
            return mapped != null && MemberPathWalk.TryFind(SymbolMemberModel.Instance, mapped, name, out _);
        }

        /// <summary>
        /// The same references opened with <see cref="MetadataImportOptions.All"/> — as close to reflection's view,
        /// and therefore the engine's, as the symbol model gets. Built per compilation and only on the path that is
        /// about to report a member failure, which is rare; held weakly against the compilation it describes so it
        /// dies with it rather than accumulating one per keystroke in the IDE.
        /// </summary>
        private static readonly ConditionalWeakTable<Compilation, Compilation> FullMetadataViews =
            new ConditionalWeakTable<Compilation, Compilation>();

        private ITypeSymbol MapToFullMetadataView(ITypeSymbol type)
        {
            if (!(type is INamedTypeSymbol named))
                return null;   // arrays, pointers and type parameters carry no user members to be hidden

            var metadataName = MetadataNameOf(named.OriginalDefinition);
            if (metadataName == null)
                return null;

            var view = FullMetadataViews.GetValue(_compilation, compilation =>
                CSharpCompilation.Create("HeddleMetadataProbe", null, compilation.References,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                        metadataImportOptions: MetadataImportOptions.All)));

            // Null on ambiguity across references as well as on absence; either way this side proves nothing and the
            // caller keeps the answer it already had.
            return view.GetTypeByMetadataName(metadataName);
        }

        /// <summary>The CLR name <see cref="Compilation.GetTypeByMetadataName"/> reads — nested types joined by
        /// <c>+</c>, generic arity as a backtick suffix (already in <see cref="ISymbol.MetadataName"/>).</summary>
        private static string MetadataNameOf(INamedTypeSymbol type)
        {
            var name = type.MetadataName;
            var outermost = type;
            for (var outer = type.ContainingType; outer != null; outer = outer.ContainingType)
            {
                name = outer.MetadataName + "+" + name;
                outermost = outer;
            }

            var ns = outermost.ContainingNamespace;
            if (ns == null || ns.IsGlobalNamespace)
                return name;
            return ns.ToDisplayString() + "." + name;
        }

        /// <summary>The Roslyn adapter of the shared member model.</summary>
        private sealed class SymbolMemberModel : ITypeModel<ITypeSymbol, IPropertySymbol>
        {
            internal static readonly SymbolMemberModel Instance = new SymbolMemberModel();

            /// <summary>The Hidden attribute by fully-qualified name to avoid foreign attributes.</summary>
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

            /// <summary>Deliberately empty: mirrors reflection where GetProperty on an interface does not search base interfaces.</summary>
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

        /// <summary>Tests whether a type is non-nullable value type; unified to avoid multiple spellings.</summary>
        public static bool IsNonNullableValueType(ITypeSymbol type)
        {
            if (type == null || !type.IsValueType)
                return false;
            return !NullableProbe.TryGetNullableUnderlying(type, out _);
        }

        private static readonly SymbolTypeFacts NullableProbe = new SymbolTypeFacts(null);

        /// <summary>
        /// Whether a resolved path ends on a ref struct. Every consumer of a path's value boxes it — a rendered
        /// parameter is an <c>object</c>, and an expression operand is one too — and a ref struct cannot be boxed.
        /// <para>Neither tier can produce a value here; the difference is how they say so. The engine refuses the
        /// template with <c>HED0005</c> when it compiles it, which is a diagnostic the host can catch and report
        /// against the template. Emitting the path put <c>CS0030</c> into the consumer's <em>build</em> instead —
        /// no Heddle id, reported against a <c>.heddle</c> file, and unfixable without editing the model. Degrading
        /// hands the question back to the tier whose refusal is the contract.</para>
        /// <para>A ref struct passed <em>through</em> to a member of its own — <c>Buf.Length</c> — is a different
        /// matter and stays precompiled: what is read there is the <c>int</c>.</para>
        /// </summary>
        public static bool EndsOnRefStruct(PathResolution resolution)
        {
            if (resolution == null || resolution.Hops.Count == 0)
                return false;
            return resolution.Hops[resolution.Hops.Count - 1].Property?.IsRefLikeType == true;
        }

        public static string FullyQualified(ITypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
