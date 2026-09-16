using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;

namespace Heddle.Precompiled
{
    /// <summary>The per-request validation gauntlet. Runs the pinned ordered checks against a
    /// resolved <see cref="PrecompiledTemplateInfo"/> and the request's effective <see cref="TemplateOptions"/>,
    /// returning the first failure as a <see cref="PrecompiledFallbackEvent"/> (with the pinned detail string) or
    /// <c>null</c> when every check passes. Pure apart from the optional staleness step's file reads.
    /// <para>Ordered: options → model type → extensions → bindings → functions → staleness. A row's
    /// presence in the entries is the precompiled fact; there is no marker step.</para></summary>
    internal static class PrecompiledGauntlet
    {
        internal const string Hed7101 = Data.HeddleDiagnosticIds.PrecompiledGauntletFallback;

        /// <param name="requestModelType">The model type the request would compile the template against — the
        /// requesting <see cref="Runtime.CompileContext.RootScopeType"/>'s <see cref="Type"/>. <c>null</c> means the
        /// caller makes no claim (a diagnostic pass rather than a request), and the model-type step is then skipped
        /// rather than guessed at.</param>
        internal static PrecompiledFallbackEvent? Validate(PrecompiledTemplateInfo entry, TemplateOptions options,
            Func<PrecompiledExtensionBinding, Type, bool> bindingResolver, Type requestModelType = null)
        {
            var optionsFailure = CheckOptions(entry, options);
            if (optionsFailure != null)
                return optionsFailure;

            var modelFailure = CheckModelType(entry, requestModelType);
            if (modelFailure != null)
                return modelFailure;

            var extensionFailure = CheckExtensions(entry, bindingResolver);
            if (extensionFailure != null)
                return extensionFailure;

            var bindingsFailure = CheckMemberBindings(entry);
            if (bindingsFailure != null)
                return bindingsFailure;

            var functionFailure = CheckFunctions(entry, options);
            if (functionFailure != null)
                return functionFailure;

            if (options.EnableFileChangeCheck)
            {
                var staleFailure = CheckStaleness(entry, options);
                if (staleFailure != null)
                    return staleFailure;
            }

            return null;
        }

        /// <summary>The typed-entry gauntlet: every <see cref="Validate"/> step, but the options
        /// step compares only <c>ExpressionMode</c> and <c>TrimDirectiveLines</c> — a typed entry
        /// renders its item's baked <c>OutputProfile</c> whatever the process default says.</summary>
        internal static PrecompiledFallbackEvent? ValidateTyped(PrecompiledTemplateInfo entry,
            TemplateOptions options,
            Func<PrecompiledExtensionBinding, Type, bool> bindingResolver, Type modelType)
        {
            var optionsFailure = CheckTypedOptions(entry, options);
            if (optionsFailure != null)
                return optionsFailure;

            var modelFailure = CheckModelType(entry, modelType);
            if (modelFailure != null)
                return modelFailure;

            var extensionFailure = CheckExtensions(entry, bindingResolver);
            if (extensionFailure != null)
                return extensionFailure;

            var bindingsFailure = CheckMemberBindings(entry);
            if (bindingsFailure != null)
                return bindingsFailure;

            var functionFailure = CheckFunctions(entry, options);
            if (functionFailure != null)
                return functionFailure;

            if (options.EnableFileChangeCheck)
            {
                var staleFailure = CheckStaleness(entry, options);
                if (staleFailure != null)
                    return staleFailure;
            }

            return null;
        }

