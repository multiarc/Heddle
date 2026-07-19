using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The emitter's symbol-metadata member resolver — the <see cref="ISymbol"/> counterpart of the runtime's
    /// reflection-based <c>MemberPathResolver</c> (phase 7; milestone 1 uses it to <b>type</b> member hops so the
    /// emitter picks the right null-safety form, not to report diagnostics). Same posture as the runtime member
    /// tier: properties only, case-sensitive, readable, getter public-or-internal, not <c>[Hidden]</c>.
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

        /// <summary>Resolves a model type name against the compilation and the template's <c>@using</c> namespaces.
        /// Handles fully-qualified metadata names and simple names resolved through the usings; returns null on
        /// failure (the caller then treats the typed construct as unsupported rather than emit wrong code).</summary>
        private static readonly Dictionary<string, SpecialType> Keywords = new Dictionary<string, SpecialType>
        {
            ["bool"] = SpecialType.System_Boolean, ["byte"] = SpecialType.System_Byte,
            ["sbyte"] = SpecialType.System_SByte, ["char"] = SpecialType.System_Char,
            ["decimal"] = SpecialType.System_Decimal, ["double"] = SpecialType.System_Double,
            ["float"] = SpecialType.System_Single, ["int"] = SpecialType.System_Int32,
            ["uint"] = SpecialType.System_UInt32, ["long"] = SpecialType.System_Int64,
            ["ulong"] = SpecialType.System_UInt64, ["short"] = SpecialType.System_Int16,
            ["ushort"] = SpecialType.System_UInt16, ["object"] = SpecialType.System_Object,
            ["string"] = SpecialType.System_String,
        };

        public ITypeSymbol ResolveModelType(string text, IReadOnlyList<string> usings)
        {
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

            // C# keyword aliases (string/bool/int/…): the runtime's ReflectionHelper accepts them.
            if (Keywords.TryGetValue(text, out var special))
                return _compilation.GetSpecialType(special);

            var direct = _compilation.GetTypeByMetadataName(text);
            if (direct != null)
                return direct;

            if (usings != null)
            {
                foreach (var ns in usings)
                {
                    var candidate = _compilation.GetTypeByMetadataName(ns + "." + text);
                    if (candidate != null)
                        return candidate;
                }
            }

            // Common namespaces implicitly available in the runtime resolver.
            foreach (var ns in new[] { "System", "System.Collections.Generic" })
            {
                var candidate = _compilation.GetTypeByMetadataName(ns + "." + text);
                if (candidate != null)
                    return candidate;
            }

            return null;
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

        private static IPropertySymbol FindProperty(ITypeSymbol type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                foreach (var member in t.GetMembers(name))
                {
                    if (member is IPropertySymbol prop && IsAccessible(prop))
                        return prop;
                }

                if (t.TypeKind == TypeKind.Interface)
                    break;
            }

            if (type.TypeKind == TypeKind.Interface)
            {
                foreach (var iface in type.AllInterfaces)
                {
                    foreach (var member in iface.GetMembers(name))
                    {
                        if (member is IPropertySymbol prop && IsAccessible(prop))
                            return prop;
                    }
                }
            }

            return null;
        }

        private static bool IsAccessible(IPropertySymbol prop)
        {
            if (prop.GetMethod == null)
                return false;
            var acc = prop.GetMethod.DeclaredAccessibility;
            if (acc != Accessibility.Public && acc != Accessibility.Internal &&
                acc != Accessibility.ProtectedOrInternal)
                return false;
            foreach (var attr in prop.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "HiddenAttribute")
                    return false;
            }

            return true;
        }

        public static bool IsNonNullableValueType(ITypeSymbol type)
        {
            if (type == null || !type.IsValueType)
                return false;
            if (type is INamedTypeSymbol named &&
                named.ConstructedFrom?.SpecialType == SpecialType.System_Nullable_T)
                return false;
            return true;
        }

        public static string FullyQualified(ITypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
