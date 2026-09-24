using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
            var shape = Shape.For(options, bindingResolver, requestModelType, typed: false);
            if (shape.IsMemoizable && entry.GauntletPassed(shape))
                return null;
            var verdict = RunOrdered(entry, options, bindingResolver, requestModelType);
            if (verdict == null && shape.IsMemoizable)
                entry.RecordGauntletPass(shape);
            return verdict;
        }

        /// <summary>The gauntlet's inputs that decide a verdict, and the assembly generation the live type graph
        /// was at when it did. Everything a check reads is here by value or by reference identity: the row is the
        /// entry's own, the options' compared fields, the request's model type, the integration's matcher and the
        /// function registry instance.
        /// <para>A shape whose request asks for the staleness step is <b>not</b> memoizable: that step exists to
        /// read the files again, and an answer kept from the last request is the one thing it must never give.</para>
        /// </summary>
        internal readonly struct Shape : IEquatable<Shape>
        {
            internal static Shape For(TemplateOptions options,
                Func<PrecompiledExtensionBinding, Type, bool> bindingResolver, Type requestModelType, bool typed)
            {
                return new Shape(options.OutputProfile, options.ExpressionMode, options.TrimDirectiveLines,
                    requestModelType, options.Functions, bindingResolver, typed,
                    !options.EnableFileChangeCheck, Heddle.Native.AssemblyHelper.Generation);
            }

            private Shape(OutputProfile profile, ExpressionMode mode, bool trim, Type requestModelType,
                object functions, object bindingResolver, bool typed, bool memoizable, int generation)
            {
                _profile = profile;
                _mode = mode;
                _trim = trim;
                _requestModelType = requestModelType;
                _functions = functions;
                _bindingResolver = bindingResolver;
                _typed = typed;
                IsMemoizable = memoizable;
                Generation = generation;
            }

            private readonly OutputProfile _profile;
            private readonly ExpressionMode _mode;
            private readonly bool _trim;
            private readonly Type _requestModelType;
            private readonly object _functions;
            private readonly object _bindingResolver;
            private readonly bool _typed;

            internal bool IsMemoizable { get; }

            internal int Generation { get; }

            public bool Equals(Shape other) =>
                _profile == other._profile && _mode == other._mode && _trim == other._trim &&
                _typed == other._typed && Generation == other.Generation &&
                ReferenceEquals(_requestModelType, other._requestModelType) &&
                ReferenceEquals(_functions, other._functions) &&
                ReferenceEquals(_bindingResolver, other._bindingResolver);

            public override bool Equals(object obj) => obj is Shape other && Equals(other);

            public override int GetHashCode()
            {
                int hash = Generation;
                hash = (hash * 397) ^ (int)_profile;
                hash = (hash * 397) ^ (int)_mode;
                hash = (hash * 397) ^ (_trim ? 1 : 0);
                hash = (hash * 397) ^ (_typed ? 1 : 0);
                hash = (hash * 397) ^ (_requestModelType != null ? _requestModelType.GetHashCode() : 0);
                hash = (hash * 397) ^ (_functions != null ?
                    RuntimeHelpers.GetHashCode(_functions) : 0);
                hash = (hash * 397) ^ (_bindingResolver != null ?
                    RuntimeHelpers.GetHashCode(_bindingResolver) : 0);
                return hash;
            }
        }

        /// <summary>How many times the ordered checks have actually run, as opposed to being answered from an
        /// entry's memo. Test-visible only; nothing in the engine reads it.</summary>
        internal static int OrderedRunCount => Volatile.Read(ref _orderedRunCount);

        private static int _orderedRunCount;

        private static PrecompiledFallbackEvent? RunOrdered(PrecompiledTemplateInfo entry, TemplateOptions options,
            Func<PrecompiledExtensionBinding, Type, bool> bindingResolver, Type requestModelType)
        {
            Interlocked.Increment(ref _orderedRunCount);
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
            var shape = Shape.For(options, bindingResolver, modelType, typed: true);
            if (shape.IsMemoizable && entry.GauntletPassed(shape))
                return null;
            var verdict = RunOrderedTyped(entry, options, bindingResolver, modelType);
            if (verdict == null && shape.IsMemoizable)
                entry.RecordGauntletPass(shape);
            return verdict;
        }

        private static PrecompiledFallbackEvent? RunOrderedTyped(PrecompiledTemplateInfo entry,
            TemplateOptions options,
            Func<PrecompiledExtensionBinding, Type, bool> bindingResolver, Type modelType)
        {
            Interlocked.Increment(ref _orderedRunCount);
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
            if (entry.OptionsFault != null)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch, entry.OptionsFault);
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
            if (entry.OptionsFault != null)
                return Fail(entry.Key, PrecompiledFallbackReason.OptionsMismatch, entry.OptionsFault);
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
            foreach (var binding in entry.ExtensionBindings)
            {
                // A definition call (in-document or composition-imported) records the definition
                // carrier, not a registry extension: it binds against the template's own declarations,
                // which ship inside the artifact and resolve from text at load. The extension registry
                // can neither provide nor contradict it, so there is nothing to compare.
                if (string.Equals(binding.ExtensionTypeName, DefinitionCarrier, StringComparison.Ordinal))
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
                    var liveFingerprint = CachedPropLayoutFingerprint(liveType);
                    if (!string.Equals(binding.PropLayoutFingerprint, liveFingerprint, StringComparison.Ordinal))
                        return Fail(entry.Key, PrecompiledFallbackReason.ExtensionBindingMismatch,
                            $"Extension '{binding.Name}': prop layout manifest={binding.PropLayoutFingerprint} " +
                            $"live={liveFingerprint ?? "<none>"}");
                }
            }

            return null;
        }

        /// <summary>The carrier a definition call records. One identity string for the process: it names a type
        /// this assembly ships, so it cannot change between calls.</summary>
        private static readonly string DefinitionCarrier =
            AqnSansVersion(typeof(Heddle.Core.DefinitionBaseExtension));

        private sealed class PropFingerprint
        {
            internal string Value;
        }

        /// <summary>A live extension type's <c>[Prop]</c> layout fingerprint, kept per type. Rebuilding it
        /// reflects over every declaration on the type and its bases, and the answer is a pure function of the
        /// type — which, once loaded, does not change. Weak on the type so nothing is pinned.</summary>
        private static string CachedPropLayoutFingerprint(Type liveType)
        {
            if (liveType == null)
                return null;
            PropFingerprint box;
            if (!PropFingerprints.TryGetValue(liveType, out box))
                box = PropFingerprints.GetValue(liveType, NewPropFingerprint);
            var value = Volatile.Read(ref box.Value);
            if (value != null)
                return value.Length == 0 ? null : value;
            value = PropLayout.Fingerprint(liveType);
            Volatile.Write(ref box.Value, value ?? string.Empty);
            return value;
        }

        private static readonly ConditionalWeakTable<Type, PropFingerprint> PropFingerprints =
            new ConditionalWeakTable<Type, PropFingerprint>();

        private static readonly ConditionalWeakTable<Type, PropFingerprint>.CreateValueCallback
            NewPropFingerprint = _ => new PropFingerprint();

        internal static bool DefaultBindingMatch(PrecompiledExtensionBinding binding, Type liveType)
        {
            return liveType != null &&
                   string.Equals(binding.ExtensionTypeName, AqnSansVersion(liveType), StringComparison.Ordinal);
        }

        /// <summary>The bindings step: the row's root model type was resolved by name at registration, and every
        /// recorded member row is walked from its own resolved start type through the live member graph, each hop
        /// compared by identity. Resolve-versus-compare order: the root and start types resolve by name and
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
                var resolved = PrecompiledTemplateInfo.FindLoadedTypeCached(row.StartType);
                if (resolved == null)
                    return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch,
                        "Type '" + row.StartType.Nominal() + "': manifest=" +
                        PrecompiledTemplateInfo.AssemblyOf(row.StartType) + " live=<unresolved>");
                start = new ExType(resolved);
            }

            var segments = new string[row.Segments.Count];
            for (int i = 0; i < segments.Length; i++)
                segments[i] = row.Segments[i] ?? string.Empty;

            var resolution = MemberPathResolver.TryResolve(start, segments);
            if (resolution == null || resolution.Kind == MemberPathResolutionKind.Failed)
            {
                var index = resolution != null ? resolution.Index : 0;
                return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch,
                    "Member '" + MemberPath(row, segments) + "': manifest=" + RecordedMemberNominal(row, index) +
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
                    var detail = CompareHop(hops[h], row, segments, h,
                        properties[h].Item1, properties[h].Item2);
                    if (detail != null)
                        return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch, detail);
                }

                for (int h = prefix; h < hopCount; h++)
                {
                    if (HasRecordedIdentity(hops[h]))
                        return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch,
                            "Member '" + MemberPath(row, segments) + "': manifest=" +
                            RecordedMemberNominal(row, h) + " live=<dynamic>");
                }

                return null;
            }

            if (properties == null || properties.Count != segments.Length)
                return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch,
                    "Member '" + MemberPath(row, segments) + "': manifest=" +
                    RecordedMemberNominal(row, properties != null ? properties.Count : 0) +
                    " live=<unresolved>");

            for (int h = 0; h < segments.Length; h++)
            {
                if (h >= hopCount)
                    break;
                var detail = CompareHop(hops[h], row, segments, h,
                    properties[h].Item1, properties[h].Item2);
                if (detail != null)
                    return Fail(key, PrecompiledFallbackReason.MemberBindingMismatch, detail);
            }

            return null;
        }

        /// <summary>The diagnostic spelling of a recorded member path. Built where a failure is being reported
        /// and nowhere else: every recorded row of every resolved entry passes through the walk above, and the
        /// string only ever appears in a detail no passing row produces.</summary>
        private static string MemberPath(CompiledMemberRow row, string[] segments) =>
            row.StartType.Nominal() + "." + string.Join(".", segments);

        /// <summary>Compares one recorded hop against the live walk's answer for the same segment: the recorded
        /// name must be the walked segment, the recorded declaring type must match the receiver in type-ref form,
        /// and the recorded member type must match the member's erased type in type-ref form. Returns the pinned
        /// detail on a difference, null when the hop binds. A null side carries no recorded identity and passes.
        /// Dynamic-ness rides the member's <c>DynamicAttribute</c>, which the build erases when it records the
        /// member type: a recorded concrete type matches a live member whose erased <c>PropertyType</c> is the
        /// same (the loader compiles against the live member, attribute and all), and a recorded dynamic matches
        /// a live member carrying the attribute.</summary>
        private static string CompareHop(CompiledMemberHop hop, CompiledMemberRow row,
            string[] segments, int index, Type liveDeclaring, PropertyInfo liveProperty)
        {
            if (hop == null)
                return null;
            var segment = segments[index];
            if (hop.MemberName != null && !string.Equals(hop.MemberName, segment, StringComparison.Ordinal))
                return "Member '" + MemberPath(row, segments) + "': manifest=" + NominalOrUnknown(hop.MemberType) +
                    " live=<unresolved>";
            if (hop.DeclaringType != null && !TypeRefMatches(hop.DeclaringType, liveDeclaring))
                return "Member '" + MemberPath(row, segments) + "': manifest=" + hop.DeclaringType.Nominal() +
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
                        return "Member '" + MemberPath(row, segments) + "': manifest=<dynamic>" +
                            " live=" + (liveErase != null ? AqnSansVersion(liveErase) : AqnFormatter.Unknown);
                }
                else if (!TypeRefMatches(hop.MemberType, liveErase))
                {
                    return "Member '" + MemberPath(row, segments) + "': manifest=" + hop.MemberType.Nominal() +
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

        /// <summary>The structural type-ref comparison: a named ref by full name and, for a non-framework ref,
        /// its assembly simple name (a framework ref's assembly name is advisory — System.Private.CoreLib at
        /// build, mscorlib on net48); a constructed generic by its definition and every argument, recursively
        /// (<c>Nullable&lt;T&gt;</c> is the constructed generic it is); an array by element and rank. Never
        /// resolves by name and loads nothing. Shared by every place the engine compares a recorded identity
        /// to a live type.</summary>
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
