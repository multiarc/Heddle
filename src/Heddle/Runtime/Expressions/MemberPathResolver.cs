using System;
using System.Diagnostics.CodeAnalysis;
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
    /// Reflection adapter for member walk. <see cref="BaseInterfaces"/> returns nothing (surfacing them would be a breaking change),
    /// and non-public base members stay invisible per <see cref="MemberVisibility"/>.
    /// </summary>
    internal sealed class ReflectionTypeModel : ITypeModel<Type, PropertyInfo>
    {
        public static readonly ReflectionTypeModel Instance = new ReflectionTypeModel();

        private ReflectionTypeModel() { }

        public bool IsDynamic(Type type) => false;   // dynamic-ness is an ExType fact, decided before the walk

        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection over a model type; model types reach the engine through [HeddleModelAssembly]/typeof parameters annotated DynamicallyAccessedMemberTypes.All, which keeps their members through a trimmed publish.")]
        public IEnumerable<PropertyInfo> DeclaredProperties(Type type, string name)
        {
            foreach (var property in type.GetProperties(MemberPathResolver.DeclaredBindingFlags))
            {
                if (string.Equals(property.Name, name, StringComparison.Ordinal))
                    yield return property;
            }
        }

        public bool DeclaresNonPropertyMember(Type type, string name) =>
            MemberPathResolver.DeclaresNonPropertyMember(type, name);

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
        /// type. Distinct by name, and the most-derived declaration of a name decides — a name a derived type
        /// re-declares as hidden, non-public, static or as a non-property member is not offered from its base.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection over a model type; model types reach the engine through [HeddleModelAssembly]/typeof parameters annotated DynamicallyAccessedMemberTypes.All, which keeps their members through a trimmed publish.")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Reflection over a model type; model types reach the engine through [HeddleModelAssembly]/typeof parameters annotated DynamicallyAccessedMemberTypes.All, which keeps their members through a trimmed publish.")]
        internal static IEnumerable<PropertyInfo> GetVisibleProperties(Type type)
        {
            if (type == null)
                yield break;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var offered = new HashSet<string>(StringComparer.Ordinal);
            bool declaredOnReceiver = true;
            for (var current = type; current != null; current = current.BaseType)
            {
                foreach (var property in current.GetProperties(DeclaredBindingFlags))
                {
                    if (seen.Contains(property.Name))
                        continue;
                    if (!MemberVisibility.IsAccessible(FactsOf(property), declaredOnReceiver))
                        continue;
                    if (offered.Add(property.Name))
                        yield return property;
                }

                foreach (var member in current.GetMembers(DeclaredBindingFlags))
                    seen.Add(member.Name);
                declaredOnReceiver = false;
            }
        }

        /// <summary>Whether <paramref name="type"/> itself declares a field, method, event or nested type named
        /// <paramref name="name"/>.</summary>
        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Reflection over a model type; model types reach the engine through [HeddleModelAssembly]/typeof parameters annotated DynamicallyAccessedMemberTypes.All, which keeps their members through a trimmed publish.")]
        internal static bool DeclaresNonPropertyMember(Type type, string name)
        {
            foreach (var member in type.GetMember(name, DeclaredBindingFlags))
            {
                if (member.MemberType != MemberTypes.Property)
                    return true;
            }

            return false;
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

            return new MemberFacts(true, AccessOf(getter), HasHidden(property), getter.IsStatic);
        }

        private static readonly string HiddenAttributeFullName = typeof(HiddenAttribute).FullName;

        /// <summary>Matched by full metadata name rather than <see cref="Type"/> identity. A model assembly
        /// loaded into its own load context can bind its attribute to another copy of this assembly; an
        /// identity lookup then finds nothing and the member is exposed. The name match fails closed, and a
        /// foreign <c>*.HiddenAttribute</c> in any other namespace still hides nothing.</summary>
        private static bool HasHidden(PropertyInfo property)
        {
            try
            {
                foreach (var attribute in property.GetCustomAttributesData())
                {
                    if (string.Equals(attribute.AttributeType.FullName, HiddenAttributeFullName,
                            StringComparison.Ordinal))
                        return true;
                }

                return false;
            }
            catch (Exception ex) when (ex is TypeLoadException || ex is MissingMemberException ||
                ex is System.IO.IOException || ex is BadImageFormatException)
            {
                // One attribute whose type or constructor cannot load — a model built against another version
                // of something — and the runtime lists none of the member's attributes. The member stays
                // hidden unless its metadata, read without loading anything, names no hidden attribute.
                return !ProvablyCarriesNoHidden(property);
            }
        }

        private static bool ProvablyCarriesNoHidden(PropertyInfo property)
        {
#if NET8_0_OR_GREATER
            try
            {
                unsafe
                {
                    if (!System.Reflection.Metadata.AssemblyExtensions.TryGetRawMetadata(
                            property.Module.Assembly, out byte* blob, out int length))
                        return false;
                    var reader = new System.Reflection.Metadata.MetadataReader(blob, length);
                    var handle = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(property.MetadataToken);
                    if (handle.Kind != System.Reflection.Metadata.HandleKind.PropertyDefinition)
                        return false;
                    var definition = reader.GetPropertyDefinition(
                        (System.Reflection.Metadata.PropertyDefinitionHandle) handle);
                    foreach (var attributeHandle in definition.GetCustomAttributes())
                    {
                        var constructor = reader.GetCustomAttribute(attributeHandle).Constructor;
                        System.Reflection.Metadata.EntityHandle declaring;
                        if (constructor.Kind == System.Reflection.Metadata.HandleKind.MemberReference)
                            declaring = reader.GetMemberReference(
                                (System.Reflection.Metadata.MemberReferenceHandle) constructor).Parent;
                        else if (constructor.Kind == System.Reflection.Metadata.HandleKind.MethodDefinition)
                            declaring = reader.GetMethodDefinition(
                                (System.Reflection.Metadata.MethodDefinitionHandle) constructor).GetDeclaringType();
                        else
                            return false;

                        string fullName;
                        if (declaring.Kind == System.Reflection.Metadata.HandleKind.TypeReference)
                        {
                            var type = reader.GetTypeReference((System.Reflection.Metadata.TypeReferenceHandle) declaring);
                            fullName = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
                        }
                        else if (declaring.Kind == System.Reflection.Metadata.HandleKind.TypeDefinition)
                        {
                            var type = reader.GetTypeDefinition((System.Reflection.Metadata.TypeDefinitionHandle) declaring);
                            fullName = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
                        }
                        else
                        {
                            return false;
                        }

                        if (string.Equals(fullName, HiddenAttributeFullName, StringComparison.Ordinal))
                            return false;
                    }

                    return true;
                }
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is InvalidOperationException ||
                ex is NotSupportedException || ex is ArgumentException)
            {
                return false;
            }
#else
            return false;
#endif
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
