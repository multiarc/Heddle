using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Helpers;

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

    internal static class MemberPathResolver
    {
        internal const BindingFlags MemberBindingFlags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

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

                var dataProperty = currentType.Type.GetProperty(segments[i], MemberBindingFlags);
                if (!IsAccessible(dataProperty))
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
        /// <see cref="TryResolve"/> applies (phase 6 D3; feeds LSP member completion). Returns nothing for a null
        /// type. Distinct by name (a hidden/derived duplicate collapses to the most-derived accessible one).
        /// </summary>
        internal static IEnumerable<PropertyInfo> GetVisibleProperties(Type type)
        {
            if (type == null)
                yield break;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in type.GetProperties(MemberBindingFlags))
            {
                if (!IsAccessible(property))
                    continue;
                if (seen.Add(property.Name))
                    yield return property;
            }
        }

        /// <summary>The exact member-tier property filter: readable, not <c>[Hidden]</c>, getter assembly/public.</summary>
        internal static bool IsAccessible(PropertyInfo property)
        {
            if (property == null || !property.CanRead)
                return false;
            if (property.GetCustomAttribute<HiddenAttribute>(false) != null)
                return false;
            var getter = property.GetGetMethod(true);
            return getter != null && (getter.IsAssembly || getter.IsPublic);
        }
    }
}
