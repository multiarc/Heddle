using System.Collections.Generic;
using System.Collections.Immutable;
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

        /// <summary>The memo key: the receiver <b>symbol</b> and the segments. Not a display string — two types
        /// carrying the same fully-qualified name is ordinary in a large reference closure, and keying on the name
        /// handed the second one the first one's <see cref="PathResolution.ResultType"/>, which decides the
        /// null-safety form, the numeric widening, the formatter and the member name the emitter writes. The
        /// separator is one no identifier can contain, so <c>["A.B"]</c> and <c>["A","B"]</c> stay distinct.</summary>
        private readonly Dictionary<(ITypeSymbol Start, string Path), PathResolution> _paths =
            new Dictionary<(ITypeSymbol, string), PathResolution>(PathKeyComparer.Instance);

        private sealed class PathKeyComparer : IEqualityComparer<(ITypeSymbol Start, string Path)>
        {
            internal static readonly PathKeyComparer Instance = new PathKeyComparer();

            public bool Equals((ITypeSymbol Start, string Path) x, (ITypeSymbol Start, string Path) y) =>
                SymbolEqualityComparer.Default.Equals(x.Start, y.Start) &&
                string.Equals(x.Path, y.Path, System.StringComparison.Ordinal);

            public int GetHashCode((ITypeSymbol Start, string Path) key) =>
                unchecked((key.Start == null ? 0 : SymbolEqualityComparer.Default.GetHashCode(key.Start)) * 397 ^
                          key.Path.GetHashCode());
        }

        /// <summary>
        /// Walks <paramref name="segments"/> off <paramref name="start"/>, typing each hop.
        /// <para>Memoized. Every path is resolved at least twice — once to decide the operand's type and once to
        /// write it — and in an editor the whole walk runs again on each keystroke.</para>
        /// </summary>
        public PathResolution ResolvePath(ITypeSymbol start, IReadOnlyList<string> segments)
        {
            var key = (start, string.Join("\0", segments));
            if (_paths.TryGetValue(key, out var memoized))
                return memoized;
            var resolved = ResolvePathCore(start, segments);
            _paths[key] = resolved;
            return resolved;
        }

        private PathResolution ResolvePathCore(ITypeSymbol start, IReadOnlyList<string> segments)
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
                    // Absent from the symbol model. Whether that is a typo or a member this compilation is merely
                    // not shown is settled by the caller that reports — see HiddenByAccessibility — because
                    // settling it costs a second compilation and most callers here never report anything.
                    result.Kind = PathKind.Failed;
                    result.DynamicIndex = i;
                    return result;
                }

                // Present, and the engine's own visibility policy accepts it, but generated code in this assembly
                // may not name it. That happens whenever the model arrives as a source compilation rather than as
                // a compiled file — a project-to-project reference in any workspace — where Roslyn shows the
                // internal member instead of hiding it, and the emitter wrote it straight into a `.g.cs` the
                // consumer's build then rejected with CS0122.
                // Same class of refusal, different word from the compiler: reflection ignores [Obsolete] entirely,
                // so the engine reads an error-obsolete member and renders, while the generated read of it is a
                // CS0619 in the consumer's build — again attributed to a .heddle file it cannot be fixed from.
                // The member's own name is only half of what the read spells. The emitter writes the property's TYPE
                // as well — `default(T)` in the null-safe form, the receiver's own name in the ref-struct form — so
                // an error-obsolete property type puts a CS0619 in the consumer's build off a member that carries no
                // attribute at all. The model-only restrictions are deliberately not asked here: a ref struct is
                // writable, and a hop THROUGH one to a member of its own stays precompiled.
                if (!IsAccessibleFromCompilation(prop) || !IsAccessibleFromCompilation(prop.GetMethod) ||
                    IsObsoleteError(prop) || IsObsoleteError(prop.GetMethod) ||
                    ClassifyTypeName(prop.Type, out _) != NameFault.None)
                {
                    result.Kind = PathKind.Inaccessible;
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

        /// <summary>
        /// Whether <paramref name="symbol"/> carries <c>[Obsolete(…, error: true)]</c> — the attribute form that
        /// makes every mention of the name a compile <b>error</b> (CS0619/CS0672) rather than a warning.
        /// <para>Only the error form counts. Reflection ignores <c>[Obsolete]</c> altogether, so both forms render
        /// on the engine; degrading on the warning form as well would take every deprecated-but-working model off
        /// the precompiled tier, which is a cost with nothing on the other side of it. The generated file already
        /// opens with a blanket <c>#pragma warning disable</c>, so the warning form raises nothing in the consumer's
        /// build against code they did not write, which is the only thing that had to be true for leaving it.</para>
        /// <para>Nested spellings are walked: an array or pointer whose element type is error-obsolete, a generic
        /// constructed over one, and a type <b>nested inside</b> one, are all just as unwritable as the bare name —
        /// <c>Legacy.Inner</c> cannot be spelled without spelling <c>Legacy</c>, and C# reports the error on the
        /// outer name.</para>
        /// </summary>
        public static bool IsObsoleteError(ISymbol symbol)
        {
            if (symbol == null)
                return false;

            if (symbol is IArrayTypeSymbol array)
                return IsObsoleteError(array.ElementType);

            if (symbol is IPointerTypeSymbol pointer)
                return IsObsoleteError(pointer.PointedAtType);

            foreach (var attribute in symbol.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null ||
                    !string.Equals(attributeClass.MetadataName, "ObsoleteAttribute", System.StringComparison.Ordinal) ||
                    attributeClass.ContainingNamespace?.ToDisplayString() != "System")
                    continue;
                var arguments = attribute.ConstructorArguments;
                if (arguments.Length >= 2 && arguments[1].Value is bool isError && isError)
                    return true;
            }

            // Only for types. A member inherited from an error-obsolete base is read off the derived name, which the
            // compiler does not object to, so walking a member's container would degrade templates that compile.
            if (symbol is INamedTypeSymbol named)
            {
                foreach (var argument in named.TypeArguments)
                    if (IsObsoleteError(argument))
                        return true;
                if (IsObsoleteError(named.ContainingType))
                    return true;
            }

            return false;
        }

        /// <summary>Why generated code cannot write a type's name.</summary>
        internal enum NameFault
        {
            None,

            /// <summary>Accessibility or error-obsolescence: the type is a perfectly ordinary one that this
            /// particular assembly is not allowed to mention. Author-fixable, so it is worth reporting (HED7030).
            /// </summary>
            Unnameable,

            /// <summary>A type no generated code could ever hold a value of, whoever compiled it. Nothing to report:
            /// the only remedy is a different model, and the engine serves the template either way.</summary>
            Unusable,
        }

        /// <summary>
        /// Whether generated code in the consumer's assembly may write <paramref name="type"/>'s name where a
        /// <b>value</b> of it lives — a local, a <c>default(T)</c>, a cast target, a field. Everything the emitter
        /// spells is such a position, and a name the emitter may spell but the consumer's compiler rejects ends the
        /// same way every time: an error against a <c>.g.cs</c>, attributed to a <c>.heddle</c> file, that the
        /// consumer cannot fix from the template. The engine binds by reflection, which asks none of these questions,
        /// so it renders (or refuses on its own terms) regardless — degrading hands the template back to it.
        /// <para>The list is meant to be complete over <see cref="TypeKind"/> rather than grown one reported defect
        /// at a time. Nameable: class, struct, enum, interface, delegate, <c>dynamic</c>, and arrays of those.
        /// Refused: <see cref="TypeKind.Error"/>/<see cref="TypeKind.Unknown"/> (the name does not resolve, CS0246);
        /// pointers and function pointers (CS0214 without <c>/unsafe</c>, and unboxable); type parameters and
        /// anything containing one — an open generic such as <c>List`1</c> or a type nested in one — since generated
        /// code has no generic context to bind them in (CS0246/CS0305); an unbound generic (<c>List&lt;&gt;</c>,
        /// CS7003); VB modules and script submissions, which C# has no syntax for; <c>System.Void</c> (CS1536,
        /// CS1547); a static class (CS0721/CS0723); and an anonymous type, which has no writable name at all.
        /// Restricted types — <c>TypedReference</c> and friends — need no row of their own: they are
        /// <see cref="ITypeSymbol.IsRefLikeType"/> and so are caught as ref structs by
        /// <see cref="ClassifyModelType"/>.</para>
        /// </summary>
        public NameFault ClassifyTypeName(ITypeSymbol type, out string reason)
        {
            reason = null;
            if (type == null)
                return NameFault.None;

            switch (type.TypeKind)
            {
                case TypeKind.Array:
                    // An array is exactly as writable as its element type, and an element type has to be able to
                    // hold a value too — `int*[]` and `Math[]` are both rejected on the element, not the brackets.
                    return ClassifyTypeName(((IArrayTypeSymbol) type).ElementType, out reason);
                case TypeKind.Error:
                case TypeKind.Unknown:
                    return Unusable(type, "does not resolve to a type this compilation can name", out reason);
                case TypeKind.Pointer:
                case TypeKind.FunctionPointer:
                    return Unusable(type, "is a pointer type, which generated code cannot name outside an unsafe " +
                                          "context", out reason);
                case TypeKind.TypeParameter:
                    return Unusable(type, "is a type parameter, and generated code has no generic context to bind " +
                                          "it in", out reason);
                case TypeKind.Module:
                case TypeKind.Submission:
                    return Unusable(type, "is not a type C# has a syntax for", out reason);
            }

            if (type.SpecialType == SpecialType.System_Void)
                return Unusable(type, "is 'void', which cannot be a parameter, a local or a cast target", out reason);

            if (type.IsStatic)
                return Unusable(type, "is a static type and cannot hold a value", out reason);

            if (type.IsAnonymousType)
                return Unusable(type, "is an anonymous type and has no name to write", out reason);

            if (type is INamedTypeSymbol named)
            {
                if (named.IsUnboundGenericType)
                    return Unusable(type, "is an unbound generic type", out reason);
                if (ContainsTypeParameter(named))
                    return Unusable(type, "is an open generic type, and generated code has no generic context to " +
                                          "bind its type parameters in", out reason);
            }

            if (!IsAccessibleFromCompilation(type) || IsObsoleteError(type))
            {
                reason = "'" + FullyQualified(type) + "' cannot be named by generated code";
                return NameFault.Unnameable;
            }

            return NameFault.None;
        }

        /// <summary>
        /// <see cref="ClassifyTypeName"/> plus the one restriction that applies only to a type a <b>model</b> value
        /// travels in: a ref struct cannot be boxed, and every model reaches the generated entry point as an
        /// <c>object</c> (CS1503, CS0457). Restricted types — <c>TypedReference</c>, <c>ArgIterator</c>,
        /// <c>RuntimeArgumentHandle</c> — are ref-like and land here too.
        /// <para>A ref struct is still perfectly writable, which is why the split exists: a hop <em>through</em> one
        /// to a member of its own spells <c>default(Span&lt;char&gt;)</c> and compiles.</para>
        /// </summary>
        public NameFault ClassifyModelType(ITypeSymbol type, out string reason)
        {
            var fault = ClassifyTypeName(type, out reason);
            if (fault != NameFault.None)
                return fault;

            if (type != null && type.IsRefLikeType)
                return Unusable(type, "is a ref struct and cannot be boxed into a model", out reason);

            return NameFault.None;
        }

        private static NameFault Unusable(ITypeSymbol type, string what, out string reason)
        {
            reason = "'" + FullyQualified(type) + "' " + what;
            return NameFault.Unusable;
        }

        /// <summary>Whether a type parameter appears anywhere in the spelling — as the type itself, an array or
        /// pointer element, a type argument, or a type argument of an enclosing type (<c>List`1.Enumerator</c> carries
        /// no type arguments of its own but still cannot be written).</summary>
        private static bool ContainsTypeParameter(ITypeSymbol type)
        {
            switch (type)
            {
                case null:
                    return false;
                case ITypeParameterSymbol _:
                    return true;
                case IArrayTypeSymbol array:
                    return ContainsTypeParameter(array.ElementType);
                case IPointerTypeSymbol pointer:
                    return ContainsTypeParameter(pointer.PointedAtType);
                case INamedTypeSymbol named:
                    foreach (var argument in named.TypeArguments)
                        if (ContainsTypeParameter(argument))
                            return true;
                    return ContainsTypeParameter(named.ContainingType);
                default:
                    return false;
            }
        }

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
        /// <para><b>Called only where a diagnostic is about to be written.</b> It costs a whole second compilation,
        /// and the walk that produces the miss runs on every typed hop of every template — including from the
        /// operand-type estimator, which reports nothing, and for receivers whose misses are suppressed outright.
        /// Asking here rather than there is the difference between one probe per reported member and one per
        /// keystroke over a half-typed name.</para>
        /// </summary>
        internal bool HiddenByAccessibility(ITypeSymbol receiver, string name)
        {
            if (receiver == null || _compilation == null)
                return false;
            foreach (var location in receiver.Locations)
                if (location.IsInSource)
                    return false;

            foreach (var mapped in MapToFullMetadataView(receiver))
            {
                if (MemberPathWalk.TryFind(SymbolMemberModel.Instance, mapped, name, out _))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The same references opened with <see cref="MetadataImportOptions.All"/> — as close to reflection's view,
        /// and therefore the engine's, as the symbol model gets. Built per compilation and only on the path that is
        /// about to report a member failure, which is rare; held weakly against the compilation it describes so it
        /// dies with it rather than accumulating one per keystroke in the IDE.
        /// </summary>
        private static readonly ConditionalWeakTable<Compilation, Compilation> FullMetadataViews =
            new ConditionalWeakTable<Compilation, Compilation>();

        /// <summary>
        /// The candidates for <paramref name="type"/> in the full-metadata view — every one of them, because the
        /// singular lookup answers null when a name appears in more than one reference. Two assemblies carrying the
        /// same type name is ordinary in a large reference closure, and treating it as "not found" put the member
        /// failure back at error severity over a template the engine renders, which is the fault this probe exists
        /// to prevent. Ambiguity is a reason to be careful about which type is meant, not a reason to conclude the
        /// member is a typo: if any candidate declares it, it degrades.
        /// </summary>
        private ImmutableArray<INamedTypeSymbol> MapToFullMetadataView(ITypeSymbol type)
        {
            // Arrays, pointers and type parameters carry no user members to be hidden.
            if (!(type is INamedTypeSymbol named))
                return ImmutableArray<INamedTypeSymbol>.Empty;

            var metadataName = MetadataNameOf(named);
            if (metadataName == null)
                return ImmutableArray<INamedTypeSymbol>.Empty;

            var view = FullMetadataViews.GetValue(_compilation, compilation =>
                CSharpCompilation.Create("HeddleMetadataProbe", null, compilation.References,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                        metadataImportOptions: MetadataImportOptions.All)));

            return view.GetTypesByMetadataName(metadataName);
        }

        /// <summary>The CLR name <see cref="Compilation.GetTypesByMetadataName"/> reads — nested types joined by
        /// <c>+</c>, generic arity as a backtick suffix, which <see cref="ISymbol.MetadataName"/> already carries on
        /// a constructed type as well as on its definition.</summary>
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
