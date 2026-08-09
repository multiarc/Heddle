using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Heddle.Language;

namespace Heddle.Generator.Probe
{
    /// <summary>
    /// The <b>build-side</b> driver for <see cref="HookProbeProtocol"/>: it asks a <i>separately loaded</i> engine
    /// to compile the protocol's three documents and reads the answers back by reflection. The engine-side driver
    /// in the test suite does the same thing against the engine it is compiled with, and the two are held to the
    /// same decode function — that is the shared-source pattern this file is the second half of.
    /// <para><b>Why the engine is loaded rather than linked.</b> A <c>ParseContext</c> compiled into
    /// <c>Heddle.Generator.dll</c> is a different CLR type from the one inside the consumer's <c>Heddle.dll</c>, so
    /// a hook could not be handed one; constructing any <c>CompileScope</c> runs <c>CSharpContext</c>'s static
    /// constructor, which does file IO and would be an RS1035 violation in this assembly; and a linked engine would
    /// answer "what does <c>@string</c> do" from the generator's copy rather than from the version the consumer
    /// actually references, inverting the fidelity every other build-time answer has. Loading keeps all three
    /// right, and the file IO happens inside the loaded assembly, not in analyzer code.</para>
    /// <para><b>The sentinel types stay this assembly's.</b> The probe hands the loaded engine
    /// <see cref="HookProbeProtocol.SParent"/> as the root scope type, so every <c>Type</c> that comes back is one
    /// this side can compare by identity — which is exactly what <see cref="HookProbeProtocol.Classify"/> does. The
    /// roles are a property of the extension, not of the type system the sentinels live in.</para>
    /// <para><b>Every failure is "unprobeable", never a guess and never a build error.</b> A member this driver
    /// cannot find, an exception out of the loaded engine, a bootstrap that does not hold, two probes that disagree
    /// — each costs the extension its precompiled tier and nothing else.</para>
    /// </summary>
    internal sealed class ReflectionHookProbe
    {
        /// <summary>How long one document's compile may take before the probe stops waiting.
        /// <para>This is a bounded <b>wait</b>, not bounded execution: there is no sandbox here (see
        /// <see cref="ProbeAssemblyLoader"/>), so a hook that never returns keeps running on its background thread
        /// for the life of the compiler process. What the timeout buys is that the build does not hang with it —
        /// and, because a hook that ran long once may do so again, the first timeout ends probing for the whole
        /// compilation rather than only for that extension.</para></summary>
        internal const int ProbeTimeoutMilliseconds = 10000;

        private readonly Reflected _api;
        private readonly Dictionary<string, HookProbeResult?> _cache =
            new Dictionary<string, HookProbeResult?>(StringComparer.Ordinal);

        private bool _abandoned;

        private ReflectionHookProbe(Reflected api)
        {
            _api = api;
        }

        /// <summary>Binds to a loaded engine and runs the bootstrap. <c>null</c> means "do not probe against this
        /// assembly": either it does not have the shape the protocol was written for, or <c>@param</c> did not
        /// behave as the protocol requires, in which case every other answer it could give is unsafe to believe.</summary>
        internal static ReflectionHookProbe TryCreate(Assembly engine)
        {
            var api = Reflected.TryBind(engine);
            if (api == null)
                return null;

            var probe = new ReflectionHookProbe(api);
            var name = HookProbeProtocol.BootstrapExtensionName;
            if (!probe.TryObserveAll(name, out var a, out var b, out var c) ||
                !HookProbeProtocol.IsBootstrapSound(a, b, c))
                return null;

            return probe;
        }

        /// <summary>The probe's answer for one extension name, or <c>false</c> when it has none.
        /// <para><b>Double-probed.</b> Every name is observed twice, on entirely fresh instances, and a
        /// disagreement refuses the extension outright. One rule, two properties: an extension that carries state
        /// between compiles or answers nondeterministically is caught, and generator output stays a pure function
        /// of the compilation rather than of how many times something has been asked.</para></summary>
        internal bool TryProbe(string name, out HookProbeResult result) => TryProbe(name, null, null, out result);

        /// <summary>The probe's answer for one extension name, refused unless the loaded engine has that name bound
        /// to exactly the type this compilation resolved it to.
        /// <para>That identity check is what makes the answer belong to <i>this</i> build. The registry inside a
        /// loaded engine is process-wide and monotone, so without it a name registered by one compilation could
        /// answer for the same name in the next one, from a different assembly. With it, a disagreement is simply
        /// an extension this build declines to probe.</para></summary>
        internal bool TryProbe(string name, string expectedTypeName, string expectedAssemblyName,
            out HookProbeResult result)
        {
            result = default;
            if (name == null || _abandoned || !_api.Exists(name))
                return false;
            if (expectedTypeName != null && !_api.IsBoundTo(name, expectedTypeName, expectedAssemblyName))
                return false;

            if (!_cache.TryGetValue(name, out var cached))
            {
                cached = Measure(name);
                _cache[name] = cached;
            }

            if (cached == null)
                return false;
            result = cached.Value;
            return true;
        }

