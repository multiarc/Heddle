using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Heddle.Generator.Emit;
using Heddle.Generator.Pipeline;
using Heddle.Precompiled;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Observe
{
    /// <summary>
    /// Layer 2, whole: emit the compilation being built to a content-addressed intermediate assembly, load it as a
    /// bundle with the consumer's own engine and every extension assembly, compile the real template through the
    /// real engine, and hand the emitter the types the engine actually chose.
    /// <para><b>It is an optimisation and nothing else.</b> A body it types is emitted with a direct cast; a body
    /// it cannot type is still precompiled, through the engine's own zero-allocation accessors. It is consulted
    /// only where the build would otherwise have <i>no</i> typing at all, so what it can change is which tier a
    /// read takes and never a rendered byte — which is exactly what the byte-identity gate asserts.</para>
    /// <para><b>Only types and structure are harvested, never executable form.</b> A compiled item's
    /// <c>Parameter</c> is a JIT-compiled delegate with its expression tree discarded and its <c>Extension</c> is a
    /// live mutated instance, so there is nothing there to translate back into C#. Value expressions stay the
    /// generator's own job, through Roslyn symbols.</para>
    /// <para><b>Nothing here may enter the incremental pipeline.</b> No <see cref="Assembly"/>, no
    /// <see cref="Type"/> and no harvest is ever a provider payload — it would break provider equality and pin
    /// memory across generations — so all of it is computed inside <c>RegisterSourceOutput</c> and memoised
    /// content-addressed instead.</para>
    /// </summary>
    internal sealed class EngineObservation
    {
        /// <summary>The metadata name of the type that identifies the engine: the base every Heddle extension
        /// derives from. It is a type name, not an extension name — nothing here decides whether a particular
        /// extension exists.</summary>
        private const string AbstractExtensionMetadataName = "Heddle.Core.AbstractExtension";

        private static readonly TimeSpan ObserveTimeout = TimeSpan.FromSeconds(20);

        private static readonly object MemoGate = new object();

        /// <summary>Digest → the span map already harvested for it. Content-addressed on template text, the import
        /// closure, the build options, the engine's module version id and the intermediate assembly's digest, so a
        /// keystroke that changes nothing re-observes nothing.</summary>
        private static readonly Dictionary<string, IReadOnlyList<EngineSurface.Entry>> Memo =
            new Dictionary<string, IReadOnlyList<EngineSurface.Entry>>(StringComparer.Ordinal);

        /// <summary>Extension names the process has seen bound to two different types. The registry is
        /// process-global and append-only with no unregister, so a contaminated name is refused for the life of the
        /// process rather than answered wrongly once.</summary>
        private static readonly HashSet<string> Poisoned = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Set by the first observed compile that never returned. One hung hook ends observation for the
        /// whole process: the alternative is paying the timeout again on every template.</summary>
        private static bool _timedOut;

        private readonly Compilation _compilation;
        private readonly GlobalConfig _config;
        private readonly EngineSurface _surface;
        private readonly Assembly _intermediate;
        private readonly string _digestPrefix;
        private readonly Func<string, string> _importReader;
        private readonly Func<string, string> _importIdentifier;
        private readonly Dictionary<string, ITypeSymbol> _symbolCache =
            new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);

        private EngineObservation(Compilation compilation, GlobalConfig config, EngineSurface surface,
            Assembly intermediate, string digestPrefix, Func<string, string> importReader,
            Func<string, string> importIdentifier)
        {
            _compilation = compilation;
            _config = config;
            _surface = surface;
            _intermediate = intermediate;
            _digestPrefix = digestPrefix;
            _importReader = importReader;
            _importIdentifier = importIdentifier;
        }

        /// <summary>Test seam: how many observed compiles this process has actually run, as opposed to answered
        /// from the memo.</summary>
        internal static int CompilesRun { get; private set; }

        /// <summary>
        /// Builds the bundle and binds the engine, or explains why observation is unavailable. Every failure is a
        /// sentence a build diagnostic can carry; none of them is an error on its own — <c>strict</c> decides that.
        /// </summary>
        internal static EngineObservation TryCreate(Compilation compilation, GlobalConfig config,
            ExtensionBinder binder, string importClosureDigest, Func<string, string> importReader,
            Func<string, string> importIdentifier, out string failure)
        {
            failure = null;
            if (config.ObserveMode == ObserveMode.Off)
                return null;
            if (_timedOut)
            {
                failure = "an earlier observed compile in this process did not finish, so observation is off";
                return null;
            }

            var engineSymbol = ExtensionBinder.EngineAssemblyOf(compilation);
            if (engineSymbol == null)
            {
                failure = "the compilation references no assembly declaring " + AbstractExtensionMetadataName;
                return null;
            }

            var implementations = ImplementationReferences.Read(config.ObserveImplementationPath);
            if (IsReferenceAssembly(engineSymbol) && !implementations.TryGet(engineSymbol.Identity, out _))
            {
                failure = "the engine assembly '" + engineSymbol.Identity.Name + "' is referenced as a reference " +
                          "assembly, which carries no method bodies and cannot be executed, and the build declared " +
                          "no implementation of that identity in " +
                          HeddleBuildOptions.ObserveImplementationPathProperty;
                return null;
            }

            var intermediatePath = IntermediateAssembly.Produce(compilation, config.ObserveIntermediatePath,
                out _, out var emitFailure, out var intermediateName);
            if (intermediatePath == null)
            {
                failure = emitFailure;
                return null;
            }

            var loader = new BundleLoader(config.ObserveIntermediatePath);
            foreach (var reference in compilation.References)
            {
                var portable = reference as PortableExecutableReference;
                if (portable == null || string.IsNullOrEmpty(portable.FilePath))
                    continue;
                var symbol = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                var simple = symbol != null ? symbol.Identity.Name : BundleLoader.FileSimpleName(portable.FilePath);

                // The compiler holds the reference assembly; the bundle has to hold something it can run. The
                // substitution is keyed on the identity the compilation itself resolved, so what gets loaded is
                // the same assembly the compilation compiled against or nothing at all — never a near-miss whose
                // metadata names would map onto other types.
                if (symbol != null && implementations.TryGet(symbol.Identity, out var implementation))
                    loader.Declare(simple, implementation.Path, implementation.ModuleVersionId);
                else
                    loader.Declare(simple, portable.FilePath, ModuleVersionIdOf(portable));
            }

            loader.DeclareContentAddressed(intermediateName, intermediatePath);
            loader.Arm();

            var engine = loader.TryLoad(engineSymbol.Identity.Name);
            if (engine == null)
            {
                failure = "the engine assembly '" + engineSymbol.Identity.Name + "' could not be loaded from a " +
                          "content-addressed copy" +
                          (BundleLoader.LastLoadError == null ? "" : " — " + BundleLoader.LastLoadError);
                return null;
            }

            var surface = EngineSurface.TryBind(engine, out failure);
            if (surface == null)
                return null;

            var intermediate = loader.TryLoad(intermediateName);
            if (intermediate == null)
            {
                failure = "the intermediate assembly could not be loaded from '" + intermediatePath + "'" +
                          (BundleLoader.LastLoadError == null ? "" : " — " + BundleLoader.LastLoadError);
                return null;
            }

            if (!RegisterAndVerify(surface, loader, binder, intermediate, out failure))
                return null;

            var digestPrefix = new StringBuilder()
                .Append(IntermediateAssembly.Digest(compilation)).Append('|')
                .Append(ModuleVersionIdOf(engine)).Append('|')
                .Append(implementations.Digest).Append('|')
                .Append(importClosureDigest ?? string.Empty).Append('|')
                .Append((int)config.OutputProfile).Append('|')
                .Append((int)config.ExpressionMode).Append('|')
                .Append(config.TrimDirectiveLines ? '1' : '0').Append('|')
                .Append(config.MaxRecursionCount).Append('|')
                .ToString();

            return new EngineObservation(compilation, config, surface, intermediate, digestPrefix, importReader,
                importIdentifier);
        }

        /// <summary>
        /// Registers every extension assembly the binder found with the loaded engine, then makes the registry's
        /// answers this compilation's own: a name the registry binds to a type this compilation did not resolve is
        /// poisoned for the process, and a poisoned name refuses observation outright rather than serving an answer
        /// some other project's extension produced.
        /// </summary>
        private static bool RegisterAndVerify(EngineSurface surface, BundleLoader loader, ExtensionBinder binder,
            Assembly intermediate, out string failure)
        {
            failure = null;
            try
            {
                surface.Register(intermediate);
                foreach (var assemblyName in binder.ExtensionAssemblyNames)
                {
                    var assembly = loader.TryLoad(assemblyName);
                    if (assembly != null)
                        surface.Register(assembly);
                }
            }
            catch (Exception e)
            {
                failure = "registering the compilation's extensions with the loaded engine failed: " +
                          (e.InnerException ?? e).Message;
                return false;
            }

            var contaminated = new List<string>();
            foreach (var pair in binder.ByName)
            {
                var bound = surface.BoundExtensionType(pair.Key);
                if (bound == null)
                    continue;
                if (!string.Equals(bound.FullName, pair.Value.BareTypeName, StringComparison.Ordinal))
                {
                    lock (MemoGate)
                        Poisoned.Add(pair.Key);
                }

                lock (MemoGate)
                {
                    if (Poisoned.Contains(pair.Key))
                        contaminated.Add(pair.Key);
                }
            }

            if (contaminated.Count == 0)
                return true;

            contaminated.Sort(StringComparer.Ordinal);
            failure = "the loaded engine's extension registry binds " + string.Join(", ", contaminated.ToArray()) +
                      " to a type this compilation did not resolve, and it has no unregister";
            return false;
        }

        /// <summary>
        /// Compiles one template through the real engine and returns the model type it chose for each body span,
        /// mapped back to this compilation's symbols.
        /// </summary>
        /// <returns>A map from (offset, length) to the model symbol, or <c>null</c> when the template could not be
        /// observed at all.</returns>
        internal ObservedDocument Observe(string content, ITypeSymbol modelSymbol, out string failure)
        {
            failure = null;
            if (_timedOut)
            {
                failure = "an earlier observed compile in this process did not finish, so observation is off";
                return null;
            }

            Type runtimeModel = null;
            if (modelSymbol != null && modelSymbol.TypeKind != TypeKind.Dynamic)
            {
                runtimeModel = RuntimeTypeOf(modelSymbol);
                if (runtimeModel == null)
                {
                    failure = "the template's model type '" + modelSymbol.ToDisplayString() +
                              "' has no counterpart in the intermediate assembly";
                    return null;
                }
            }

            var digest = ContentHash.HashText(_digestPrefix + (runtimeModel == null
                ? "dynamic"
                : runtimeModel.AssemblyQualifiedName) + "|" + content);

            IReadOnlyList<EngineSurface.Entry> entries;
            lock (MemoGate)
            {
                if (Memo.TryGetValue(digest, out entries))
                    return new ObservedDocument(this, entries);
            }

            var profile = _surface.ParseEnum("Heddle.Data.OutputProfile", _config.OutputProfile.ToString());
            var mode = _surface.ParseEnum("Heddle.Data.ExpressionMode", _config.ExpressionMode.ToString());
            if (profile == null || mode == null)
            {
                failure = "the loaded engine does not carry the output profile or expression mode this build uses";
                return null;
            }

            entries = _surface.TryObserve(content, runtimeModel, string.Empty, profile, mode,
                _config.TrimDirectiveLines, _config.MaxRecursionCount, _importReader, _importIdentifier,
                ObserveTimeout, out failure);
            CompilesRun++;
            if (entries == null)
            {
                if (failure != null && failure.IndexOf("did not finish", StringComparison.Ordinal) >= 0)
                    _timedOut = true;
                failure = failure ?? "the observed compile produced no scope map";
                return null;
            }

            lock (MemoGate)
                Memo[digest] = entries;
            return new ObservedDocument(this, entries);
        }

        /// <summary>
        /// Whether a referenced assembly is a <b>reference assembly</b> — metadata with its method bodies thrown
        /// away, which the runtime refuses to execute at all.
        /// <para>A project-to-project reference compiles against the referenced project's generated reference
        /// assembly, so the engine the compilation names is an image with no <c>InitStart</c> in it to run — but
        /// MSBuild knows the implementation too, and <see cref="ImplementationReferences"/> carries it. What this
        /// predicate answers is therefore only "does this one need substituting", and a build with no substitution
        /// on record for it is the one that cannot observe.</para>
        /// </summary>
        private static bool IsReferenceAssembly(IAssemblySymbol assembly)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass != null &&
                    attributeClass.ToDisplayString() == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute")
                    return true;
            }

            return false;
        }

        /// <summary>Maps a runtime type the engine reported back onto this compilation's symbols, by metadata name
        /// against the emitted assembly — 1:1 by construction, because the assembly <i>is</i> the compilation.
        /// Anything the mapping cannot express is <c>null</c>, which is the observation declining rather than
        /// guessing.</summary>
        internal ITypeSymbol SymbolOf(Type type)
        {
            if (type == null || type.IsByRef || type.IsPointer || type.IsGenericParameter)
                return null;

            if (type.IsArray)
            {
                var element = SymbolOf(type.GetElementType());
                return element == null ? null : _compilation.CreateArrayTypeSymbol(element, type.GetArrayRank());
            }

            if (type.IsConstructedGenericType)
            {
                var definition = SymbolOf(type.GetGenericTypeDefinition()) as INamedTypeSymbol;
                if (definition == null)
                    return null;
                var arguments = type.GenericTypeArguments;
                var mapped = new ITypeSymbol[arguments.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    mapped[i] = SymbolOf(arguments[i]);
                    if (mapped[i] == null)
                        return null;
                }

                return definition.Construct(mapped);
            }

            var metadataName = type.FullName;
            if (metadataName == null)
                return null;

            lock (_symbolCache)
            {
                if (_symbolCache.TryGetValue(metadataName, out var cached))
                    return cached;
            }

            var symbol = _compilation.GetTypeByMetadataName(metadataName);
            lock (_symbolCache)
                _symbolCache[metadataName] = symbol;
            return symbol;
        }

        /// <summary>The runtime type behind a symbol, looked up in the bundle by the same metadata name the symbol
        /// carries. Only named types are resolved; anything else is the observation declining.</summary>
        private Type RuntimeTypeOf(ITypeSymbol symbol)
        {
            var named = symbol as INamedTypeSymbol;
            if (named == null || named.IsGenericType)
                return MetadataName(symbol) == null ? null : LookupInBundle(MetadataName(symbol));
            var name = MetadataName(symbol);
            return name == null ? null : LookupInBundle(name);
        }

        private Type LookupInBundle(string metadataName)
        {
            var found = _intermediate.GetType(metadataName, false);
            if (found != null)
                return found;
            found = _surface.ResolveType(metadataName);
            if (found != null)
                return found;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;
                try
                {
                    found = assembly.GetType(metadataName, false);
                }
                catch (Exception)
                {
                    continue;
                }

                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>The CLR metadata name of a named type — <c>Ns.Outer+Inner</c>, the spelling reflection
        /// produces, rather than the display string's dotted form.</summary>
        internal static string MetadataName(ITypeSymbol symbol)
        {
            var named = symbol as INamedTypeSymbol;
            if (named == null)
                return null;

            var parts = new List<string>();
            var current = named;
            while (current != null)
            {
                parts.Insert(0, current.MetadataName);
                current = current.ContainingType;
            }

            var ns = named.ContainingNamespace;
            var prefix = new List<string>();
            while (ns != null && !ns.IsGlobalNamespace)
            {
                prefix.Insert(0, ns.Name);
                ns = ns.ContainingNamespace;
            }

            var name = string.Join("+", parts.ToArray());
            return prefix.Count == 0 ? name : string.Join(".", prefix.ToArray()) + "." + name;
        }

        private static string ModuleVersionIdOf(PortableExecutableReference reference)
        {
            try
            {
                var assembly = reference.GetMetadata() as AssemblyMetadata;
                if (assembly != null)
                {
                    foreach (var module in assembly.GetModules())
                        return module.GetModuleVersionId().ToString("N");
                }

                var single = reference.GetMetadata() as ModuleMetadata;
                if (single != null)
                    return single.GetModuleVersionId().ToString("N");
            }
            catch (Exception)
            {
            }

            return null;
        }

        private static string ModuleVersionIdOf(Assembly assembly)
        {
            try
            {
                return assembly.ManifestModule.ModuleVersionId.ToString("N");
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// The one place a bundle is built, and deliberately <b>not</b> built until something asks.
    /// <para>Emitting the compilation being built is a whole C# compile, and the generator runs on every change to
    /// that compilation — so paying it for a project whose templates the build already types would put a full emit
    /// on an editor's keystroke path for nothing. Observation is consulted only where the build has no typing of
    /// its own, so this is asked only from there, and a compilation that never asks emits no assembly, loads
    /// nothing, and cannot fail to observe.</para>
    /// </summary>
    internal sealed class ObservationSource
    {
        private readonly Compilation _compilation;
        private readonly GlobalConfig _config;
        private readonly ExtensionBinder _binder;
        private readonly string _importClosureDigest;
        private readonly Func<string, string> _importReader;
        private readonly Func<string, string> _importIdentifier;

        private bool _attempted;
        private EngineObservation _observation;
        private string _failure;

        internal ObservationSource(Compilation compilation, GlobalConfig config, ExtensionBinder binder,
            string importClosureDigest, Func<string, string> importReader, Func<string, string> importIdentifier)
        {
            _compilation = compilation;
            _config = config;
            _binder = binder;
            _importClosureDigest = importClosureDigest;
            _importReader = importReader;
            _importIdentifier = importIdentifier;
        }

        /// <summary>The bundle, built on the first ask and remembered — answer or reason — for every later one.
        /// </summary>
        internal EngineObservation Get(out string failure)
        {
            if (!_attempted)
            {
                _attempted = true;
                _observation = EngineObservation.TryCreate(_compilation, _config, _binder, _importClosureDigest,
                    _importReader, _importIdentifier, out _failure);
            }

            failure = _failure;
            return _observation;
        }
    }

    /// <summary>
    /// One template's observed body-compile spans. A span's answer is used only when <b>every</b> recorded entry
    /// for it agrees: a definition body compiles once per call site, so aligning by record order would be unsound
    /// and two disagreeing entries mean the span has no single typing to emit.
    /// </summary>
    internal sealed class ObservedDocument
    {
        private readonly EngineObservation _owner;
        private readonly IReadOnlyList<EngineSurface.Entry> _entries;

        internal ObservedDocument(EngineObservation owner, IReadOnlyList<EngineSurface.Entry> entries)
        {
            _owner = owner;
            _entries = entries;
        }

        /// <summary>
        /// The model the engine compiled the body at <paramref name="offset"/> against — <c>false</c> when the span
        /// was never recorded, the entries disagree, or the type it names has no symbol in this compilation.
        /// <para><b>Dynamic is an answer, not the absence of one.</b> The engine compiling a body against a dynamic
        /// scope is what it did, and it is what the emitter reproduces by building the body in a dynamic context —
        /// which is a whole tier better than emitting it type-agnostically, because a dynamic-tier body still reads
        /// members, calls functions and hosts branch participants. Conflating the two put every body under a
        /// model-less root through the substitute.</para>
        /// </summary>
        internal bool TryBodyModel(int offset, int length, out ITypeSymbol model, out bool isDynamic)
        {
            model = null;
            isDynamic = false;
            Type found = null;
            var seen = false;
            var anyDynamic = false;
            foreach (var entry in _entries)
            {
                if (entry.Offset != offset || entry.Length != length)
                    continue;
                if (entry.ModelDynamic || entry.Model == null)
                {
                    if (seen && !anyDynamic)
                        return false;
                    anyDynamic = true;
                    seen = true;
                    continue;
                }

                if (seen && (anyDynamic || entry.Model != found))
                    return false;
                found = entry.Model;
                seen = true;
            }

            if (!seen)
                return false;
            if (anyDynamic)
            {
                isDynamic = true;
                return true;
            }

            model = _owner.SymbolOf(found);
            return model != null;
        }
    }
}
