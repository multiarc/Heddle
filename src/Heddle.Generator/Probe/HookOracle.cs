using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Generator.Pipeline;
using Heddle.Language;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Probe
{
    /// <summary>
    /// What the emitter asks about an extension's compile-time hook, and the one place the answer is assembled from
    /// a loaded engine. Off unless <c>HeddleProbeExtensionHooks</c> is set, and off whenever the engine behind the
    /// compilation's reference cannot be reached from an immutable root — in either case
    /// <see cref="TryGet(string, string, string, out HookProbeResult)"/> simply has no answer and the caller keeps
    /// whatever it would have done without one.
    /// <para><b>A failing hook costs a call site its tier and never fails the build.</b> That is the whole error
    /// model here, and it is deliberately different from the emitter's own defect channel: a defect in this
    /// generator is the maintainer's fault and is reported loudly, while a third-party extension the build cannot
    /// evaluate is a property of someone else's package.</para>
    /// </summary>
    internal sealed class HookOracle
    {
        /// <summary>The answer for a build that is not probing: no answers, and no assembly loaded to find out.</summary>
        internal static readonly HookOracle Disabled = new HookOracle(null);

        private static readonly object Gate = new object();

        /// <summary>Probes by engine assembly, because that — plus the extension assemblies registered into it — is
        /// what an answer is a function of. Held for the compiler's lifetime on purpose: a loaded engine cannot be
        /// unloaded, so re-deriving the probe would only pay the bootstrap again.</summary>
        private static readonly Dictionary<Assembly, ReflectionHookProbe> Probes =
            new Dictionary<Assembly, ReflectionHookProbe>();

        private static readonly HashSet<string> Registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly ReflectionHookProbe _probe;

        private HookOracle(ReflectionHookProbe probe)
        {
            _probe = probe;
        }

        /// <summary>True when this build can actually observe a hook. Callers use it to keep the whole probing
        /// path — including its cost — out of a build that opted out.</summary>
        internal bool Enabled => _probe != null;

        /// <summary>Builds the oracle for one compilation: the engine assembly behind its <c>Heddle</c> reference,
        /// plus every referenced assembly that declares an extension, loaded through
        /// <see cref="ProbeAssemblyLoader"/> and registered with the loaded engine so its names resolve.</summary>
        internal static HookOracle For(Compilation compilation, GlobalConfig config,
            IEnumerable<IAssemblySymbol> extensionAssemblies)
        {
            if (compilation == null || config == null || !config.ProbeExtensionHooks)
                return Disabled;

            var abstractExtension = compilation.GetTypeByMetadataName("Heddle.Core.AbstractExtension");
            var engineSymbol = abstractExtension?.ContainingAssembly;
            if (engineSymbol == null)
                return Disabled;

            var roots = ProbeAssemblyLoader.SplitPackageFolders(config.PackageFolders);
            var loader = new ProbeAssemblyLoader(roots, ReferencePaths(compilation));

            var engine = loader.TryLoad(FilePathOf(compilation, engineSymbol));
            if (engine == null)
                return Disabled;

            lock (Gate)
            {
                if (!Probes.TryGetValue(engine, out var probe))
                {
                    probe = ReflectionHookProbe.TryCreate(engine);
                    Probes[engine] = probe;
                }

                if (probe == null)
                    return Disabled;

                RegisterExtensionAssemblies(compilation, engine, loader, engineSymbol, extensionAssemblies);
                return new HookOracle(probe);
            }
        }

        /// <summary>The role pair the emitter needs, or <c>false</c> when this extension has no readable answer:
        /// probing is off, the assembly was unreachable, the two probes disagreed, or the hook did something the
        /// protocol has no role for.</summary>
        internal bool TryGet(string name, string typeName, string assemblyName, out HookProbeResult result)
        {
            result = default;
            return _probe != null && _probe.TryProbe(name, typeName, assemblyName, out result);
        }

        /// <summary>Teaches the loaded engine the extensions this compilation references, so their names resolve
        /// when a probe document names one. Only ever adds; an assembly whose path is not loadable is skipped, and
        /// its extensions are then simply unprobeable.</summary>
        private static void RegisterExtensionAssemblies(Compilation compilation, Assembly engine,
            ProbeAssemblyLoader loader, IAssemblySymbol engineSymbol,
            IEnumerable<IAssemblySymbol> extensionAssemblies)
        {
            if (extensionAssemblies == null)
                return;

            var factory = engine.GetType("Heddle.Runtime.TemplateFactory", false);
            var load = factory?.GetMethod("LoadAddExtensionsFromAssembly",
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Assembly) }, null);
            var add = factory?.GetMethod("AddExtensions", BindingFlags.Public | BindingFlags.Static);
            if (load == null || add == null)
                return;

            foreach (var symbol in extensionAssemblies)
            {
                if (symbol == null || SymbolEqualityComparer.Default.Equals(symbol, engineSymbol))
                    continue;

                var path = FilePathOf(compilation, symbol);
                if (path == null || !Registered.Add(path))
                    continue;

                var assembly = loader.TryLoad(path);
                if (assembly == null)
                    continue;

                try
                {
                    add.Invoke(null, new[] { load.Invoke(null, new object[] { assembly }) });
                }
                catch (Exception)
                {
                    // A package whose extensions the engine declines to register is one this build does not probe.
                }
            }
        }

        private static string FilePathOf(Compilation compilation, IAssemblySymbol symbol) =>
            compilation.GetMetadataReference(symbol) is PortableExecutableReference pe ? pe.FilePath : null;

        private static IEnumerable<string> ReferencePaths(Compilation compilation)
        {
            foreach (var reference in compilation.References)
                if (reference is PortableExecutableReference pe && !string.IsNullOrEmpty(pe.FilePath))
                    yield return pe.FilePath;
        }
    }
}