        private HookProbeResult? Measure(string name)
        {
            if (!TryObserveAll(name, out var a1, out var b1, out var c1))
                return null;
            var first = HookProbeProtocol.Decode(a1, b1, c1);

            if (!TryObserveAll(name, out var a2, out var b2, out var c2))
                return null;
            var second = HookProbeProtocol.Decode(a2, b2, c2);

            if (first.Outcome != second.Outcome || first.Body != second.Body || first.Chained != second.Chained ||
                first.ZeroOutput != second.ZeroOutput)
                return null;

            return first.Outcome == HookProbeOutcome.Unclassified ? (HookProbeResult?)null : first;
        }

        private bool TryObserveAll(string name, out HookProbeObservation a, out HookProbeObservation b,
            out HookProbeObservation c) =>
            TryObserve(name, HookProbeDocument.A, out a) &
            TryObserve(name, HookProbeDocument.B, out b) &
            TryObserve(name, HookProbeDocument.C, out c);

        /// <summary>One document, compiled on a background thread the caller stops waiting for. The thread is a
        /// background thread on purpose: the process must be able to exit while a runaway hook is still in it.</summary>
        private bool TryObserve(string name, HookProbeDocument document, out HookProbeObservation observation)
        {
            observation = default;
            if (_abandoned)
                return false;

            HookProbeObservation captured = default;
            var ok = false;
            var thread = new Thread(() =>
            {
                try
                {
                    ok = Observe(name, document, out captured);
                }
                catch (Exception)
                {
                    ok = false;
                }
            });
            thread.IsBackground = true;
            thread.Start();

            if (!thread.Join(ProbeTimeoutMilliseconds))
            {
                _abandoned = true;
                return false;
            }

            observation = captured;
            return ok;
        }

        private bool Observe(string name, HookProbeDocument document, out HookProbeObservation observation)
        {
            observation = default;
            var text = HookProbeProtocol.Document(name, document);

            IDisposable context = null;
            try
            {
                var options = Activator.CreateInstance(_api.TemplateOptions);
                _api.ProvideLanguageFeatures.SetValue(options, true, null);
                var rootType = Activator.CreateInstance(_api.ExType,
                    new object[] { typeof(HookProbeProtocol.SParent) });
                context = (IDisposable)Activator.CreateInstance(_api.CompileContext,
                    new object[] { options, rootType });

                var template = Activator.CreateInstance(_api.HeddleTemplate);
                _api.Compile.Invoke(template, new object[] { text, context });

                var scopeMap = _api.ScopeMap.GetValue(context, null);
                var entries = scopeMap == null ? null : (IEnumerable)_api.Entries.GetValue(scopeMap, null);

                object body = null;
                var count = 0;
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        count++;
                        if (count == 2)
                            body = entry;
                    }
                }

                // Entry 0 is the document itself; the hook's body is the first span recorded inside it. A span whose
                // length is not the marker's belongs to some other compile the hook triggered, which the protocol
                // reads as unrecognized rather than guessing at.
                var compiled = count > 1;
                var readable = compiled && (int)_api.Length.GetValue(body, null) ==
                               HookProbeProtocol.BodyMarker.Length;

                var returned = HookProbeSentinel.Unrecognized;
                if (document == HookProbeDocument.C)
                {
                    var items = (IDictionary)_api.CompiledItems.GetValue(context, null);
                    if (items != null && items.Count == 1)
                    {
                        foreach (var value in items.Values)
                            returned = Sentinel(_api.ReturnTypeChainedPrevious.GetValue(value));
                    }
                }

