using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Heddle.Language.Binding;
using Heddle.Language.Members;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        private readonly CSharpParseOptions _parseOptions;

        public SymbolTypeResolver(Compilation compilation)
        {
            _compilation = compilation;
            _parseOptions = ProbeParseOptions.For(compilation);
        }

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

            /// <summary>Why an <see cref="PathKind.Inaccessible"/> hop was refused, which decides whether anyone
            /// reports it. <see cref="NameFault.Unnameable"/> is a name this assembly is merely not allowed to
            /// mention — the author can act on that, and HED7030 says so. <see cref="NameFault.Unusable"/> is a
            /// property of the type itself with no remedy but a different model, so naming the member in a warning
            /// would be pointing at code that is not wrong.</summary>
            public NameFault Fault;

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

            // No `?` prelude here, deliberately. The runtime resolves a type spelling through the same shared
            // grammar and that grammar has no nullable suffix, so `@model(){{int?}}` is a template the engine will
            // not compile at all — on any of its paths. Lifting it to `Nullable<int>` on this side alone bound a
            // strategy for it and rendered it, which is the one outcome the two tiers must never differ on. The
            // `::`, prop and slot positions never see a `?`: the grammar takes their type name as an identifier and
            // the suffix is not part of one.
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
                if (!(definition is INamedTypeSymbol named))
                    return false;

                // Reflection counts a nested type's generic arguments including the enclosing types' parameters,
                // so the spelling's argument total is matched against the whole containment chain — the nested
                // symbol's own Arity alone refused `Outer<int>.Inner`, a spelling the engine binds.
                var chain = new List<INamedTypeSymbol>();
                for (var link = named; link != null; link = link.ContainingType)
                    chain.Add(link);
                chain.Reverse();

                var total = 0;
                foreach (var link in chain)
                    total += link.Arity;
                if (total != arguments.Count)
                    return false;

                var next = 0;
                INamedTypeSymbol current = null;
                foreach (var link in chain)
                {
                    var target = current == null ? link.ConstructedFrom : FindNested(current, link);
                    if (target == null)
                        return false;
                    if (link.Arity > 0)
                    {
                        var slice = new ITypeSymbol[link.Arity];
                        for (int i = 0; i < link.Arity; i++)
                            slice[i] = arguments[next++];
                        target = target.Construct(slice);
                    }

                    current = target;
                }

                constructed = current;
                return true;
            }

            /// <summary>The <paramref name="definition"/>'s counterpart inside a (possibly constructed)
            /// <paramref name="outer"/>, so the nested symbol's containing type carries the outer's arguments.</summary>
            private static INamedTypeSymbol FindNested(INamedTypeSymbol outer, INamedTypeSymbol definition)
            {
                foreach (var member in outer.GetTypeMembers(definition.Name, definition.Arity))
                    return member;
                return null;
            }

            public bool TryGetValueTupleDefinition(int arity, out ITypeSymbol definition)
            {
                definition = _compilation.GetTypeByMetadataName("System.ValueTuple`" + arity);
                return definition != null;
            }
        }

        private readonly Dictionary<string, bool> _usingBodies = new Dictionary<string, bool>(System.StringComparer.Ordinal);

        /// <summary>
        /// Whether <paramref name="text"/> is a <c>@using</c> body that becomes a working C# <c>using</c> directive
        /// here.
        /// <para>The engine writes every collected body into <c>using &lt;body&gt;;</c> in the unit it compiles for
        /// an embedded expression, so the question is not "is this a namespace" but "does that directive compile" —
        /// and the compiler is asked it rather than a hand-written name walk answering something narrower. A walk
        /// over dotted segments said no to <c>using static System.Math;</c> and to a using-alias, both of which the
        /// engine compiles, and said yes to a body with a trailing <c>//</c> comment, which swallows the semicolon
        /// and stops the consumer's build.</para>
        /// <para>The directive is compiled in this compilation, whose references are the ones the generated file
        /// will be compiled against, so a namespace nothing here declares is refused for the same reason the engine
        /// refuses it: <c>CS0246</c>.</para>
        /// </summary>
        public bool UsingDirectiveCompiles(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || _compilation == null)
                return false;
            if (_usingBodies.TryGetValue(text, out var memoized))
                return memoized;

            var resolved = UsingDirectiveCompilesCore(text);
            _usingBodies[text] = resolved;
            return resolved;
        }

        private bool UsingDirectiveCompilesCore(string text)
        {
            var source = "using " + text + ";";
            var tree = CSharpSyntaxTree.ParseText(source, _parseOptions);
            foreach (var diagnostic in tree.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    return false;
            }

            // One directive and nothing else. A body carrying its own `;` parses cleanly and declares whatever
            // follows it into the consumer's assembly, which a `@using` has no business doing; a trailing `//`
            // comment swallows the semicolon and brings an invented one back in its place.
            var root = tree.GetCompilationUnitRoot();
            if (root.Usings.Count != 1 || root.Members.Count != 0 || root.ToFullString() != source)
                return false;
            foreach (var token in root.DescendantTokens())
            {
                if (token.IsMissing)
                    return false;
            }

            var probe = _compilation.AddSyntaxTrees(tree);
            foreach (var diagnostic in probe.GetSemanticModel(tree, false).GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Whether any type in the compilation or its references answers to <paramref name="text"/> — the last gate
        /// before HED7007 calls a <c>@model</c> spelling a typo, so it has to be generous about the ways the runtime
        /// finds a type and exact about the ways it does not.
        /// <para>The spelling is matched against fully-qualified names as a <b>dot-bounded suffix</b>. That keeps the
        /// bare-name case generous — <c>Article</c> matches <c>Ns.Article</c>, because the runtime resolves an
        /// unqualified name by scanning what is loaded, and an import the emitter did not model would make it bind —
        /// while a spelling that wrote namespace segments has to be right about them. Matching on the last segment
        /// alone said yes to <c>Nope.Nope.Article</c> off an unrelated <c>Article</c>, and the raw spelling went on
        /// to become the entry point's parameter type: four CS0246 against a <c>.g.cs</c>, and not one diagnostic of
        /// Heddle's own. A trailing <c>?</c> is not stripped either — no tier's grammar has one, so a spelling that
        /// carries it answers to nothing and should be told so.</para>
        /// </summary>
        public bool TypeNameExistsAnywhere(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            if (Keywords.ContainsKey(text))
                return true;

            if (NamespaceContainsType(_compilation.GlobalNamespace, text))
                return true;
            foreach (var reference in _compilation.References)
            {
                if (_compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol asm &&
                    NamespaceContainsType(asm.GlobalNamespace, text))
                    return true;
            }

            return false;
        }

        private static bool NamespaceContainsType(INamespaceSymbol ns, string spelling)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (IsDotBoundedSuffix(ns, type.Name, spelling))
                    return true;
            }

            foreach (var child in ns.GetNamespaceMembers())
            {
                if (NamespaceContainsType(child, spelling))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether <paramref name="spelling"/> is a dot-bounded suffix of <paramref name="typeName"/> qualified by
        /// <paramref name="ns"/>, matched segment by segment from the right rather than against a built-up string.
        /// The walk visits every type in the whole reference closure on a path that runs per keystroke in an editor,
        /// and composing the qualified name to compare it allocated two strings per type visited.
        /// </summary>
        private static bool IsDotBoundedSuffix(INamespaceSymbol ns, string typeName, string spelling)
        {
            int end = spelling.Length;
            if (!ConsumeSegment(spelling, ref end, typeName))
                return false;

            for (var current = ns; ; current = current.ContainingNamespace)
            {
                if (end == 0)
                    return true;
                if (current == null || current.IsGlobalNamespace || spelling[end - 1] != '.')
                    return false;
                end--;
                if (!ConsumeSegment(spelling, ref end, current.Name))
                    return false;
            }
        }

        /// <summary>Takes <paramref name="name"/> off the end of <paramref name="spelling"/>, moving
        /// <paramref name="end"/> back past it, or leaves both alone and answers false.</summary>
        private static bool ConsumeSegment(string spelling, ref int end, string name)
        {
            if (end < name.Length ||
                string.CompareOrdinal(spelling, end - name.Length, name, 0, name.Length) != 0)
                return false;
            end -= name.Length;
            return true;
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
                // The two are carried apart rather than collapsed into one boolean, because only the first is
                // something the template's author can do anything about and only the first is worth a warning
                // against their .heddle file.
                var fault = !IsAccessibleFromCompilation(prop) || !IsAccessibleFromCompilation(prop.GetMethod) ||
                            IsObsoleteError(prop) || IsObsoleteError(prop.GetMethod)
                    ? NameFault.Unnameable
                    : ClassifyTypeName(prop.Type, out _);
                if (fault != NameFault.None)
                {
                    result.Kind = PathKind.Inaccessible;
                    result.Fault = fault;
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

        /// <summary>Whether <paramref name="name"/> binds to a readable, visible property of
        /// <paramref name="type"/> — asked through the same shared walk that resolves a member path, so the
        /// build tier's answer to "does this prop hide a model member" is the walk's answer, not a second one.</summary>
        internal static bool BindsReadableProperty(ITypeSymbol type, string name) =>
            type != null && type.TypeKind != TypeKind.Dynamic && FindProperty(type, name) != null;

        /// <summary>The shared visibility facts of one property symbol — the same adapter the member walk feeds
        /// <c>MemberVisibility</c>, exposed for the indexer filter so both ask the one policy.</summary>
        internal static MemberFacts PropertyFacts(IPropertySymbol member) =>
            SymbolMemberModel.Instance.FactsOf(member);

        /// <summary>
        /// Whether <paramref name="symbol"/> carries <c>[Obsolete(…, error: true)]</c> — the attribute form that
        /// makes every mention of the name a compile <b>error</b> (CS0619/CS0672) rather than a warning.
        /// <para>Only the error form counts. Reflection ignores <c>[Obsolete]</c> altogether, so both forms render
        /// on the engine; degrading on the warning form as well would take every deprecated-but-working model off
        /// the precompiled tier, which is a cost with nothing on the other side of it. The generated file already
        /// opens with a blanket <c>#pragma warning disable</c>, so the warning form raises nothing in the consumer's
        /// build against code they did not write, which is the only thing that had to be true for leaving it.</para>
        /// <para>The only nesting asked about here is the <b>containing</b> type's own attribute:
        /// <c>Legacy.Inner</c> cannot be spelled without spelling <c>Legacy</c>, and C# reports the error on the
        /// outer name. Array elements, pointed-at types and type arguments — the enclosing types' arguments
        /// included — are walked by <see cref="Classify"/>, which reaches this predicate once per component, so an
        /// arm for them here would be a second copy of that walk, agreeing with it until the day it did not.</para>
        /// </summary>
        public static bool IsObsoleteError(ISymbol symbol)
        {
            if (symbol == null)
                return false;

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
            if (symbol is INamedTypeSymbol named && IsObsoleteError(named.ContainingType))
                return true;

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
            /// the only remedy is a different model. Degrading hands the template to the engine, which serves it
            /// where it can — though not always: a ref struct in a boxing position fails the modern-TFM engine too,
            /// at its compile, with HED0005 (its expression trees reject by-ref-like types wholesale), so there the
            /// degrade preserves the engine's own refusal rather than a render.</summary>
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
        /// code has no generic context to bind them in (CS0246/CS0305); VB modules and script submissions, which C#
        /// has no syntax for; <c>System.Void</c> (CS1536, CS1547); a static class (CS0721/CS0723); and an anonymous
        /// type, which has no writable name at all. An unbound <c>List&lt;&gt;</c> is refused too, but as an error
        /// type rather than an open one: Roslyn fills its arguments with <see cref="TypeKind.Error"/> symbols, so it
        /// is the first row that answers for it and not the type-parameter one.
        /// Restricted types — <c>TypedReference</c> and friends — need no row of their own: they are
        /// <see cref="ITypeSymbol.IsRefLikeType"/> and so are caught as ref structs.</para>
        /// <para>The question is asked of the whole spelling, not its head: an array element and a type argument are
        /// positions a value lives in too, so <c>List&lt;System.Void&gt;</c> and <c>System.Math[]</c> are refused on
        /// what is inside them. See <see cref="Classify"/> for the one verdict that differs by position.</para>
        /// </summary>
        public NameFault ClassifyTypeName(ITypeSymbol type, out string reason) =>
            Classify(type, refStructAllowed: true, out reason);

        /// <summary>
        /// <see cref="ClassifyTypeName"/> plus the one restriction that applies only to a type a <b>model</b> value
        /// travels in: a ref struct cannot be boxed, and every model reaches the generated entry point as an
        /// <c>object</c> (CS1503, CS0457). Restricted types — <c>TypedReference</c>, <c>ArgIterator</c>,
        /// <c>RuntimeArgumentHandle</c> — are ref-like and land here too.
        /// <para>A ref struct is still perfectly writable, which is why the split exists: a hop <em>through</em> one
        /// to a member of its own spells <c>default(Span&lt;char&gt;)</c> and compiles.</para>
        /// </summary>
        public NameFault ClassifyModelType(ITypeSymbol type, out string reason) =>
            Classify(type, refStructAllowed: false, out reason);

        /// <summary>
        /// Why a <see cref="TypeKind"/> can never be written into generated C#, or null where the kind itself is no
        /// obstacle and the rest of the walk decides.
        /// <para>The kinds are answered here, apart from the symbol, so the table can be asked about every one of
        /// them. Two — <see cref="TypeKind.Unknown"/> and <see cref="TypeKind.Module"/> — have no C# compilation
        /// that produces a symbol carrying them, so a walk taking symbols could only ever leave those labels
        /// asserted by nothing; grouped with a neighbour, deleting one changed no answer at all.</para>
        /// </summary>
        internal static string UnnameableKind(TypeKind kind)
        {
            switch (kind)
            {
                case TypeKind.Error:
                case TypeKind.Unknown:
                    return "does not resolve to a type this compilation can name";
                case TypeKind.Pointer:
                case TypeKind.FunctionPointer:
                    return "is a pointer type, which generated code cannot name outside an unsafe context";
                case TypeKind.TypeParameter:
                    return "is a type parameter, and generated code has no generic context to bind it in";
                case TypeKind.Module:
                case TypeKind.Submission:
                    return "is not a type C# has a syntax for";
                default:
                    return null;
            }
        }

        /// <summary>
        /// The one walk behind both, with the single verdict that varies by <b>position</b> as its parameter.
        /// <para><paramref name="refStructAllowed"/> is true exactly where the spelling only has to be a name the
        /// consumer's compiler accepts — the outermost type of a member hop, whose value stays in a local. It is
        /// false wherever the value has to go somewhere a ref struct may not: boxed into a model, stored in an array
        /// element (CS0611), or substituted for a type argument (CS9244). That is why the recursion flips it: the
        /// element type of <c>Span&lt;char&gt;[]</c> and the type argument of <c>List&lt;Span&lt;char&gt;&gt;</c> are
        /// both refused although <c>Span&lt;char&gt;</c> on its own is not.</para>
        /// <para>Every other verdict holds in every position and so recurses unchanged: <c>void</c>, a static class,
        /// a pointer and a type parameter are as unwritable inside a spelling as they are at the head of one, and
        /// C# reports all of them on the outer name — <c>List&lt;System.Math&gt;</c> is CS0718 where the emitter
        /// wrote it, not somewhere the author can see.</para>
        /// </summary>
        private NameFault Classify(ITypeSymbol type, bool refStructAllowed, out string reason)
        {
            reason = null;
            if (type == null)
                return NameFault.None;

            // The kind's own verdict first, and for every kind including Array. Ordered after the array
            // short-circuit instead, no answer the table gives about an array could ever be reached, and the
            // verdict test's Array row was silently a second copy of the row for its element's kind.
            var kindReason = UnnameableKind(type.TypeKind);
            if (kindReason != null)
                return Unusable(type, kindReason, out reason);

            // An array the table allows is exactly as writable as its element type, and an element type has to be
            // able to hold a value too — `int*[]` and `Math[]` are both rejected on the element, not the brackets.
            if (type.TypeKind == TypeKind.Array)
                return Classify(((IArrayTypeSymbol) type).ElementType, refStructAllowed: false, out reason);

            if (type.SpecialType == SpecialType.System_Void)
                return Unusable(type, "is 'void', which cannot be a parameter, a local or a cast target", out reason);

            if (type.IsStatic)
                return Unusable(type, "is a static type and cannot hold a value", out reason);

            if (type.IsAnonymousType)
                return Unusable(type, "is an anonymous type and has no name to write", out reason);

            if (type is INamedTypeSymbol named)
            {
                if (ContainsTypeParameter(named))
                    return Unusable(type, "is an open generic type, and generated code has no generic context to " +
                                          "bind its type parameters in", out reason);

                // Every type argument the spelling carries, the enclosing types' included. `Outer<Legacy>.Inner`
                // has none of its own and still cannot be written without writing `Legacy`, so asking only
                // `named.TypeArguments` let an error-obsolete argument through: the emitter spelled the property's
                // type into a `default(T)` and the consumer's build died on CS0619, twice, off a property carrying
                // no attribute of its own.
                for (var spelled = named; spelled != null; spelled = spelled.ContainingType)
                {
                    foreach (var argument in spelled.TypeArguments)
                    {
                        var argumentFault = Classify(argument, refStructAllowed: false, out reason);
                        if (argumentFault != NameFault.None)
                            return argumentFault;
                    }
                }
            }

            if (!IsAccessibleFromCompilation(type) || IsObsoleteError(type))
            {
                reason = "'" + FullyQualified(type) + "' cannot be named by generated code";
                return NameFault.Unnameable;
            }

            if (!refStructAllowed && IsRefLikeOrRestricted(type))
                return Unusable(type, "is a ref struct: it cannot be boxed into a model, be an array element, or " +
                                      "stand as a type argument", out reason);

            return NameFault.None;
        }

        /// <summary>Ref-struct-ness as the CONSUMER'S COMPILER decides it, not as the metadata spells it. The
        /// classic restricted trio predates <c>IsByRefLikeAttribute</c>: .NET Framework's mscorlib carries no
        /// marking on them, so <see cref="ITypeSymbol.IsRefLikeType"/> answers false there while csc still
        /// hardcodes the refusal — generated code boxing one fails the consumer's build (CS1503). Matched by
        /// name for exactly that reason.</summary>
        public static bool IsRefLikeOrRestricted(ITypeSymbol type)
        {
            if (type == null)
                return false;
            if (type.IsRefLikeType)
                return true;
            if (type.TypeKind != TypeKind.Struct)
                return false;

            var ns = type.ContainingNamespace;
            if (ns == null || ns.Name != "System" || ns.ContainingNamespace?.IsGlobalNamespace != true)
                return false;
            return type.Name == "TypedReference" || type.Name == "ArgIterator" ||
                   type.Name == "RuntimeArgumentHandle";
        }

        private static NameFault Unusable(ITypeSymbol type, string what, out string reason)
        {
            reason = "'" + FullyQualified(type) + "' " + what;
            return NameFault.Unusable;
        }

        /// <summary>Whether a type parameter stands as a type argument of the spelling or of an enclosing type
        /// (<c>List`1.Enumerator</c> carries no type arguments of its own but still cannot be written). It answers
        /// only for a type argument that <em>is</em> the parameter: one merely wrapped in an array or a pointer —
        /// <c>Outer&lt;T[]&gt;.Inner</c> — is left to <see cref="Classify"/>, which walks every type argument in the
        /// spelling and turns the parameter down on its own kind. Adding arms for those here would be a second walk
        /// that only changes which sentence the refusal carries.</summary>
        private static bool ContainsTypeParameter(ITypeSymbol type)
        {
            switch (type)
            {
                case null:
                    return false;
                case ITypeParameterSymbol _:
                    return true;
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

#if ROSLYN_4_11_OR_GREATER
            return view.GetTypesByMetadataName(metadataName);
#else
            // Compilation.GetTypesByMetadataName exists only from Roslyn 4.2; this is the 4.1 floor variant —
            // the one every net48 compiler host loads — so the same query is spelled as a per-assembly walk.
            var candidates = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            foreach (var reference in view.References)
            {
                if (!(view.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly))
                    continue;
                var candidate = assembly.GetTypeByMetadataName(metadataName);
                if (candidate != null)
                    candidates.Add(candidate);
            }

            return candidates.ToImmutable();
#endif
        }

        /// <summary>The CLR name the full-metadata probe reads — nested types joined by
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
        /// Whether a resolved path ends on a ref struct. What that costs depends on the <b>sink</b> the value lands
        /// in, not on the type alone (<c>TemplateEmitter.RefStructUse</c>): a boxing sink — an extension's model
        /// value, a slot value, an expression operand, a function argument — cannot take one, and emitting the path
        /// there put <c>CS0030</c> into the consumer's <em>build</em>, so those sinks degrade with a reason naming
        /// the sink. The engine cannot produce the value on any modern TFM either: its expression trees reject
        /// by-ref-like types wholesale, so it refuses the same template at its compile with <c>HED0005</c> — a
        /// diagnostic the host can catch — while the .NET Framework engine, whose <c>System.Memory</c> spans carry
        /// no by-ref-like marking, boxes and renders.
        /// <para>The rendered sink needs no box at all: the carrier's protocol is
        /// <c>value is string s ? s : value.ToString()</c>, so the emitter stringifies the value in place and
        /// precompiles — the functioning tier's bytes, on every TFM.</para>
        /// <para>A ref struct passed <em>through</em> to a member of its own — <c>Buf.Length</c> — is a different
        /// matter and stays precompiled in every sink: what is read there is the <c>int</c>.</para>
        /// </summary>
        public static bool EndsOnRefStruct(PathResolution resolution)
        {
            if (resolution == null || resolution.Hops.Count == 0)
                return false;
            return IsRefLikeOrRestricted(resolution.Hops[resolution.Hops.Count - 1].Property);
        }

        public static string FullyQualified(ITypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
