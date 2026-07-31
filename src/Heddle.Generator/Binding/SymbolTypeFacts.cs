using Heddle.Language.Binding;
using Heddle.Language.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// The Roslyn adapter of <see cref="ITypeFacts{TType}"/>. The verified Roslyn-vs-CLR disagreements — over the
    /// nullable domain, and over array elements the CLR reduces to one type — are corrected here; the shared
    /// conformance corpus enforces adapter fidelity.
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
        /// plus the corrections where classification and the CLR relation disagree.
        /// <list type="number">
        /// <item><description><c>int → int?</c> is CLR-assignable (the CLR's special <c>Nullable&lt;T&gt;</c>
        /// treatment) but classified <c>ImplicitNullable</c> — neither reference nor boxing — so a naive
        /// classification-based adapter answers false. Corrected toward the CLR.</description></item>
        /// <item><description>Boxing <b>out of</b> a <c>Nullable&lt;T&gt;</c> classifies against the boxed
        /// <c>T</c>, which reaches <c>T</c>'s interfaces and, for an enum, <c>System.Enum</c>. The CLR relates
        /// <c>Nullable&lt;T&gt;</c> itself, whose own hierarchy is <c>ValueType</c> and <c>object</c> and nothing
        /// more. Corrected toward the CLR.</description></item>
        /// <item><description>Array covariance over element types the CLR reduces to one — an enum with its
        /// underlying primitive, and each signed/unsigned integer pair — is no conversion at all to Roslyn, so a
        /// classification-based adapter refuses <c>uint[] → int[]</c>, which the CLR admits.</description></item>
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

            // Implicit boxing. A Nullable<T> source boxes to T's box, which is what the classification follows, but
            // the CLR relation is over Nullable<T> itself — whose own hierarchy is ValueType and object and nothing
            // else. So `int? -> IComparable` is false, and so is `DayOfWeek? -> Enum`.
            if (conv.IsImplicit && conv.IsBoxing)
                return !TryGetNullableUnderlying(source, out _) || HierarchyAssignable(target, source);

            // Array covariance over element types the CLR treats as one, which Roslyn classifies as no conversion
            // at all. Reducing both sides and re-asking also answers the forms this propagates through: an array
            // interface (`uint[] -> IList<int>`) and a jagged array, whose element type is itself an array.
            var reducedSource = ReduceArrayElements(source);
            var reducedTarget = ReduceArrayElements(target);
            if (!SymbolEqualityComparer.Default.Equals(reducedSource, source) ||
                !SymbolEqualityComparer.Default.Equals(reducedTarget, target))
                return IsAssignableFrom(reducedTarget, reducedSource);

            // The other half of that reduction: reducing the arrays alone answers `uint[] -> IList<int>`, where the
            // source is what reduces, and leaves `int[] -> IList<uint>` refused, where the interface's own argument
            // is. Only an array asks this — `IList<int> -> IList<uint>` is not a conversion the CLR makes, so the
            // argument reduces against an array source and nowhere else, and the re-ask still has to find the
            // interface on the array before this admits anything.
            if (source is IArrayTypeSymbol && target is INamedTypeSymbol constructed &&
                constructed.TypeKind == TypeKind.Interface && constructed.TypeArguments.Length == 1)
            {
                var argument = constructed.TypeArguments[0];
                var reducedArgument = ReduceElement(ReduceArrayElements(argument));
                if (!SymbolEqualityComparer.Default.Equals(reducedArgument, argument))
                    return IsAssignableFrom(constructed.ConstructedFrom.Construct(reducedArgument), source);
            }

            return false;
        }

        /// <summary>The same array with every element type replaced by its CLR array-element representative, or the
        /// type unchanged where nothing reduces (so the caller can tell, and the re-ask terminates).</summary>
        private ITypeSymbol ReduceArrayElements(ITypeSymbol type)
        {
            if (!(type is IArrayTypeSymbol array))
                return type;

            var element = ReduceElement(ReduceArrayElements(array.ElementType));
            return SymbolEqualityComparer.Default.Equals(element, array.ElementType)
                ? type
                : _compilation.CreateArrayTypeSymbol(element, array.Rank);
        }

        /// <summary>An enum stands for its underlying primitive, and each signed/unsigned integer pair for one
        /// representative of the pair — the pointer-width pair included, which the C# numeric-conversion table this
        /// otherwise reads from does not name. <c>char</c> and <c>bool</c> stand for nothing: the CLR refuses
        /// <c>char[] -> ushort[]</c> and <c>bool[] -> byte[]</c> though the widths agree.</summary>
        private ITypeSymbol ReduceElement(ITypeSymbol element)
        {
            if (element is INamedTypeSymbol named && named.EnumUnderlyingType != null)
                element = named.EnumUnderlyingType;

            if (element.SpecialType == SpecialType.System_UIntPtr)
                return _compilation.GetSpecialType(SpecialType.System_IntPtr);

            var kind = SymbolFacts.ToNumericKind(element.SpecialType);
            NumericKind reduced;
            switch (kind)
            {
                case NumericKind.Byte: reduced = NumericKind.SByte; break;
                case NumericKind.UInt16: reduced = NumericKind.Int16; break;
                case NumericKind.UInt32: reduced = NumericKind.Int32; break;
                case NumericKind.UInt64: reduced = NumericKind.Int64; break;
                default: return element;
            }

            return _compilation.GetSpecialType(SymbolFacts.ToSpecialType(reduced));
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
