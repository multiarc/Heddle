using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Heddle.Precompiled
{
    /// <summary>
    /// Support entry points called by generated precompiled-template code. Not intended for
    /// hand-written use. Public entry, internal access to the engine-private state the generated code cannot reach
    /// directly (the extension body fields, the internal root <see cref="Scope"/> ctor, <c>PropsData</c>/<c>RootData</c>).
    /// </summary>
    public static class PrecompiledRuntime
    {
        // Initial capacity is a perf hint only; the renderer grows as needed and never changes output bytes.
        private const int DefaultBufferCapacity = 256;

        /// <summary>InitStart-equivalent: installs a generated body on a pre-constructed extension.
        /// <paramref name="needsLocals"/> routes <c>ScopeLocals</c> frame provisioning through the engine's
        /// render protocol. Called only from generated static initializers (thread-safe via CLR type-init); the
        /// extension is never mutated after <c>Bind</c> returns.</summary>
        public static TExtension Bind<TExtension>(TExtension extension, IProcessStrategy body,
            RenderType renderType, bool needsLocals, int line, int column)
            where TExtension : AbstractExtension
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));
            extension.BindPrecompiled(body, renderType, needsLocals, new BlockPosition(line, column));
            return extension;
        }

        /// <summary>Bind for definition call sites. Constructs the engine-internal definition carrier and
        /// returns it as <see cref="AbstractExtension"/>. Outer carrier body is caller content; inner carrier's body
        /// is the definition body. Props install on definition-body scope. The build's
        /// <paramref name="maxRecursionCount"/> (= <c>HeddleMaxRecursionCount</c>) takes precedence over runtime options.</summary>
        public static AbstractExtension BindDefinition(IProcessStrategy body, IProcessStrategy callerContent,
            object[] props, PrecompiledPropSetter[] dynamicSetters, RenderType renderType, bool needsLocals,
            int maxRecursionCount, int line, int column)
            => BindDefinition(body, callerContent, props, dynamicSetters, renderType, needsLocals, false,
                maxRecursionCount, line, column);

        /// <summary>Slot-aware overload: <paramref name="slotMode"/> installs the <c>SlotContent</c> carrier instead
        /// of pre-rendering the caller content, so a slot-mode <c>@out(expr)</c> projects the caller body
        /// lazily.</summary>
        public static AbstractExtension BindDefinition(IProcessStrategy body, IProcessStrategy callerContent,
            object[] props, PrecompiledPropSetter[] dynamicSetters, RenderType renderType, bool needsLocals,
            bool slotMode, int maxRecursionCount, int line, int column)
            => BindDefinition(body, callerContent, props, dynamicSetters, renderType, needsLocals, needsLocals,
                slotMode, maxRecursionCount, line, column);

        /// <summary>Per-carrier locals overload. The two carriers host different documents (inner = definition body,
        /// outer = caller content) with independent frame-provisioning flags from <c>RuntimeDocument.NeedsLocals</c>.
        /// Older overloads apply one flag to both, which suppresses deliberate frame-clearing under provisioned parents
        /// — a behavior change. Old overloads remain for compatibility with assemblies from older generator versions.</summary>
        /// <param name="bodyNeedsLocals">Frame provisioning for the definition <b>body</b> (the inner carrier).</param>
        /// <param name="callerContentNeedsLocals">Frame provisioning for the invocation site's <b>caller content</b>
        /// (the outer carrier).</param>
        public static AbstractExtension BindDefinition(IProcessStrategy body, IProcessStrategy callerContent,
            object[] props, PrecompiledPropSetter[] dynamicSetters, RenderType renderType, bool bodyNeedsLocals,
            bool callerContentNeedsLocals, bool slotMode, int maxRecursionCount, int line, int column)
        {
            var position = new BlockPosition(line, column);

            var inner = new DefinitionBaseExtension();
            inner.BindPrecompiled(body, renderType, bodyNeedsLocals, position);
            inner.SetMaxRecursion(maxRecursionCount);

            var outer = new DefinitionBaseExtension { DefinitionParameterTemplate = inner };
            outer.BindPrecompiled(callerContent, renderType, callerContentNeedsLocals, position);
            outer.SetMaxRecursion(maxRecursionCount);
            outer.SlotMode = slotMode;
            if (props != null || (dynamicSetters != null && dynamicSetters.Length != 0))
                outer.SetPrecompiledProps(props, dynamicSetters);
            return outer;
        }

        /// <summary>Bind for parameter-declaring extension call sites. Constructs and returns an engine-internal
        /// <c>ExtensionParameterCarrier</c> as <see cref="AbstractExtension"/>. The <paramref name="renderType"/>
        /// (from extension's <c>[EncodeOutput]</c>/<c>[NotEncode]</c>) applies to the inner extension; the carrier
        /// is transparent to render type. Called once from generated static initializer (thread-safe via CLR type-init);
        /// nothing mutated after return.</summary>
        public static AbstractExtension BindExtension<TExtension>(TExtension extension, object[] props,
            PrecompiledPropSetter[] dynamicSetters, string[] parameterNames, RenderType renderType, bool needsLocals,
            int line, int column)
            where TExtension : AbstractExtension
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));
            if (parameterNames == null)
                throw new ArgumentNullException(nameof(parameterNames));

            extension.BindPrecompiled(null, renderType, needsLocals, new BlockPosition(line, column));
            var carrier = new ExtensionParameterCarrier(extension, props, dynamicSetters,
                new ExtensionParameterMap(parameterNames));
            return carrier;
        }

        /// <summary>Binds an <see cref="OutExtension"/> call site. <paramref name="slotMode"/> puts the carrier
        /// in slot-projection mode: <c>@out(value)</c> projects caller content through <c>SlotContent</c> carrier
        /// instead of splicing pre-rendered chained content. Only reached from generated static initializers.</summary>
        public static Heddle.Extensions.OutExtension BindOut(Heddle.Extensions.OutExtension extension, bool slotMode,
            int line, int column)
        {
            if (extension == null)
                throw new ArgumentNullException(nameof(extension));
            extension.BindPrecompiled(null, RenderType.Raw, false, new BlockPosition(line, column));
            if (slotMode)
                extension.SetPrecompiledSlotMode();
            return extension;
        }

        /// <summary>The engine's <c>ScopeLocals</c>-provisioning decorator, for the one body <c>Bind</c>
        /// never sees: a document root hosting branch participants. Wraps <paramref name="body"/> so every
        /// Render/Execute runs under a fresh frame; roots without participants stay unwrapped.</summary>
        public static IProcessStrategy WithLocalsFrame(IProcessStrategy body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));
            return new LocalsFrameStrategy(body);
        }

        /// <summary>Indexed prop read for generated definition bodies (<c>Scope.PropsData</c> is internal).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Prop(in Scope scope, int index)
        {
            var props = scope.PropsData;
            return props != null && index >= 0 && index < props.Length ? props[index] : null;
        }

        /// <summary>Root-model read for <c>@root</c>-anchored paths (<c>Scope.RootData</c> is internal).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object RootModel(in Scope scope) => scope.RootData;

        /// <summary>
        /// The value an unnamed carrier hands on — <see cref="Heddle.Extensions.EmptyExtension"/>'s own
        /// pass-through, reproduced for generated code that flattens a one-item chain parameter
        /// (<c>@list(len(Name))</c>, <c>@card((Cols))</c>) instead of building the carrier.
        /// <para>The carrier renders its input, so what the next link receives is <b>text</b>, never the producer's
        /// own value. Handing on the raw value instead is invisible wherever the consumer just prints it and
        /// decisive wherever the consumer is type-sensitive: <c>@list</c> over an <c>int</c> iterated nothing where
        /// the engine iterates the digits, and a definition typed <c>:: System.String</c> threw
        /// <see cref="System.InvalidCastException"/> at render.</para>
        /// </summary>
        public static string CarrierValue(object value)
        {
            if (value == null)
                return string.Empty;
            return value as string ?? value.ToString();
        }

        /// <summary>Root entry: renderer creation and the internal root <see cref="Scope"/> ctor, mirroring
        /// <c>HeddleTemplate.Generate</c>. The root frame (when the document hosts branch participants) rides the
        /// strategy via <see cref="WithLocalsFrame"/>, so no locals are seeded here.</summary>
        public static string GenerateString(IProcessStrategy root, object model, object chained, object callerData)
            => GenerateString(root, model, chained, callerData, null);

        /// <summary>Options-carrying overload. Establishes the ambient
        /// <see cref="TemplateOptions"/> that generated <c>@partial</c> code consults through
        /// <see cref="ResolvePartial(string)"/> for its registry-then-dynamic-compile resolution. A <c>null</c>
        /// <paramref name="options"/> inherits the current ambient (so a partial rendered inside this render keeps the
        /// outermost options); the resolver adapter passes the request's options, the typed entry passes <c>null</c>
        /// (ambient defaults to a fresh <see cref="TemplateOptions"/>). Setting the ambient never changes the dynamic
        /// path — it is only read by <see cref="ResolvePartial(string)"/>.</summary>
        public static string GenerateString(IProcessStrategy root, object model, object chained, object callerData,
            TemplateOptions options)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            var previous = _ambientOptions;
            if (options != null)
                _ambientOptions = options;
            try
            {
                var renderer = new ScopeRenderer(DefaultBufferCapacity);
                renderer.SetOutputEncoder(_ambientOptions?.Encoder);   // stamp the effective encoder on the sink
                var scope = new Scope(model, callerData, model, chained, WithBudget(renderer), null, null);
                root.Render(scope);
                var result = renderer.ToString();
                renderer.Clear();
                return result;
            }
            finally
            {
                _ambientOptions = previous;
            }
        }

        /// <summary>Renders a precompiled root strategy into a <see cref="TextWriter"/> sink. Generated
        /// sink entry point. Constructs the matching renderer, builds the root <see cref="Scope"/> exactly as
        /// <see cref="GenerateString(IProcessStrategy,object,object,object)"/> does, and calls <c>root.Render</c> — no
        /// locals provisioning of its own (the frame rides the strategy). The caller owns the writer: no flush,
        /// no dispose.</summary>
        public static void GenerateToWriter(IProcessStrategy root, object model, object chained, object callerData,
            TextWriter writer)
            => GenerateToWriter(root, model, chained, callerData, writer, null);

        /// <summary>Options-carrying overload; mirrors <see cref="GenerateString"/>.</summary>
        internal static void GenerateToWriter(IProcessStrategy root, object model, object chained, object callerData,
            TextWriter writer, TemplateOptions options)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            var previous = _ambientOptions;
            if (options != null)
                _ambientOptions = options;
            try
            {
                var renderer = new TextWriterScopeRenderer(writer);
                renderer.SetOutputEncoder(_ambientOptions?.Encoder);   // stamp the effective encoder on the sink
                var scope = new Scope(model, callerData, model, chained, WithBudget(renderer), null, null);
                root.Render(scope);
            }
            finally
            {
                _ambientOptions = previous;
            }
        }

        /// <summary>Renders a precompiled root strategy into a UTF-8 <see cref="IBufferWriter{T}"/> of
        /// <see cref="byte"/>. Generated sink entry point. Opted-in u8 pieces flow to the writer through
        /// <see cref="WritePiece"/>'s zero-transcode branch; everything else transcodes via the renderer. The
        /// caller owns the writer: no flush, no complete, no dispose.</summary>
        public static void GenerateUtf8(IProcessStrategy root, object model, object chained, object callerData,
            IBufferWriter<byte> writer)
            => GenerateUtf8(root, model, chained, callerData, writer, null);

        /// <summary>Options-carrying overload; mirrors <see cref="GenerateString"/>.</summary>
        internal static void GenerateUtf8(IProcessStrategy root, object model, object chained, object callerData,
            IBufferWriter<byte> writer, TemplateOptions options)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            var previous = _ambientOptions;
            if (options != null)
                _ambientOptions = options;
            try
            {
                var renderer = new Utf8ScopeRenderer(writer);
                renderer.SetOutputEncoder(_ambientOptions?.Encoder);   // stamp the effective encoder on the sink
                var scope = new Scope(model, callerData, model, chained, WithBudget(renderer), null, null);
                root.Render(scope);
            }
            finally
            {
                _ambientOptions = previous;
            }
        }

        // Wrapper enforces same render budget limits as dynamic engine — both backends throw identically.
        private static IScopeRenderer WithBudget(IScopeRenderer sink)
        {
            var budget = _ambientOptions?.RenderBudget;
            return budget == null ? sink : new BudgetedRenderer(sink, budget);
        }

        /// <summary>Writes pre-encoded u8 for UTF-8 sinks, string otherwise. Encode proxies are deliberately not
        /// <see cref="IUtf8ScopeRenderer"/> so pre-encoded bytes never bypass a proxy. The only u8/string decision
        /// point in generated code.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePiece(in Scope scope, string piece, ReadOnlySpan<byte> utf8Piece)
        {
            if (scope.Renderer is IUtf8ScopeRenderer u8)
                u8.RenderUtf8(utf8Piece);
            else
                scope.Renderer.Render(piece);
        }

        // Thread-static so concurrent renders never share ambient options; dynamic path only reads this, untouched.
        [ThreadStatic] private static TemplateOptions _ambientOptions;

        /// <summary>The function registry this render binds late-bound call sites against: the ambient request's
        /// own <see cref="TemplateOptions.Functions"/>, or the frozen default set the dynamic tier's compiler
        /// falls back to for the same input (<c>NativeExpressionCompiler</c>'s
        /// <c>Options.Functions ?? FunctionRegistry.Default</c>). Reading it allocates nothing.</summary>
        internal static Runtime.Expressions.FunctionRegistry EffectiveFunctions =>
            _ambientOptions?.Functions ?? Runtime.Expressions.FunctionRegistry.Default;

        /// <summary>Establishes <paramref name="options"/> as the ambient request for a render this class does not
        /// itself drive — the resolver's precompiled adapter calls <c>IProcessStrategy.Render</c> directly, so
        /// without this a late-bound site would bind against the default registry while the gauntlet had just
        /// validated the request's own. Returns the previous ambient for <see cref="LeaveAmbient"/>; the pair is
        /// a plain field swap and allocates nothing.</summary>
        internal static TemplateOptions EnterAmbient(TemplateOptions options)
        {
            var previous = _ambientOptions;
            if (options != null)
                _ambientOptions = options;
            return previous;
        }

        /// <summary>Restores the ambient request <see cref="EnterAmbient"/> displaced.</summary>
        internal static void LeaveAmbient(TemplateOptions previous) => _ambientOptions = previous;

        /// <summary>Registry-then-dynamic-compile partial resolution against the ambient options, dynamic child
        /// model. Called from generated <c>@partial</c> code in a dynamic-tier body, memoized once via
        /// <c>LazyInitializer</c>.</summary>
        public static IProcessStrategy ResolvePartial(string key)
            => ResolvePartialCore(key, _ambientOptions ?? new TemplateOptions(), null);

        /// <summary>Registry-then-dynamic-compile partial resolution against the ambient options, typing a
        /// dynamically-compiled child by <paramref name="callerModelType"/> — the caller-site model type the runtime
        /// <c>PartialExtension</c> compiles the child against (<c>null</c> = the dynamic tier). Called from generated
        /// <c>@partial</c> code in a typed body.</summary>
        public static IProcessStrategy ResolvePartial(string key, Type callerModelType)
            => ResolvePartialCore(key, _ambientOptions ?? new TemplateOptions(),
                callerModelType == null ? null : new ExType(callerModelType));

        /// <summary>
        /// The one-time evaluation of a computed <c>@partial</c> name, called from a generated static field
        /// initializer. Reproduces <c>PartialExtension.InitStart</c>: the name body renders against
        /// <see cref="Scope.Null"/> and the trimmed result is the resolved name — a fault during that render is the
        /// engine's HED0005 compile fault for the call, captured here (never thrown, so type initialization always
        /// succeeds) and re-raised by <see cref="PrecompiledPartialName.Get"/> at render.
        /// </summary>
        /// <param name="nameBody">The compiled name-body strategy (wrapped in a locals frame when it needs one).</param>
        /// <param name="positionStart">The <c>@partial</c> call's start offset in the template document.</param>
        /// <param name="positionLength">The call's length — with <paramref name="positionStart"/>, the position the
        /// engine gives the fault.</param>
        public static PrecompiledPartialName EvaluatePartialName(IProcessStrategy nameBody, int positionStart,
            int positionLength)
        {
            if (nameBody == null)
                throw new ArgumentNullException(nameof(nameBody));
            try
            {
                var name = nameBody.Execute(Scope.Null);
                return new PrecompiledPartialName(name == null ? string.Empty : name.Trim());
            }
            catch (Exception e)
            {
                // HeddleCompiler.CompileItemFault's exact shape for the '@partial' subject; the differential suite
                // pins the two byte-for-byte.
                return new PrecompiledPartialName(new HeddleCompileError
                {
                    Exception = e,
                    Position = new BlockPosition(positionStart, positionLength),
                    DiagnosticId = HeddleDiagnosticIds.CompilationFailed,
                    Error = "Compiling '@partial' failed: " + e.Message
                });
            }
        }

        /// <summary>Registry-then-dynamic-compile partial resolution: a registered precompiled entry wins
        /// (mixed mode — a precompiled template renders a precompiled partial); otherwise the named template compiles
        /// dynamically under <paramref name="options"/> against the dynamic tier (a precompiled template renders a
        /// runtime-compiled partial). Thread-safe; generated call sites memoize the result. Returns a strategy whose
        /// render appends the partial's output — byte-identical to the runtime <c>PartialExtension</c>'s
        /// <c>InnerTemplate.Generate</c> splice.</summary>
        public static IProcessStrategy ResolvePartial(string key, TemplateOptions options)
            => ResolvePartialCore(key, options ?? new TemplateOptions(), null);

        private static IProcessStrategy ResolvePartialCore(string key, TemplateOptions options, ExType childModelType)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (PrecompiledTemplates.TryGet(key, out var entry) && entry.IsPrecompiled)
                return entry.Strategy;

            var opts = options ?? new TemplateOptions();
            var templateName = TemplateKey.StripTemplateExtension(key);
            var childOptions = new TemplateOptions(opts, templateName);
            var template = new Heddle.HeddleTemplate(new CompileContext(childOptions, childModelType ?? ExType.Dynamic));
            if (!template.CompileResult.Success)
                throw new TemplateCompileException(template.CompileResult.ErrorList);
            return new DynamicPartialStrategy(template);
        }

        /// <summary>Wraps a dynamically-compiled partial template as an <see cref="IProcessStrategy"/> so a precompiled
        /// body can render it uniformly. Delegates to <c>HeddleTemplate.Generate</c> with the current model/chained,
        /// mirroring <c>PartialExtension</c>.</summary>
        private sealed class DynamicPartialStrategy : IProcessStrategy
        {
            private readonly Heddle.HeddleTemplate _template;
            public DynamicPartialStrategy(Heddle.HeddleTemplate template) => _template = template;

            public string Execute(in Scope scope) =>
                _template.Generate(scope.ModelData, scope.ChainedData) ?? string.Empty;

            public void Render(in Scope scope) =>
                scope.Renderer.Render(_template.Generate(scope.ModelData, scope.ChainedData));
        }

        /// <summary>
        /// The engine's member-path accessor for one value node, built once from a generated static field
        /// initializer. Resolves <paramref name="segments"/> off <paramref name="startType"/> through the engine's
        /// own member walk (same visibility filter, same hop order) and compiles the engine's null-safe hop chain —
        /// so the value is byte-identical to the dynamic tier's by construction, including members generated C#
        /// could not name (an <c>internal</c> getter of a referenced assembly, a member type with no writable name).
        /// The returned delegate performs one call per render and allocates nothing beyond what the engine's own
        /// accessor allocates (a box for a value-type result).
        /// </summary>
        /// <param name="startType">The type the walk starts at; the input object is converted to it exactly as the
        /// engine converts, so a mismatched value throws the same <see cref="InvalidCastException"/>.</param>
        /// <param name="segments">The member path, one property name per hop.</param>
        /// <returns>A delegate from the boxed start value to the boxed path value; a <c>null</c> receiver
        /// propagates <c>null</c> per the engine's hop rule.</returns>
        /// <exception cref="ArgumentException">The path does not resolve under the engine's member walk — the
        /// build emitted this call against a different shape of <paramref name="startType"/> than the one loaded,
        /// which is a host deployment fault, not a template fault.</exception>
        public static Func<object, object> MemberAccessor(Type startType, string[] segments)
        {
            var resolution = ResolveEngineChain(startType, segments);
            return Runtime.Parameters.ModelParameter.GetPropertyChainAccessor(resolution.Properties).Compile();
        }

        /// <summary>
        /// The engine's native-expression compilation for a path-shaped value node, built once from a generated
        /// static field initializer. Mirrors the dynamic tier's expression compiler for a model-rooted or
        /// <c>::</c>-rooted path: the same shared member resolution, the same null-safe hop chain over the same
        /// three-channel delegate shape (<c>model</c>, <c>chained</c>, <c>root</c>), boxed to <c>object</c> the way
        /// the engine boxes its expression result. One delegate call per render; no allocation beyond the engine's
        /// own (a box for a value-type result).
        /// </summary>
        /// <param name="startType">The type the path walks from — the scope's model type, or the root model type
        /// for a <c>::</c>-rooted path.</param>
        /// <param name="segments">The member path, one property name per hop.</param>
        /// <param name="rootRef">Whether the path is <c>::</c>-rooted; selects the root channel over the model
        /// channel, as the engine's compiler does.</param>
        /// <exception cref="ArgumentException">The path does not resolve under the engine's member walk — see
        /// <see cref="MemberAccessor"/>.</exception>
        public static Func<object, object, object, object> NativeAccessor(Type startType, string[] segments,
            bool rootRef)
        {
            var resolution = ResolveEngineChain(startType, segments);
            var model = System.Linq.Expressions.Expression.Parameter(typeof(object), "model");
            var chained = System.Linq.Expressions.Expression.Parameter(typeof(object), "chained");
            var root = System.Linq.Expressions.Expression.Parameter(typeof(object), "root");
            var body = Runtime.Parameters.ModelParameter.BuildNullSafePropertyChain(
                rootRef ? root : model, resolution.Properties);
            var boxed = System.Linq.Expressions.Expression.Convert(body, typeof(object));
            return System.Linq.Expressions.Expression
                .Lambda<Func<object, object, object, object>>(boxed, model, chained, root).Compile();
        }

        /// <summary>The engine's member resolution for the two accessors — a host-programming failure throws,
        /// because the generator only emits an accessor for a path it proved the engine resolves.</summary>
        private static Runtime.Expressions.MemberPathResolution ResolveEngineChain(Type startType, string[] segments)
        {
            if (startType == null)
                throw new ArgumentNullException(nameof(startType));
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));
            var resolution = Runtime.Expressions.MemberPathResolver.TryResolve(new ExType(startType), segments);
            if (resolution.Kind != Runtime.Expressions.MemberPathResolutionKind.Resolved)
                throw new ArgumentException(
                    resolution.FailureMessage ??
                    "Member path does not resolve under the engine's member walk: " + string.Join(".", segments));
            return resolution;
        }

        /// <summary>Single dynamic member hop — shared single implementation of dynamic tier's member access.
        /// <para><c>null</c> receiver propagates <c>null</c>, reproducing engine's per-hop behavior.</para>
        /// <para><b>Binder context:</b> bound in Heddle's assembly, not caller's. This prevents seeing caller's
        /// <c>internal</c> members. Asymmetry with typed tier is deliberate and preserved for back-compat: widening
        /// visibility would be a breaking change.</para>
        /// <para>Thread-safe: sites cached per member name; DLR polymorphic inline cache handles dispatch.</para>
        /// </summary>
        /// <param name="receiver">The object to read the member from; <c>null</c> yields <c>null</c>.</param>
        /// <param name="name">The member name, ordinal and case-sensitive.</param>
        /// <returns>The member value, or <c>null</c> when <paramref name="receiver"/> is <c>null</c>.</returns>
        public static object DynamicMember(object receiver, string name)
        {
            if (receiver == null)
                return null;
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            var site = DynamicMemberSites.GetOrAdd(name, CreateMemberSite);
            return site.Target(site, receiver);
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,
                System.Runtime.CompilerServices.CallSite<Func<System.Runtime.CompilerServices.CallSite, object, object>>>
            DynamicMemberSites =
                new System.Collections.Concurrent.ConcurrentDictionary<string,
                    System.Runtime.CompilerServices.CallSite<Func<System.Runtime.CompilerServices.CallSite, object, object>>>(
                    StringComparer.Ordinal);

        private static System.Runtime.CompilerServices.CallSite<Func<System.Runtime.CompilerServices.CallSite, object, object>>
            CreateMemberSite(string name)
        {
            // Same binder context as dynamic tier so both tiers see identical member set.
            var binder = Microsoft.CSharp.RuntimeBinder.Binder.GetMember(
                Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags.None, name,
                typeof(Runtime.Parameters.DynamicParameter),
                new[]
                {
                    Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(
                        Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags.None, null)
                });
            return System.Runtime.CompilerServices.CallSite<Func<System.Runtime.CompilerServices.CallSite, object, object>>
                .Create(binder);
        }

        private sealed class LocalsFrameStrategy : IProcessStrategy
        {
            private readonly IProcessStrategy _inner;

            public LocalsFrameStrategy(IProcessStrategy inner) => _inner = inner;

            public string Execute(in Scope scope) => _inner.Execute(scope.WithLocals(new ScopeLocals()));

            public void Render(in Scope scope) => _inner.Render(scope.WithLocals(new ScopeLocals()));
        }
    }
}
