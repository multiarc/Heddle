using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Language.Members;

namespace Heddle.Runtime.Expressions
{
    internal enum MemberPathResolutionKind
    {
        Resolved,
        DynamicHop,
        Failed
    }

    /// <summary>
    /// The single source of member-path resolution semantics, shared by the member tier
    /// (<c>HeddleCompiler.CompileModelAccessor</c>) and the native-expression tier. Same
    /// <see cref="BindingFlags"/>, same <c>CanRead</c>/<c>[Hidden]</c>/getter-visibility filter, same
    /// <c>Property {name} not found in Type [{type}]</c> message (surfaced as HED0001).
    /// </summary>
    internal sealed class MemberPathResolution
    {
        private MemberPathResolution(MemberPathResolutionKind kind, List<(Type, PropertyInfo)> properties,
            ExType resultType, int index, string failureMessage)
        {
            Kind = kind;
            Properties = properties;
            ResultType = resultType;
            Index = index;
            FailureMessage = failureMessage;
        }

        public MemberPathResolutionKind Kind { get; }

        /// <summary>The resolved property chain (a prefix, for <see cref="MemberPathResolutionKind.DynamicHop"/>).</summary>
        public List<(Type, PropertyInfo)> Properties { get; }

        /// <summary>Final property type for <see cref="MemberPathResolutionKind.Resolved"/>.</summary>
        public ExType ResultType { get; }

        /// <summary>Dynamic-hop segment index, or the failing-segment index.</summary>
        public int Index { get; }

        /// <summary>The HED0001 message text for <see cref="MemberPathResolutionKind.Failed"/>.</summary>
        public string FailureMessage { get; }

        public static MemberPathResolution Resolved(List<(Type, PropertyInfo)> properties, ExType resultType) =>
            new MemberPathResolution(MemberPathResolutionKind.Resolved, properties, resultType, -1, null);

        public static MemberPathResolution DynamicHop(List<(Type, PropertyInfo)> prefix, int index) =>
            new MemberPathResolution(MemberPathResolutionKind.DynamicHop, prefix, null, index, null);

        public static MemberPathResolution Failed(int index, string message) =>
            new MemberPathResolution(MemberPathResolutionKind.Failed, null, null, index, message);
    }

    /// <summary>
    /// The reflection fact source for the shared member walk. Roslyn's mirror of this adapter lives in
    /// the generator; both feed the identical <see cref="MemberVisibility"/> policy, so the six divergences
    /// verified between the two hand-written resolvers cannot re-open.
    /// <para>Two capability choices are deliberate and runtime-normative: <see cref="BaseInterfaces"/>
    /// returns nothing (reflection's <c>GetProperty</c> never searched base interfaces, so surfacing them would be a
    /// behavior <i>widening</i> — a breaking-window candidate, not a drift fix), and non-public members declared on a
    /// base class stay invisible, which <see cref="MemberVisibility"/> encodes through its
    /// <c>declaredOnReceiver</c> rule.</para>
    /// </summary>
    internal sealed class ReflectionTypeModel : ITypeModel<Type, PropertyInfo>
    {
        public static readonly ReflectionTypeModel Instance = new ReflectionTypeModel();

        private ReflectionTypeModel() { }

        public bool IsDynamic(Type type) => false;   // dynamic-ness is an ExType fact, decided before the walk

        public IEnumerable<PropertyInfo> DeclaredProperties(Type type, string name)
        {
            foreach (var property in type.GetProperties(MemberPathResolver.DeclaredBindingFlags))
            {
                if (string.Equals(property.Name, name, StringComparison.Ordinal))
                    yield return property;
            }
        }

        public MemberFacts FactsOf(PropertyInfo member) => MemberPathResolver.FactsOf(member);

        public Type TypeOf(PropertyInfo member) => member.PropertyType;

        public Type BaseOf(Type type) => type.BaseType;

        public bool IsInterface(Type type) => type.IsInterface;

        public IEnumerable<Type> BaseInterfaces(Type type) => Array.Empty<Type>();
    }