        private static PrecompiledFallbackEvent? CheckTypedOptions(PrecompiledTemplateInfo entry,
            TemplateOptions options)
        {
            var fp = entry.OptionsFingerprint;
            if (fp.ExpressionMode != options.ExpressionMode)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch,
                    $"ExpressionMode: manifest={fp.ExpressionMode} request={options.ExpressionMode}");
            if (fp.TrimDirectiveLines != options.TrimDirectiveLines)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch,
                    $"TrimDirectiveLines: manifest={Lower(fp.TrimDirectiveLines)} request={Lower(options.TrimDirectiveLines)}");
            return null;
        }

        private static PrecompiledFallbackEvent? CheckOptions(PrecompiledTemplateInfo entry, TemplateOptions options)
        {
            var fp = entry.OptionsFingerprint;
            if (fp.Profile != options.OutputProfile)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch,
                    $"OutputProfile: manifest={fp.Profile} request={options.OutputProfile}");
            if (fp.ExpressionMode != options.ExpressionMode)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch,
                    $"ExpressionMode: manifest={fp.ExpressionMode} request={options.ExpressionMode}");
            if (fp.TrimDirectiveLines != options.TrimDirectiveLines)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch,
                    $"TrimDirectiveLines: manifest={Lower(fp.TrimDirectiveLines)} request={Lower(options.TrimDirectiveLines)}");
            return null;
        }

        /// <summary>The model-type step, and the one check the request's <see cref="TemplateOptions"/> cannot
        /// answer on their own.
        /// <para>An entry whose model type is <b>not</b> ambient carries a type its own <c>@model</c> directive
        /// pinned, and <c>ModelExtension.InitStart</c> pins the very same type on the dynamic tier whatever the host
        /// asked for — so the request's model type does not participate and there is nothing here to compare.</para>
        /// <para>An ambient entry is the opposite: the template names no model, so the build compiled it against its
        /// own assumption while the dynamic tier would compile it against
        /// <see cref="Runtime.CompileContext.RootScopeType"/>. Two different types there mean two different member
        /// resolutions — an <c>internal</c> member, a native expression's start type, a <c>@list</c> element — so the
        /// answer has to be identity rather than assignability. Assignability would let a derived request through on
        /// the strength of the cast the generated code performs, and the cast is not what decides the bytes: the
        /// typing done <em>before</em> it is.</para></summary>
        private static PrecompiledFallbackEvent? CheckModelType(PrecompiledTemplateInfo entry, Type requestModelType)
        {
            if (!entry.ModelTypeIsAmbient || requestModelType == null)
                return null;

            // ExType.Dynamic's Type is System.Object, which is also what the build assumes for an untyped template,
            // so a dynamic request and an untyped entry meet here as the same type rather than as two spellings.
            var compiled = entry.ModelType ?? typeof(object);
            if (compiled == requestModelType)
                return null;

            return Fail(entry.Key, PrecompiledFallbackReason.ModelTypeMismatch,
                $"Model: manifest={AqnSansVersion(compiled)} request={AqnSansVersion(requestModelType)}");
        }

        private static PrecompiledFallbackEvent? CheckExtensions(PrecompiledTemplateInfo entry,
            Func<PrecompiledExtensionBinding, Type, bool> bindingResolver)
        {
            string definitionCarrier = null;
            foreach (var binding in entry.ExtensionBindings)
            {
                // A definition call (in-document or composition-imported) records the definition
                // carrier, not a registry extension: it binds against the template's own declarations,
                // which ship inside the artifact and resolve from text at load. The extension registry
                // can neither provide nor contradict it, so there is nothing to compare.
                if (definitionCarrier == null)
                    definitionCarrier = AqnSansVersion(typeof(Heddle.Core.DefinitionBaseExtension));
                if (string.Equals(binding.ExtensionTypeName, definitionCarrier, StringComparison.Ordinal))
                    continue;
                if (!TemplateFactory.TryGetExtensionType(binding.Name, out var liveType))
                    return Fail(entry.Key, PrecompiledFallbackReason.ExtensionBindingMismatch,
                        $"Extension '{binding.Name}': manifest={binding.ExtensionTypeName} live=<unresolved>");

                var matches = bindingResolver != null
                    ? bindingResolver(binding, liveType)
                    : DefaultBindingMatch(binding, liveType);
                if (!matches)
                    return Fail(entry.Key, PrecompiledFallbackReason.ExtensionBindingMismatch,
                        $"Extension '{binding.Name}': manifest={binding.ExtensionTypeName} live={AqnSansVersion(liveType)}");

                // Identity match (same AQN) does not guarantee [Prop] slots remain unchanged; extension packages
                // can gain/reorder slots while keeping AQN, so validate the fingerprint.
                if (!string.IsNullOrEmpty(binding.PropLayoutFingerprint))
                {
                    var liveFingerprint = PropLayout.Fingerprint(liveType);
                    if (!string.Equals(binding.PropLayoutFingerprint, liveFingerprint, StringComparison.Ordinal))
                        return Fail(entry.Key, PrecompiledFallbackReason.ExtensionBindingMismatch,
                            $"Extension '{binding.Name}': prop layout manifest={binding.PropLayoutFingerprint} " +
                            $"live={liveFingerprint ?? "<none>"}");
                }
            }

            return null;
        }

        internal static bool DefaultBindingMatch(PrecompiledExtensionBinding binding, Type liveType)
        {
            return liveType != null &&
                   string.Equals(binding.ExtensionTypeName, AqnSansVersion(liveType), StringComparison.Ordinal);
        }

        /// <summary>The bindings step: the row's root model type was resolved by name at registration, and every
        /// recorded member row is walked from its own resolved start type through the live member graph, each hop
        /// compared by identity. Resolve-versus-compare order (AC-4): the root and start types resolve by name and
        /// may fail with a <c>Type</c> detail; everything reached through the member graph is compared and never
        /// resolved, so the verdict does not depend on which assemblies have loaded beyond the roots'. A hop the
        /// engine classifies dynamic carries no recorded identity and binds through the dynamic parameter.
        /// Entries with no recorded member rows (hand-written manifests) pass vacuously.</summary>
        private static PrecompiledFallbackEvent? CheckMemberBindings(PrecompiledTemplateInfo entry)
        {
            if (entry.ModelTypeUnresolved)
            {
                var modelRef = entry.ModelTypeRef;
                var nominal = modelRef != null ? modelRef.Nominal() : AqnFormatter.Unknown;
                var assembly = modelRef != null
                    ? PrecompiledTemplateInfo.AssemblyOf(modelRef)
                    : AqnFormatter.Unknown;
                return Fail(entry.Key, PrecompiledFallbackReason.MemberBindingMismatch,
                    "Type '" + nominal + "': manifest=" + assembly + " live=<unresolved>");
            }

            var rows = entry.MemberRows;
            if (rows == null)
                return null;
            for (int i = 0; i < rows.Count; i++)
            {
                var failure = CheckMemberRow(entry.Key, rows[i]);
                if (failure != null)
                    return failure;
            }

            return null;
        }

        private static PrecompiledFallbackEvent? CheckMemberRow(string key, CompiledMemberRow row)
        {
            if (row == null || row.Segments == null || row.Segments.Count == 0)
                return null;

            if (row.StartType == null)
                return null;

            ExType start;
            if (row.StartType is DynamicTypeRef)
            {
                start = ExType.Dynamic;
            }
            else
            {
                var resolved = PrecompiledTemplateInfo.FindLoadedType(row.StartType);
                if (resolved == null)
                    return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch,
                        "Type '" + row.StartType.Nominal() + "': manifest=" +
                        PrecompiledTemplateInfo.AssemblyOf(row.StartType) + " live=<unresolved>");
                start = new ExType(resolved);
            }

            var segments = new string[row.Segments.Count];
            for (int i = 0; i < segments.Length; i++)
                segments[i] = row.Segments[i] ?? string.Empty;
            var path = row.StartType.Nominal() + "." + string.Join(".", segments);

            var resolution = MemberPathResolver.TryResolve(start, segments);
            if (resolution == null || resolution.Kind == MemberPathResolutionKind.Failed)
            {
                var index = resolution != null ? resolution.Index : 0;
                return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch,
                    "Member '" + path + "': manifest=" + RecordedMemberNominal(row, index) +
                    " live=<unresolved>");
            }

            var hops = row.Hops;
            int hopCount = hops != null ? hops.Count : 0;
            var properties = resolution.Properties;
            if (resolution.Kind == MemberPathResolutionKind.DynamicHop)
            {
                int prefix = properties != null ? properties.Count : 0;
                for (int h = 0; h < prefix; h++)
                {
                    if (h >= hopCount)
                        break;
                    var detail = CompareHop(hops[h], path, segments[h],
                        properties[h].Item1, properties[h].Item2);
                    if (detail != null)
                        return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch, detail);
                }

                for (int h = prefix; h < hopCount; h++)
                {
                    if (HasRecordedIdentity(hops[h]))
                        return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch,
                            "Member '" + path + "': manifest=" + RecordedMemberNominal(row, h) +
                            " live=<dynamic>");
                }

                return null;
            }

            if (properties == null || properties.Count != segments.Length)
                return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch,
                    "Member '" + path + "': manifest=" +
                    RecordedMemberNominal(row, properties != null ? properties.Count : 0) +
                    " live=<unresolved>");

            for (int h = 0; h < segments.Length; h++)
            {
                if (h >= hopCount)
                    break;
                var detail = CompareHop(hops[h], path, segments[h],
                    properties[h].Item1, properties[h].Item2);
                if (detail != null)
                    return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch, detail);
            }

            return null;
        }

        /// <summary>Compares one recorded hop against the live walk's answer for the same segment: the recorded
        /// name must be the walked segment, the recorded declaring type must match the receiver in type-ref form,
        /// and the recorded member type must match the member's erased type in type-ref form. Returns the pinned
        /// detail on a difference, null when the hop binds. A null side carries no recorded identity and passes.
        /// Dynamic-ness rides the member's <c>DynamicAttribute</c>, which the build erases when it records the
        /// member type: a recorded concrete type matches a live member whose erased <c>PropertyType</c> is the
        /// same (the loader compiles against the live member, attribute and all), and a recorded dynamic matches
        /// a live member carrying the attribute.</summary>
        private static string CompareHop(CompiledMemberHop hop, string path,
            string segment, Type liveDeclaring, PropertyInfo liveProperty)
        {
            if (hop == null)
                return null;
            if (hop.MemberName != null && !string.Equals(hop.MemberName, segment, StringComparison.Ordinal))
                return "Member '" + path + "': manifest=" + NominalOrUnknown(hop.MemberType) +
                    " live=<unresolved>";
            if (hop.DeclaringType != null && !TypeRefMatches(hop.DeclaringType, liveDeclaring))
                return "Member '" + path + "': manifest=" + hop.DeclaringType.Nominal() +
                    " live=" + AqnSansVersion(liveDeclaring);
            if (hop.MemberType != null)
            {
                var recordedDynamic = hop.MemberType is DynamicTypeRef;
                Type liveErase = liveProperty != null ? liveProperty.PropertyType : null;
                bool liveDynamic = liveProperty != null &&
                    liveProperty.GetCustomAttribute<DynamicAttribute>() != null;
                if (recordedDynamic)
                {
                    if (!liveDynamic)
                        return "Member '" + path + "': manifest=<dynamic>" +
                            " live=" + (liveErase != null ? AqnSansVersion(liveErase) : AqnFormatter.Unknown);
                }
                else if (!TypeRefMatches(hop.MemberType, liveErase))
                {
                    return "Member '" + path + "': manifest=" + hop.MemberType.Nominal() +
                        " live=" + (liveErase != null ? AqnSansVersion(liveErase) : AqnFormatter.Unknown);
                }
            }

            return null;
        }

        private static bool HasRecordedIdentity(CompiledMemberHop hop) =>
            hop != null && (hop.DeclaringType != null || hop.MemberType != null);

        private static string RecordedMemberNominal(CompiledMemberRow row, int index)
        {
            if (row.Hops != null && index >= 0 && index < row.Hops.Count)
            {
                var hop = row.Hops[index];
                if (hop != null && hop.MemberType != null)
                    return hop.MemberType.Nominal();
            }

            return row.StartType != null ? row.StartType.Nominal() : AqnFormatter.Unknown;
        }

        private static string NominalOrUnknown(CompiledTypeRef typeRef) =>
            typeRef != null ? typeRef.Nominal() : AqnFormatter.Unknown;

        /// <summary>Structural type-ref comparison (AC-4): a named ref by full name and, for a non-framework
        /// assembly, the assembly simple name; a constructed generic by its definition and every argument
        /// recursively (<c>Nullable&lt;T&gt;</c> is the constructed generic it is); an array by element and rank.
        /// Never resolves by name and loads nothing.</summary>
        /// <summary>AC-4's type-ref comparison: a Named ref by full name and, for a non-framework ref, its
        /// assembly simple name (a framework ref's assembly name is advisory — System.Private.CoreLib at build,
        /// mscorlib on net48); a constructed generic by its definition and every argument; an array by element
        /// and rank. Shared by every place the engine compares a recorded identity to a live type.</summary>
        internal static bool TypeRefMatches(CompiledTypeRef recorded, Type live)
        {
            if (recorded == null || live == null)
                return recorded == null && live == null;
            if (recorded is DynamicTypeRef)
                return false;

            var named = recorded as NamedTypeRef;
            if (named != null)
                return NamedMatches(named, live);

            var generic = recorded as GenericTypeRef;
            if (generic != null)
            {
                if (!live.IsGenericType)
                    return false;
                Type definition;
                try
                {
                    definition = live.GetGenericTypeDefinition();
                }
                catch (Exception)
                {
                    return false;
                }

                if (!NamedMatches(generic.Definition, definition))
                    return false;
                Type[] arguments;
                try
                {
                    arguments = live.GetGenericArguments();
                }
                catch (Exception)
                {
                    return false;
                }

                if (arguments.Length != generic.Arguments.Count)
                    return false;
                for (int i = 0; i < arguments.Length; i++)
                    if (!TypeRefMatches(generic.Arguments[i], arguments[i]))
                        return false;
                return true;
            }

            var array = recorded as ArrayTypeRef;
            if (array != null)
            {
                if (!live.IsArray)
                    return false;
                int rank;
                Type element;
                try
                {
                    rank = live.GetArrayRank();
                    element = live.GetElementType();
                }
                catch (Exception)
                {
                    return false;
                }

                return rank == array.Rank && TypeRefMatches(array.Element, element);
            }

            return false;
        }

        private static bool NamedMatches(NamedTypeRef recorded, Type live)
        {
            if (recorded == null || live == null)
                return false;
            string liveFullName;
            try
            {
                liveFullName = live.FullName;
            }
            catch (Exception)
            {
                return false;
            }

            if (!string.Equals(recorded.FullName, liveFullName, StringComparison.Ordinal))
                return false;
            if (recorded.IsFramework)
                return true;
            string liveAssembly;
            try
            {
                liveAssembly = live.Assembly.GetName().Name;
            }
            catch (Exception)
            {
                return false;
            }

            return string.Equals(recorded.AssemblySimpleName, liveAssembly, StringComparison.Ordinal);
        }

        private static PrecompiledFallbackEvent? CheckFunctions(PrecompiledTemplateInfo entry, TemplateOptions options)
        {
            var rows = entry.FunctionBindings;
            if (rows.Count == 0)
                return null;

            // A request with no registry compiles against the frozen default set — and so does a late-bound site,
            // which is why a null-target row still has to be checked where the all-built-in shortcut used to
            // answer for the whole entry.
            var registry = options.Functions ?? FunctionRegistry.Default;
            var allBuiltIn = rows.All(r => r.TargetTypeName == DefaultFunctionTable.ShimTargetTypeName);

            if (options.Functions == null)
            {
                if (allBuiltIn)
                    return null;
                var exportRow = rows.FirstOrDefault(r =>
                    r.TargetTypeName != DefaultFunctionTable.ShimTargetTypeName && r.TargetTypeName != null);
                if (exportRow.TargetTypeName != null)
                    return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                        $"Function '{exportRow.Name}': manifest={exportRow.TargetTypeName} live=<missing>");
            }

            var names = new List<string>();
            foreach (var r in rows)
                if (!names.Contains(r.Name))
                    names.Add(r.Name);

            foreach (var name in names)
            {
                var recordedForName = rows.Where(r => r.Name == name && r.TargetTypeName != null).ToList();
                if (recordedForName.Count == 0)
                {
                    // A null-target row on a PRECOMPILED entry is a late-bound call site: the build knew the call
                    // shape but not the target, and the materialized site resolves it at first render through
                    // the engine's own ranker. Two render-time configurations are outside what that site can
                    // reproduce, and both are the dynamic tier's to answer, so they fall back here rather than at
                    // a render: a name that is a registered EXTENSION (whose render protocol is not a value), and
                    // a name registered nowhere (whose engine answer is a compile error, and a compile error the
                    // dynamic tier raises is the parity contract).
                    if (TemplateFactory.Exists(name))
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest=<late-bound> live=<extension>");
                    if (!registry.Contains(name))
                        return Fail(entry.Key, PrecompiledFallbackReason.UnsupportedFunction,
                            $"Function '{name}': manifest=<late-bound> live=<unregistered>");
                    continue;
                }
                var recordedTargets = new HashSet<string>(recordedForName.Select(r => r.TargetTypeName),
                    StringComparer.Ordinal);
                var manifestTarget = recordedForName[0].TargetTypeName;
                var live = registry.GetOverloads(name);

                foreach (var reg in live)
                {
                    if (reg.Method == null) // a delegate registration under a bound name
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest={manifestTarget} live=<delegate>");
                    var liveAqn = AqnSansVersion(reg.Method.DeclaringType);
                    if (!recordedTargets.Contains(liveAqn))
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest={manifestTarget} live={liveAqn}");
                }

                foreach (var row in recordedForName)
                {
                    var liveCount = live.Count(reg => reg.Method != null &&
                                                      string.Equals(AqnSansVersion(reg.Method.DeclaringType),
                                                          row.TargetTypeName, StringComparison.Ordinal));
                    if (liveCount > row.OverloadCount)
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest={row.TargetTypeName} live=<overloads added>");
                    if (liveCount < row.OverloadCount)
                        return Fail(entry.Key, PrecompiledFallbackReason.FunctionBindingMismatch,
                            $"Function '{name}': manifest={row.TargetTypeName} live=<missing>");
                }
            }

            return null;
        }

        private static PrecompiledFallbackEvent? CheckStaleness(PrecompiledTemplateInfo entry, TemplateOptions options)
        {
            var rootPath = options.RootPath ?? string.Empty;
            var contentPath = TemplateKey.ToPath(entry.Key, rootPath);
            if (!File.Exists(contentPath))
                return Fail(entry.Key, PrecompiledFallbackReason.StaleContent, $"Content: '{entry.Key}' missing");
            if (!string.Equals(HashFile(contentPath), entry.ContentHash, StringComparison.Ordinal))
                return Fail(entry.Key, PrecompiledFallbackReason.StaleContent, $"Content: '{entry.Key}' hash mismatch");

            foreach (var import in entry.Imports)
            {
                var importPath = TemplateKey.ToPath(import.Key, rootPath);
                if (!File.Exists(importPath))
                    return Fail(entry.Key, PrecompiledFallbackReason.StaleImport, $"Import: '{import.Key}' missing");
                if (!string.Equals(HashFile(importPath), import.ContentHash, StringComparison.Ordinal))
                    return Fail(entry.Key, PrecompiledFallbackReason.StaleImport,
                        $"Import: '{import.Key}' hash mismatch");
            }

            return null;
        }

        /// <summary>Decode-then-hash: the file is read with BOM detection (a BOM is honored and
        /// stripped; no BOM means UTF-8) and its <b>decoded text</b> is hashed through the shared
        /// <see cref="ContentHash"/> rule — the same input the build hashed at build time. Hashing the raw byte
        /// stream here is what made every BOM'd/UTF-16 template permanently <c>StaleContent</c>.</summary>
        internal static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
                return ContentHash.HashText(reader.ReadToEnd());
        }

        /// <summary>The manifest identity string, produced by the shared <see cref="AqnFormatter"/>
        /// through its reflection adapter — the same rule the build host applies when it writes the row, so a nested or
        /// generic container can no longer spell its identity two different ways.</summary>
        internal static string AqnSansVersion(Type type) => ReflectionTypeIdentity.AqnSansVersion(type);

        private static string Lower(bool value) => value ? "true" : "false";

        private static PrecompiledFallbackEvent Fail(string key, PrecompiledFallbackReason reason, string detail)
        {
            return PrecompiledFallbackEvent.ForTemplate(key, reason, detail, Hed7101);
        }
    }
}