                observation = new HookProbeObservation(
                    compiled,
                    compiled
                        ? (readable ? Sentinel(_api.ModelType.GetValue(body, null)) : HookProbeSentinel.Unrecognized)
                        : HookProbeSentinel.Absent,
                    compiled
                        ? (readable ? Sentinel(_api.ChainedType.GetValue(body, null)) : HookProbeSentinel.Unrecognized)
                        : HookProbeSentinel.Absent,
                    returned);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                try
                {
                    context?.Dispose();
                }
                catch (Exception)
                {
                    // A loaded engine's Dispose is not this build's business.
                }
            }
        }

        private HookProbeSentinel Sentinel(object exType)
        {
            if (exType == null)
                return HookProbeProtocol.Classify(null, false);
            var type = (Type)_api.ExTypeType.GetValue(exType, null);
            var dynamic = (bool)_api.IsDynamic.GetValue(exType, null);
            return HookProbeProtocol.Classify(type, dynamic);
        }

        /// <summary>Every member the driver reads, bound once. Nothing here is a public API of the engine — the
        /// state the protocol reads is compile-time bookkeeping — so the binding is by name and is allowed to
        /// fail: a member that has moved makes the whole engine unprobeable, which is the fail-closed direction.</summary>
        private sealed class Reflected
        {
            internal Type TemplateOptions;
            internal Type ExType;
            internal Type CompileContext;
            internal Type HeddleTemplate;
            internal PropertyInfo ProvideLanguageFeatures;
            internal MethodInfo Compile;
            internal PropertyInfo ScopeMap;
            internal PropertyInfo Entries;
            internal PropertyInfo Length;
            internal PropertyInfo ModelType;
            internal PropertyInfo ChainedType;
            internal PropertyInfo CompiledItems;
            internal FieldInfo ReturnTypeChainedPrevious;
            internal PropertyInfo ExTypeType;
            internal PropertyInfo IsDynamic;
            private MethodInfo _exists;
            private MethodInfo _tryGetExtensionType;

            /// <summary>Whether the loaded engine registers this name at all. A name it does not know compiles no
            /// body and returns nothing, which decodes to a perfectly well-formed <c>NoBody</c> answer about an
            /// extension that does not exist — so the question is asked first rather than read out of the
            /// silence.</summary>
            internal bool Exists(string name)
            {
                try
                {
                    return (bool)_exists.Invoke(null, new object[] { name });
                }
                catch (Exception)
                {
                    return false;
                }
            }

            /// <summary>Whether the loaded engine binds <paramref name="name"/> to the type the build's own binder
            /// resolved it to, compared by CLR full name and assembly simple name — the same pair the manifest
            /// binding row carries.</summary>
            internal bool IsBoundTo(string name, string typeName, string assemblyName)
            {
                try
                {
                    var args = new object[] { name, null };
                    if (!(bool)_tryGetExtensionType.Invoke(null, args))
                        return false;
                    var type = (Type)args[1];
                    return type != null &&
                           string.Equals(type.FullName, typeName, StringComparison.Ordinal) &&
                           (assemblyName == null ||
                            string.Equals(type.Assembly.GetName().Name, assemblyName, StringComparison.Ordinal));
                }
                catch (Exception)
                {
                    return false;
                }
            }

            private const BindingFlags Any =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            internal static Reflected TryBind(Assembly engine)
            {
                if (engine == null)
                    return null;

                try
                {
                    var r = new Reflected
                    {
                        TemplateOptions = engine.GetType("Heddle.Data.TemplateOptions", false),
                        ExType = engine.GetType("Heddle.Data.ExType", false),
                        CompileContext = engine.GetType("Heddle.Runtime.CompileContext", false),
                        HeddleTemplate = engine.GetType("Heddle.HeddleTemplate", false)
                    };
                    var scopeMapEntry = engine.GetType("Heddle.Runtime.ScopeMapEntry", false);
                    var scopeMap = engine.GetType("Heddle.Runtime.ScopeMap", false);
                    var compiledElement = engine.GetType("Heddle.Runtime.CompiledElement", false);
                    var factory = engine.GetType("Heddle.Runtime.TemplateFactory", false);
                    if (factory != null)
                    {
                        const BindingFlags anyStatic =
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                        r._exists = factory.GetMethod("Exists", anyStatic, null, new[] { typeof(string) }, null);
                        r._tryGetExtensionType = factory.GetMethod("TryGetExtensionType", anyStatic, null,
                            new[] { typeof(string), typeof(Type).MakeByRefType() }, null);
                    }

                    if (r._exists == null || r._tryGetExtensionType == null || r.TemplateOptions == null || r.ExType == null || r.CompileContext == null ||
                        r.HeddleTemplate == null || scopeMapEntry == null || scopeMap == null ||
                        compiledElement == null)
                        return null;

                    r.ProvideLanguageFeatures = r.TemplateOptions.GetProperty("ProvideLanguageFeatures", Any);
                    r.Compile = r.HeddleTemplate.GetMethod("Compile", Any, null,
                        new[] { typeof(string), r.CompileContext }, null);
                    r.ScopeMap = r.CompileContext.GetProperty("ScopeMap", Any);
                    r.CompiledItems = r.CompileContext.GetProperty("CompiledItems", Any);
                    r.Entries = scopeMap.GetProperty("Entries", Any);
                    r.Length = scopeMapEntry.GetProperty("Length", Any);
                    r.ModelType = scopeMapEntry.GetProperty("ModelType", Any);
                    r.ChainedType = scopeMapEntry.GetProperty("ChainedType", Any);
                    r.ReturnTypeChainedPrevious = compiledElement.GetField("ReturnTypeChainedPrevious", Any);
                    r.ExTypeType = r.ExType.GetProperty("Type", Any);
                    r.IsDynamic = r.ExType.GetProperty("IsDynamic", Any);

                    if (r.ProvideLanguageFeatures == null || r.Compile == null || r.ScopeMap == null ||
                        r.CompiledItems == null || r.Entries == null || r.Length == null || r.ModelType == null ||
                        r.ChainedType == null || r.ReturnTypeChainedPrevious == null || r.ExTypeType == null ||
                        r.IsDynamic == null)
                        return null;

                    if (r.ExType.GetConstructor(new[] { typeof(Type) }) == null ||
                        r.CompileContext.GetConstructor(new[] { r.TemplateOptions, r.ExType }) == null ||
                        r.HeddleTemplate.GetConstructor(new Type[0]) == null ||
                        r.TemplateOptions.GetConstructor(new Type[0]) == null)
                        return null;

                    return r;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}
