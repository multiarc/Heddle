using System.Collections.Generic;
using Heddle.Language.Binding;
using Heddle.Strings.Core;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Binding
{
    /// <summary>
    /// Discovers declaratively exported host functions: the assembly-level
    /// <c>Heddle.Attributes.ExportFunctionsAttribute</c> on the compilation's own assembly and its referenced
    /// assemblies. Each container is a <c>public static</c> class; every <b>eligible</b> public static method is one
    /// function named <c>MethodInfo.Name.ToLowerInvariant()</c>. Generated calls bind <b>directly</b>
    /// to the discovered container (no shim, no runtime registry), and the manifest records the container as AQN
    /// sans version — the exact shape the gauntlet compares against the live registry.
    /// <para>Three rules live in the shared
    /// <see cref="ExportRules"/>/<see cref="ExportBookkeeping{TPayload}"/> core rather than being transcribed
    /// here by hand:</para>
    /// <list type="bullet">
    /// <item><description><b>method eligibility</b> — the runtime refuses open generics, <c>void</c> returns and
    /// <c>ref</c>/<c>out</c>/pointer parameters; this resolver used to count them, and because the gauntlet compares
    /// overload counts exactly in both directions, one <c>void Log(string)</c> helper permanently un-precompiled
    /// every template calling any function from that container;</description></item>
    /// <item><description><b>merge across containers</b> — a second container exporting the same name adds its
    /// overloads rather than being ignored, so the manifest rows match the live merged registry;</description></item>
    /// <item><description><b>container eligibility</b> — an ineligible container is <c>HED7021</c> at
    /// <b>Error</b> severity, not a silent skip: the runtime throws
    /// <c>ArgumentException</c> at <c>RegisterFrom</c>, so the build fails the same way instead of masking a host
    /// configuration error until first render.</description></item>
    /// </list>
    /// </summary>
    internal sealed class FunctionExportResolver
    {
        /// <summary>One discovered overload: the container it lives in and the metadata a call site needs to emit
        /// (and to rank) it.</summary>
        internal sealed class ExportOverloadInfo
        {
            public ExportOverloadInfo(INamedTypeSymbol container, IMethodSymbol method)
            {
                Container = container;
                Method = method;
            }

            public INamedTypeSymbol Container { get; }

            public IMethodSymbol Method { get; }

            /// <summary><c>global::</c>-qualified container type name for the generated call site.</summary>
            public string ContainerGlobalName =>
                Container.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            /// <summary>The concrete (cased) method name to emit.</summary>
            public string MethodName => Method.Name;
        }

        internal sealed class ExportEntry
        {
            public ExportEntry(IReadOnlyList<ExportOverloadInfo> overloads,
                IReadOnlyList<(string Aqn, int OverloadCount)> manifestRows)
            {
                Overloads = overloads;
                ManifestRows = manifestRows;
            }

            /// <summary>Every overload registered under this function name, across every container that exports it —
            /// the merged set the runtime's registry holds.</summary>
            public IReadOnlyList<ExportOverloadInfo> Overloads { get; }

            /// <summary>One manifest <c>FunctionBindings</c> row per contributing container, with that container's
            /// overload count. The gauntlet compares each row's count exactly.</summary>
            public IReadOnlyList<(string Aqn, int OverloadCount)> ManifestRows { get; }

            /// <summary>Convenience for the single-container case the emitter's older call shape assumes.</summary>
            public string ContainerAqnSansVersion => ManifestRows.Count == 1 ? ManifestRows[0].Aqn : null;

            public int OverloadCount => ManifestRows.Count == 1 ? ManifestRows[0].OverloadCount : 0;
        }

        /// <summary>An ineligible <c>[ExportFunctions]</c> container, for the <c>HED7021</c> error.</summary>
        internal readonly struct IneligibleContainer
        {
            public IneligibleContainer(string display, string reason)
            {
                Display = display;
                Reason = reason;
            }

            public string Display { get; }
            public string Reason { get; }
        }

        private readonly Dictionary<string, ExportEntry> _byFunctionName;
        private readonly IReadOnlyList<IneligibleContainer> _ineligible;

        private FunctionExportResolver(Dictionary<string, ExportEntry> byFunctionName,
            IReadOnlyList<IneligibleContainer> ineligible)
        {
            _byFunctionName = byFunctionName;
            _ineligible = ineligible;
        }

        public bool TryGet(string functionName, out ExportEntry entry) =>
            _byFunctionName.TryGetValue(functionName, out entry);

        public bool Any => _byFunctionName.Count != 0;

        /// <summary>Containers the runtime would reject with <c>ArgumentException</c> — each one <c>HED7021</c>.</summary>
        public IReadOnlyList<IneligibleContainer> IneligibleContainers => _ineligible;

        public static FunctionExportResolver Build(Compilation compilation)
        {
            var byName = new Dictionary<string, ExportEntry>(System.StringComparer.Ordinal);
            var ineligible = new List<IneligibleContainer>();
            if (compilation == null)
                return new FunctionExportResolver(byName, ineligible);

            var exportAttr = compilation.GetTypeByMetadataName("Heddle.Attributes.ExportFunctionsAttribute");
            if (exportAttr == null)
                return new FunctionExportResolver(byName, ineligible);

            var assemblies = new List<IAssemblySymbol> { compilation.Assembly };
            assemblies.AddRange(compilation.SourceModule.ReferencedAssemblySymbols);

            var bookkeeping = new ExportBookkeeping<ExportOverloadInfo>();

            foreach (var assembly in assemblies)
            {
                foreach (var attr in assembly.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, exportAttr))
                        continue;

                    foreach (var container in ContainerTypes(attr))
                        AddContainer(container, bookkeeping, ineligible);
                }
            }

            foreach (var name in bookkeeping.Names)
            {
                var overloads = new List<ExportOverloadInfo>();
                foreach (var overload in bookkeeping.Overloads(name))
                    overloads.Add(overload.Payload);

                var rows = new List<(string, int)>();
                foreach (var container in bookkeeping.Containers(name))
                    rows.Add((container, bookkeeping.OverloadCount(name, container)));

                byName[name] = new ExportEntry(overloads, rows);
            }

            return new FunctionExportResolver(byName, ineligible);
        }

        private static IEnumerable<INamedTypeSymbol> ContainerTypes(AttributeData attr)
        {
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol single)
                {
                    yield return single;
                }
                else if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var item in arg.Values)
                        if (item.Kind == TypedConstantKind.Type && item.Value is INamedTypeSymbol many)
                            yield return many;
                }
            }
        }

        private static void AddContainer(INamedTypeSymbol container,
            ExportBookkeeping<ExportOverloadInfo> bookkeeping, List<IneligibleContainer> ineligible)
        {
            if (container == null)
                return;

            bool isStaticClass = container.TypeKind == TypeKind.Class && container.IsStatic;
            bool isPublic = IsPubliclyVisible(container);
            if (!ExportRules.IsContainerEligible(isStaticClass, isPublic))
            {
                var display = SymbolTypeIdentity.FullName(container);
                ineligible.Add(new IneligibleContainer(display,
                    ExportRules.ContainerIneligibleMessage(display)));
                return;
            }

            var containerAqn = SymbolTypeIdentity.AqnSansVersion(container);
            foreach (var member in container.GetMembers())
            {
                if (!(member is IMethodSymbol method))
                    continue;

                var facts = DescribeMethod(method);
                if (!ExportRules.IsCandidate(facts))
                    continue;

                var rejection = ExportRules.Evaluate(facts);
                if (rejection != ExportRejection.None)
                {
                    ineligible.Add(new IneligibleContainer(SymbolTypeIdentity.FullName(container),
                        ExportRules.MethodIneligibleMessage(SymbolTypeIdentity.FullName(container), method.Name,
                            rejection)));
                    continue;
                }

                bookkeeping.AddOrReplace(ExportRules.FunctionName(method.Name),
                    new ExportOverload<ExportOverloadInfo>
                    {
                        ContainerAqn = containerAqn,
                        ParameterTypeKeys = facts.ParameterTypeKeys,
                        Payload = new ExportOverloadInfo(container, method)
                    });
            }
        }

        private static bool IsPubliclyVisible(INamedTypeSymbol type)
        {
            for (var t = type; t != null; t = t.ContainingType)
                if (t.DeclaredAccessibility != Accessibility.Public)
                    return false;
            return true;
        }

        /// <summary>
        /// The Roslyn adapter of <see cref="ExportedMethodFacts"/>.
        /// <para><b>The <c>MethodKind.Ordinary</c> ↔ <c>!IsSpecialName</c> correspondence is stated here, once.</b>
        /// Reflection skips <c>IsSpecialName</c> methods — property accessors, event accessors, operators,
        /// constructors. Roslyn's <c>MethodKind</c> partitions the same set: everything <em>except</em>
        /// <c>Ordinary</c> (and <c>DeclareMethod</c>, a VB concept) is special-name in metadata. The residual
        /// difference runs the other way — a C# method can carry <c>[SpecialName]</c> explicitly and still be
        /// <c>MethodKind.Ordinary</c>. That case would make the build tier count a method the runtime skips, so it
        /// is checked explicitly rather than inferred from the kind.</para>
        /// <para>Parameter-type keys use a fully-qualified, non-aliased display. They are only ever compared
        /// <em>within</em> this tier (each tier merges its own registrations), so consistency is what matters, not
        /// byte-equality with reflection's <c>Type.FullName</c>.</para>
        /// </summary>
        private static ExportedMethodFacts DescribeMethod(IMethodSymbol method)
        {
            var keys = new string[method.Parameters.Length];
            bool byRefOrPointer = false;
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                if (parameter.RefKind != RefKind.None || parameter.Type.TypeKind == TypeKind.Pointer ||
                    parameter.Type.TypeKind == TypeKind.FunctionPointer)
                    byRefOrPointer = true;
                keys[i] = SignatureKey(parameter.Type);
            }

            bool specialName = method.MethodKind != MethodKind.Ordinary ||
                               HasSpecialNameAttribute(method);

            return new ExportedMethodFacts
            {
                Name = method.Name,
                IsStatic = method.IsStatic,
                IsPublic = method.DeclaredAccessibility == Accessibility.Public,
                IsOpenGeneric = method.IsGenericMethod,
                ReturnsVoid = method.ReturnsVoid,
                HasByRefOrPointerParameter = byRefOrPointer,
                IsSpecialName = specialName,
                ParameterTypeKeys = keys
            };
        }

        private static bool HasSpecialNameAttribute(IMethodSymbol method)
        {
            foreach (var attribute in method.GetAttributes())
                if (attribute.AttributeClass?.MetadataName == "SpecialNameAttribute")
                    return true;
            return false;
        }

        private static readonly SymbolDisplayFormat SignatureFormat = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
            .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

        internal static string SignatureKey(ITypeSymbol type) => type.ToDisplayString(SignatureFormat);

        /// <summary>The build-time position an <c>HED7021</c> is reported at is the template whose compilation
        /// consulted the resolver; the resolver itself is template-independent, so the pipeline supplies it.</summary>
        internal static BlockPosition NoPosition => default;
    }
}