    internal static class MemberPathResolver
    {
        internal const BindingFlags MemberBindingFlags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>The same set restricted to one type's own declarations — the shared walk visits the base chain
        /// itself, most-derived-first, so a <c>new</c>-shadowed property resolves deterministically instead of
        /// throwing <c>AmbiguousMatchException</c> out of <c>Type.GetProperty</c>.</summary>
        internal const BindingFlags DeclaredBindingFlags = MemberBindingFlags | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Walks <paramref name="segments"/> off <paramref name="startType"/>, applying the member-tier filter
        /// verbatim. Returns a resolved chain, a dynamic-hop split point, or a positioned-message failure.
        /// </summary>
        internal static MemberPathResolution TryResolve(ExType startType, string[] segments)
        {
            var properties = new List<(Type, PropertyInfo)>(segments.Length);
            var currentType = startType;
            for (int i = 0; i < segments.Length; i++)
            {
                if (currentType.IsDynamic)
                    return MemberPathResolution.DynamicHop(properties, i);

                if (!MemberPathWalk.TryFind(ReflectionTypeModel.Instance, currentType.Type, segments[i],
                        out var dataProperty))
                {
                    return MemberPathResolution.Failed(i,
                        $"Property {segments[i]} not found in Type [{currentType}]");
                }

                properties.Add((currentType.Type, dataProperty));
                currentType = dataProperty.GetPropertyExType();
            }

            if (properties.Count == 0)
                return MemberPathResolution.Failed(0, "Empty member path");

            var last = properties[properties.Count - 1].Item2;
            return MemberPathResolution.Resolved(properties, last.GetPropertyExType());
        }

        /// <summary>
        /// Every visible property of <paramref name="type"/> under the <b>identical</b> member-tier filter
        /// <see cref="TryResolve"/> applies (feeds LSP member completion). Returns nothing for a null
        /// type. Distinct by name (a hidden/derived duplicate collapses to the most-derived accessible one).
        /// </summary>
        internal static IEnumerable<PropertyInfo> GetVisibleProperties(Type type)
        {
            if (type == null)
                yield break;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool declaredOnReceiver = true;
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var property in current.GetProperties(DeclaredBindingFlags))
                {
                    if (!MemberVisibility.IsAccessible(FactsOf(property), declaredOnReceiver))
                        continue;
                    if (seen.Add(property.Name))
                        yield return property;
                }

                declaredOnReceiver = false;
            }
        }

        /// <summary>The exact member-tier property filter: readable, not <c>[Hidden]</c>, instance, getter
        /// assembly/public. Kept as a <see cref="PropertyInfo"/>-shaped entry point for the callers that already
        /// hold a resolved property (prop-shadow detection, indexer selection).</summary>
        internal static bool IsAccessible(PropertyInfo property)
        {
            return property != null && MemberVisibility.IsAccessible(FactsOf(property));
        }

        /// <summary>The reflection → <see cref="MemberFacts"/> adapter.</summary>
        internal static MemberFacts FactsOf(PropertyInfo property)
        {
            if (property == null || !property.CanRead)
                return new MemberFacts(false, MemberAccess.Private, false, false);

            var getter = property.GetGetMethod(true);
            if (getter == null)
                return new MemberFacts(false, MemberAccess.Private, false, false);

            bool hidden = property.GetCustomAttribute<HiddenAttribute>(false) != null;
            return new MemberFacts(true, AccessOf(getter), hidden, getter.IsStatic);
        }

        private static MemberAccess AccessOf(MethodBase getter)
        {
            if (getter.IsPublic)
                return MemberAccess.Public;
            if (getter.IsFamilyOrAssembly)
                return MemberAccess.ProtectedOrInternal;
            if (getter.IsFamilyAndAssembly)
                return MemberAccess.ProtectedAndInternal;
            if (getter.IsAssembly)
                return MemberAccess.Internal;
            if (getter.IsFamily)
                return MemberAccess.Protected;
            return MemberAccess.Private;
        }
    }
}
