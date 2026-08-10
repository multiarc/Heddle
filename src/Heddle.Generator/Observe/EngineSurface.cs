using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Heddle.Generator.Observe
{
    /// <summary>
    /// The reflection surface of one loaded engine, and the one place a real engine compile is driven.
    /// <para>Everything is reached reflectively because the generator cannot <i>reference</i> the engine: the parse
    /// pipeline is linked into <c>Heddle.Generator.dll</c> as shared source, so this assembly's
    /// <c>Heddle.Language.ParseContext</c> and the loaded engine's are two different types with the same name. The
    /// bundle's types are the ones the engine understands, so the bundle's types are the ones used.</para>
    /// <para>The compile is driven exactly the way <c>DocumentAnalyzer</c> drives it —
    /// <c>ParserSettings</c> → <c>DocumentParser.Parse</c> → <c>TemplateOptions</c> with
    /// <c>ProvideLanguageFeatures</c> → <c>CompileContext</c> → <c>CompileScope</c> →
    /// <c>HeddleCompiler.Compile</c> — and deliberately <b>not</b> the way <c>HeddleTemplate.Compile</c> does:
    /// that path leaves <c>ImportReader</c> null, so <c>@&lt;&lt;</c> imports are read from <i>disk</i> inside the
    /// engine. The generator's own import delegates are passed instead, so the observed compile sees byte-identical
    /// import content and touches no file.</para>
    /// </summary>
    internal sealed class EngineSurface
    {
        private readonly Type _parserSettings;
        private readonly MethodInfo _parse;
        private readonly Type _templateOptions;
        private readonly Type _exType;
        private readonly ConstructorInfo _compileContext;
        private readonly ConstructorInfo _compileScope;
        private readonly MethodInfo _compile;
        private readonly PropertyInfo _scopeMap;
        private readonly PropertyInfo _scopeMapEntries;
        private readonly PropertyInfo _entryOffset;
        private readonly PropertyInfo _entryLength;
        private readonly PropertyInfo _entryModelType;
        private readonly PropertyInfo _entryChainedType;
        private readonly PropertyInfo _exTypeIsDynamic;
        private readonly PropertyInfo _exTypeType;
        private readonly MethodInfo _loadExtensions;
        private readonly MethodInfo _addExtensions;
        private readonly MethodInfo _tryGetExtensionType;

        internal Assembly Engine { get; }

        private EngineSurface(Assembly engine, Type parserSettings, MethodInfo parse, Type templateOptions,
            Type exType, ConstructorInfo compileContext, ConstructorInfo compileScope, MethodInfo compile,
            PropertyInfo scopeMap, PropertyInfo scopeMapEntries, Type entryType, MethodInfo loadExtensions,
            MethodInfo addExtensions, MethodInfo tryGetExtensionType)
        {
            Engine = engine;
            _parserSettings = parserSettings;
            _parse = parse;
            _templateOptions = templateOptions;
            _exType = exType;
            _compileContext = compileContext;
            _compileScope = compileScope;
            _compile = compile;
            _scopeMap = scopeMap;
            _scopeMapEntries = scopeMapEntries;
            _entryOffset = entryType.GetProperty("Offset");
            _entryLength = entryType.GetProperty("Length");
            _entryModelType = entryType.GetProperty("ModelType");
            _entryChainedType = entryType.GetProperty("ChainedType");
            _exTypeIsDynamic = exType.GetProperty("IsDynamic");
            _exTypeType = exType.GetProperty("Type");
            _loadExtensions = loadExtensions;
            _addExtensions = addExtensions;
            _tryGetExtensionType = tryGetExtensionType;
        }

        private const BindingFlags Any =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        /// <summary>Binds the surface against a loaded engine, or returns <c>null</c> with a reason. A missing
        /// member means this engine build is not one this generator knows how to drive, which is a tier and never
        /// an error.</summary>
        internal static EngineSurface TryBind(Assembly engine, out string failure)
        {
            failure = null;
            try
            {
                var parserSettings = engine.GetType("Heddle.Language.ParserSettings", false);
                var parser = engine.GetType("Heddle.Language.DocumentParser", false);
                var templateOptions = engine.GetType("Heddle.Data.TemplateOptions", false);
                var exType = engine.GetType("Heddle.Data.ExType", false);
                var compileContextType = engine.GetType("Heddle.Runtime.CompileContext", false);
                var compileScopeType = engine.GetType("Heddle.Runtime.CompileScope", false);
                var csharpContextType = engine.GetType("Heddle.Runtime.CSharpContext", false);
                var compilerType = engine.GetType("Heddle.Runtime.HeddleCompiler", false);
                var parseContextType = engine.GetType("Heddle.Language.ParseContext", false);
                var factoryType = engine.GetType("Heddle.Runtime.TemplateFactory", false);
                if (parserSettings == null || parser == null || templateOptions == null || exType == null ||
                    compileContextType == null || compileScopeType == null || csharpContextType == null ||
                    compilerType == null || parseContextType == null || factoryType == null)
                {
                    failure = "the loaded engine does not carry the compile pipeline this generator drives";
                    return null;
                }

                MethodInfo parse = null;
                foreach (var candidate in parser.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var parameters = candidate.GetParameters();
                    // nameof over this assembly's own linked copy of the same front end: the loaded engine's
                    // method has the same name because it is the same source, so a rename breaks this compile
                    // rather than silently finding nothing at run time.
                    if (candidate.Name == nameof(Heddle.Language.DocumentParser.Parse) && parameters.Length == 3 &&
                        parameters[1].ParameterType == parserSettings)
                    {
                        parse = candidate;
                        break;
                    }
                }

                var compileContext = compileContextType.GetConstructor(new[] { templateOptions, exType });
                var compileScope = compileScopeType.GetConstructor(new[] { compileContextType, csharpContextType });
                var compile = compilerType.GetMethod("Compile", Any, null,
                    new[] { typeof(string), compileScopeType, parseContextType, exType }, null);
                var scopeMap = compileContextType.GetProperty("ScopeMap", Any);
                if (parse == null || compileContext == null || compileScope == null || compile == null ||
                    scopeMap == null)
                {
                    failure = "the loaded engine's compile pipeline has a shape this generator cannot drive";
                    return null;
                }

                var entries = scopeMap.PropertyType.GetProperty("Entries", Any);
                var entryType = engine.GetType("Heddle.Runtime.ScopeMapEntry", false);
                if (entries == null || entryType == null)
                {
                    failure = "the loaded engine records no scope map";
                    return null;
                }

                var loadExtensions = factoryType.GetMethod("LoadAddExtensionsFromAssembly",
                    BindingFlags.Public | BindingFlags.Static);
                var addExtensions = factoryType.GetMethod("AddExtensions",
                    BindingFlags.Public | BindingFlags.Static);
                var tryGet = factoryType.GetMethod("TryGetExtensionType", Any);
                if (loadExtensions == null || addExtensions == null || tryGet == null)
                {
                    failure = "the loaded engine's extension registry has a shape this generator cannot read";
                    return null;
                }

                return new EngineSurface(engine, parserSettings, parse, templateOptions, exType, compileContext,
                    compileScope, compile, scopeMap, entries, entryType, loadExtensions, addExtensions, tryGet);
            }
            catch (Exception e)
            {
                failure = "the loaded engine could not be bound: " + e.Message;
                return null;
            }
        }

        /// <summary>
        /// Registers an assembly's <c>[ExtensionName]</c> types with the loaded engine's process-global registry,
        /// <b>one at a time</b>. The registry builds its batch on a copy and throws before publishing any of it, so
        /// a single pair of unrelated types claiming one name would otherwise cost every other extension in the
        /// assembly its registration — and a test fixture that exists to <i>be</i> such a pair would take the whole
        /// compilation's observation with it. One at a time, the conflicting candidate is the only thing refused.
        /// <para>Append-only with no unregister, which is why <see cref="BoundExtensionType"/> exists: what the
        /// registry ends up holding still has to be checked against what this compilation resolved.</para>
        /// </summary>
        internal void Register(Assembly assembly)
        {
            var found = (IEnumerable)_loadExtensions.Invoke(null, new object[] { assembly });
            var elementType = _addExtensions.GetParameters()[0].ParameterType.GetGenericArguments()[0];
            foreach (var candidate in found)
            {
                var batch = Array.CreateInstance(elementType, 1);
                batch.SetValue(candidate, 0);
                try
                {
                    _addExtensions.Invoke(null, new object[] { batch });
                }
                catch (Exception)
                {
                    // Two unrelated types claiming one name. The engine refuses the later one and so does this;
                    // what the registry holds is then checked against the compilation either way.
                }
            }
        }

        /// <summary>The type the loaded registry currently binds <paramref name="name"/> to, or <c>null</c>. The
        /// precondition of trusting any observation that involves an extension: the registry is process-global and
        /// append-only, so a name another compilation bound to a different type must be refused rather than
        /// answered.</summary>
        internal Type BoundExtensionType(string name)
        {
            var arguments = new object[] { name, null };
            var found = _tryGetExtensionType.Invoke(null, arguments);
            return found is bool && (bool)found ? (Type)arguments[1] : null;
        }

        /// <summary>A type by metadata name out of the engine assembly itself, for the cases where a model or a
        /// prop names one of the engine's own types rather than one of the compilation's.</summary>
        internal Type ResolveType(string metadataName) => Engine.GetType(metadataName, false);

        /// <summary>One recorded body-compile span with the types the engine actually compiled it against.</summary>
        internal readonly struct Entry
        {
            internal Entry(int offset, int length, Type model, bool modelDynamic, Type chained, bool chainedDynamic)
            {
                Offset = offset;
                Length = length;
                Model = model;
                ModelDynamic = modelDynamic;
                Chained = chained;
                ChainedDynamic = chainedDynamic;
            }

            internal int Offset { get; }
            internal int Length { get; }
            internal Type Model { get; }
            internal bool ModelDynamic { get; }
            internal Type Chained { get; }
            internal bool ChainedDynamic { get; }
        }

        /// <summary>
        /// Compiles one document through the loaded engine and returns what its scope map recorded.
        /// <para>Bounded by a wait, not by a sandbox: an extension's hook runs with the compiler's privileges and
        /// cannot be unloaded, so the only thing this can promise is that a hook which never returns stops costing
        /// the build after <paramref name="timeout"/>.</para>
        /// </summary>
        internal IReadOnlyList<Entry> TryObserve(string document, Type modelType, string rootPath,
            object outputProfile, object expressionMode, bool trimDirectiveLines, int maxRecursionCount,
            Func<string, string> importReader, Func<string, string> importIdentifier, TimeSpan timeout,
            out string failure)
        {
            List<Entry> result = null;
            string inner = null;
            var done = new ManualResetEventSlim(false);
            var worker = new Thread(() =>
            {
                try
                {
                    result = Observe(document, modelType, rootPath, outputProfile, expressionMode,
                        trimDirectiveLines, maxRecursionCount, importReader, importIdentifier);
                }
                catch (Exception e)
                {
                    inner = (e.InnerException ?? e).Message;
                }
                finally
                {
                    done.Set();
                }
            });
            worker.IsBackground = true;
            worker.Start();

            if (!done.Wait(timeout))
            {
                failure = "the observed compile did not finish within " +
                          ((int)timeout.TotalMilliseconds).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                          " ms";
                return null;
            }

            failure = inner;
            return result;
        }

        private List<Entry> Observe(string document, Type modelType, string rootPath, object outputProfile,
            object expressionMode, bool trimDirectiveLines, int maxRecursionCount,
            Func<string, string> importReader, Func<string, string> importIdentifier)
        {
            var settings = Activator.CreateInstance(_parserSettings);
            SetProperty(_parserSettings, settings, "RootPath", rootPath ?? string.Empty);
            SetProperty(_parserSettings, settings, "ProvideLanguageFeatures", true);
            SetProperty(_parserSettings, settings, "ImportReader", importReader);
            SetProperty(_parserSettings, settings, "ImportIdentifier", importIdentifier);

            var parseArguments = new object[] { document, settings, null };
            var parseContext = _parse.Invoke(null, parseArguments);

            var options = Activator.CreateInstance(_templateOptions);
            SetProperty(_templateOptions, options, "RootPath", rootPath ?? string.Empty);
            SetProperty(_templateOptions, options, "ProvideLanguageFeatures", true);
            SetProperty(_templateOptions, options, "OutputProfile", outputProfile);
            SetProperty(_templateOptions, options, "ExpressionMode", expressionMode);
            SetProperty(_templateOptions, options, "TrimDirectiveLines", trimDirectiveLines);
            SetProperty(_templateOptions, options, "MaxRecursionCount", maxRecursionCount);

            var model = modelType == null
                ? null
                : Activator.CreateInstance(_exType, new object[] { modelType });
            var compileContext = _compileContext.Invoke(new[] { options, model });
            var compileScope = _compileScope.Invoke(new[] { compileContext, null });
            _compile.Invoke(null, new[] { document, compileScope, parseContext, null });

            var scopeMap = _scopeMap.GetValue(compileContext);
            var entries = new List<Entry>();
            if (scopeMap == null)
                return entries;

            foreach (var entry in (IEnumerable)_scopeMapEntries.GetValue(scopeMap))
            {
                var modelValue = _entryModelType.GetValue(entry);
                var chainedValue = _entryChainedType.GetValue(entry);
                entries.Add(new Entry(
                    (int)_entryOffset.GetValue(entry),
                    (int)_entryLength.GetValue(entry),
                    TypeOf(modelValue), IsDynamic(modelValue),
                    TypeOf(chainedValue), IsDynamic(chainedValue)));
            }

            return entries;
        }

        internal object ParseEnum(string typeMetadataName, string memberName)
        {
            var type = Engine.GetType(typeMetadataName, false);
            if (type == null || !type.IsEnum)
                return null;
            foreach (var name in Enum.GetNames(type))
                if (string.Equals(name, memberName, StringComparison.Ordinal))
                    return Enum.Parse(type, name, false);
            return null;
        }

        private Type TypeOf(object exType) => exType == null ? null : (Type)_exTypeType.GetValue(exType);

        private bool IsDynamic(object exType) => exType != null && (bool)_exTypeIsDynamic.GetValue(exType);

        private static void SetProperty(Type type, object instance, string name, object value)
        {
            var property = type.GetProperty(name);
            if (property != null && property.CanWrite)
                property.SetValue(instance, value);
        }
    }
}
