using Heddle.Language.Binding;
using Heddle.Language.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The Roslyn adapter of <see cref="ITypeFacts{TType}"/>. Two verified Roslyn-vs-CLR disagreements over
    /// nullable domain assignability are corrected here; the shared conformance corpus enforces adapter fidelity.
    /// </summary>
    internal sealed class SymbolTypeFacts : ITypeFacts<ITypeSymbol>
    {
        private readonly Compilation _compilation;
        private readonly CSharpCompilation _csharp;

        public SymbolTypeFacts(Compilation compilation)
        {
            _compilation = compilation;
            _csharp = compilation as CSharpCompilation;
        }

        /// <summary>
        /// Reflection's <c>target.IsAssignableFrom(source)</c> over symbols: Roslyn's conversion classification
        /// plus two explicit <c>Nullable&lt;T&gt;</c> corrections, because classification and the CLR relation
        /// disagree over the nullable domain in <b>both</b> directions.
        /// <list type="number">
        /// <item><description><c>int → int?</c> is CLR-assignable (the CLR's special <c>Nullable&lt;T&gt;</c>
        /// treatment) but classified <c>ImplicitNullable</c> — neither reference nor boxing — so a naive
        /// classification-based adapter answers false. Corrected toward the CLR.</description></item>
        /// <item><description><c>int? → IComparable</c> classifies as boxing (the boxed underlying value does
        /// implement the interface) but <c>typeof(IComparable).IsAssignableFrom(typeof(int?))</c> is false:
        /// <c>Nullable&lt;T&gt;</c> itself implements no interfaces. Corrected toward the CLR.</description></item>
        /// </list>
        /// <para>Without a C# compilation (a non-C# host) the relation degrades to the nominal hierarchy walk,
        /// which is sound for the named-type cases every caller in this phase asks about.</para>
        /// </summary>
        public bool IsAssignableFrom(ITypeSymbol target, ITypeSymbol source)
        {
            if (target == null || source == null)
                return false;
            if (SymbolEqualityComparer.Default.Equals(target, source))
                return true;

            if (_csharp == null)
                return HierarchyAssignable(target, source);

            if (TryGetNullableUnderlying(target, out var targetUnderlying) &&
                SymbolEqualityComparer.Default.Equals(targetUnderlying, source))
                return true;

            var conv = _csharp.ClassifyConversion(source, target);

            if (conv.IsIdentity)
                return true;
            if (conv.IsImplicit && conv.IsReference)
                return true;

            // Implicit boxing, except Nullable<T> to interface.
            bool sourceNullable = TryGetNullableUnderlying(source, out _);
            if (conv.IsImplicit && conv.IsBoxing && !(sourceNullable && target.TypeKind == TypeKind.Interface))
                return true;

            return false;
        }

        /// <summary>The nominal hierarchy walk — base chain plus the transitive interface set. Compilation-free,
        /// so the extension-precedence rule (which only ever compares two named class symbols) can use it even
        /// where <c>ClassifyConversion</c> is unavailable.</summary>
        internal static bool HierarchyAssignable(ITypeSymbol target, ITypeSymbol source)
        {
            if (target == null || source == null)
                return false;
            if (SymbolEqualityComparer.Default.Equals(target, source))
                return true;

            for (var t = source.BaseType; t != null; t = t.BaseType)
                if (SymbolEqualityComparer.Default.Equals(t, target))
                    return true;

            foreach (var iface in source.AllInterfaces)
                if (SymbolEqualityComparer.Default.Equals(iface, target))
                    return true;

            return false;
        }

        public bool TryGetNullableUnderlying(ITypeSymbol type, out ITypeSymbol underlying)
        {
            // OriginalDefinition, not ConstructedFrom: the two agree for every Nullable<T> a template can name,
            // but unified here to use one spelling.
            if (type is INamedTypeSymbol named &&
                named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T &&
                named.TypeArguments.Length == 1)
            {
                underlying = named.TypeArguments[0];
                return true;
            }

            underlying = null;
            return false;
        }

        public bool IsInterface(ITypeSymbol type) => type != null && type.TypeKind == TypeKind.Interface;

        public bool IsValueType(ITypeSymbol type) => type != null && type.IsValueType;

        /// <summary>False for null, error types, pointers, or types containing generic parameters.</summary>
        public bool IsUsableAsPropType(ITypeSymbol type)
        {
            if (type == null || type is IErrorTypeSymbol)
                return false;
            if (type.TypeKind == TypeKind.Pointer || type.TypeKind == TypeKind.FunctionPointer)
                return false;
            return !ContainsTypeParameter(type);
        }

        /// <summary><c>Type.ContainsGenericParameters</c> over symbols: the type <em>is</em> a type parameter, is an
        /// unbound generic definition, or has one anywhere in its type arguments / element type.</summary>
        private static bool ContainsTypeParameter(ITypeSymbol type)
        {
            switch (type.TypeKind)
            {
                case TypeKind.TypeParameter:
                    return true;
                case TypeKind.Array:
                    return ContainsTypeParameter(((IArrayTypeSymbol) type).ElementType);
            }

            if (type is INamedTypeSymbol named)
            {
                if (named.IsUnboundGenericType)
                    return true;
                foreach (var argument in named.TypeArguments)
                    if (ContainsTypeParameter(argument))
                        return true;
                if (named.ContainingType != null)
                    return ContainsTypeParameter(named.ContainingType);
            }

            return false;
        }

        public NumericKind GetNumericKind(ITypeSymbol type) =>
            type == null ? NumericKind.None : SymbolFacts.ToNumericKind(type.SpecialType);

        public bool IsObject(ITypeSymbol type) => type != null && type.SpecialType == SpecialType.System_Object;

        public string FormatAqn(ITypeSymbol type) =>
            SymbolTypeIdentity.AqnSansVersion(type as INamedTypeSymbol);

        /// <summary>Type format for diagnostic messages: not fully qualified (no special-type aliases) so error
        /// messages match the dynamic tier's reflection-based spelling.</summary>
        private static readonly SymbolDisplayFormat DiagnosticFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
            .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        public string Display(ITypeSymbol type) =>
            type == null ? "<unknown>" : type.ToDisplayString(DiagnosticFormat);

        /// <summary>The compilation this adapter answers against — callers that must construct a symbol (the
        /// nullable lift, for instance) need it and should not carry a second reference.</summary>
        internal Compilation Compilation => _compilation;
    }
}
