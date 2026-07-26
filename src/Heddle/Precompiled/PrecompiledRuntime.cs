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
        // The renderer grows as needed; the initial capacity is a perf hint only and never changes output bytes.
        // (The size-adaptive per-template high-water mark HeddleTemplate.Generate keeps is a future refinement.)
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

        /// <summary>Bind for definition call sites. Constructs the engine-internal definition carrier
        /// itself — <c>DefinitionBaseExtension</c> is internal to <c>Heddle</c>, so generated code never names it (the
        /// same public-entry/internal-access posture as <see cref="Bind{TExtension}"/>) — and returns it as
        /// <see cref="AbstractExtension"/>. The outer carrier's body is the invocation-site caller content; its
        /// <c>DefinitionParameterTemplate</c> is an inner carrier whose body is the definition body. Props are
        /// installed on the definition-body scope (the frozen prototype shared when all-constant; each dynamic setter
        /// runs against the caller view). Both carriers get the baked recursion limit
        /// (<paramref name="maxRecursionCount"/> = the build's <c>HeddleMaxRecursionCount</c>) that <c>InitStart</c>
        /// would otherwise read from options — build wins over runtime options.</summary>
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

        /// <summary>
        /// Per-carrier locals overload. The two carriers a definition call site builds
        /// host <b>different</b> documents — the inner carrier the definition body, the outer carrier the invocation
        /// site's caller content — and the dynamic tier derives each one's frame-provisioning flag from its own
        /// <c>RuntimeDocument.NeedsLocals</c> (<c>AbstractExtension.InitStart</c>). The older overloads apply one
        /// flag to both, which suppresses the deliberate frame-<i>clearing</i> a non-participating body gets under a
        /// provisioned parent (<c>AbstractExtension.GetInnerResult</c>) — that is a behavior change, not a harmless
        /// over-provision, so the flags are split here.
        /// <para>The existing overloads are retained and unchanged: assemblies emitted by older generator versions
        /// keep binding, and passing the same value twice is byte-identical to them.</para>
        /// </summary>
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
            // The outer carrier's body is the caller content; it is pre-rendered onto the chained channel (non-slot)
            // or projected lazily via the SlotContent carrier (slot mode).
            outer.BindPrecompiled(callerContent, renderType, callerContentNeedsLocals, position);
            outer.SetMaxRecursion(maxRecursionCount);
            outer.SlotMode = slotMode;
            if (props != null || (dynamicSetters != null && dynamicSetters.Length != 0))
                outer.SetPrecompiledProps(props, dynamicSetters);
            return outer;
        }

        /// <summary>Bind for parameter-declaring extension call sites. Constructs the engine-internal
        /// <c>ExtensionParameterCarrier</c> itself (it is internal to <c>Heddle</c> — the same public-entry/
        /// internal-access posture as <see cref="BindDefinition"/>) and returns it as <see cref="AbstractExtension"/>.
        /// The <paramref name="renderType"/> — the generator-computed <c>Encode</c>/<c>Raw</c> derived from the
        /// extension's <c>[EncodeOutput]</c>/<c>[NotEncode]</c> — is applied to the <b>inner</b> extension via
        /// <c>BindPrecompiled</c>, exactly as the dynamic tier's <c>InitializeTemplate</c> → <c>SetUpRenderType</c>
        /// lands it before the carrier wraps — the carrier is transparent: an <c>[EncodeOutput]</c> inner self-encodes
        /// on both tiers. Called once from a generated static initializer (thread-safe via CLR type-init); nothing
        /// is mutated after it returns.</summary>
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

        /// <summary>Binds an <see cref="OutExtension"/> call site. <paramref name="slotMode"/> puts
        /// the carrier in slot-projection mode — a slot-declaring definition body's <c>@out(value)</c> projects the
        /// invocation-site caller content through the <c>SlotContent</c> carrier instead of splicing the pre-rendered
        /// chained content — reproducing what <c>OutExtension.InitStart</c> derives from
        /// <c>CompileContext.SlotParameterType</c> (which <c>BindDefinition</c> bypasses). The dynamic path is
        /// unchanged: this setter is only reached from generated static initializers.</summary>
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

        // Wrap the sink in the budget seam when the ambient options carry a RenderBudget; otherwise return the
        // bare sink unchanged (null path = no wrapper, no allocation). The wrapper enforces the same limits the
        // dynamic engine's HeddleTemplate.Generate does, so both backends throw identically.
        private static IScopeRenderer WithBudget(IScopeRenderer sink)
        {
            var budget = _ambientOptions?.RenderBudget;
            return budget == null ? sink : new BudgetedRenderer(sink, budget);
        }

        /// <summary>The single piece-write hook for generated bodies: writes the pre-encoded u8 twin when
        /// the scope's renderer is a UTF-8 sink, the string form otherwise — including under encode proxies, which are
        /// deliberately not <see cref="IUtf8ScopeRenderer"/>, so pre-encoded bytes never bypass an active
        /// proxy. The only u8/string decision point in generated code.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePiece(in Scope scope, string piece, ReadOnlySpan<byte> utf8Piece)
        {
            if (scope.Renderer is IUtf8ScopeRenderer u8)
                u8.RenderUtf8(utf8Piece);
            else
                scope.Renderer.Render(piece);
        }

        // The ambient options a generated @partial resolves against. Thread-static so concurrent renders
        // never share it; read only by ResolvePartial, so the dynamic path is untouched.
        [ThreadStatic] private static TemplateOptions _ambientOptions;

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
        /// One dynamic member hop — the single implementation of the dynamic tier's member access, shared by the
        /// engine and by generated precompiled code.
        /// <para>A <c>null</c> receiver propagates <c>null</c>, exactly like the engine's per-hop
        /// <c>Condition(input == null, null, Dynamic(GetMember …))</c>, so a chain of these calls reproduces the
        /// dynamic member path hop for hop.</para>
        /// <para><b>Binder context.</b> The member is bound in <b>Heddle's</b> assembly context, not the calling
        /// assembly's. Generated code used to emit an inline <c>(dynamic)</c> cast chain, which binds in the
        /// consumer's context and therefore resolved the consumer's <c>internal</c> members — members the engine's
        /// own dynamic tier cannot see. Routing both tiers through this helper makes that choice exist once.
        /// Note the deliberate asymmetry it preserves: the <i>typed</i> member tier accepts an <c>internal</c>
        /// getter regardless of assembly, while the dynamic tier does not. Reproducing the engine's behavior is
        /// deliberate; harmonizing the two tiers would widen visibility and is a breaking change.</para>
        /// <para>Thread-safe: call sites are cached per member name and the DLR's own polymorphic inline cache
        /// handles the per-receiver-type dispatch.</para>
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
            // typeof(Runtime.Parameters.DynamicParameter) is the engine's own binder context — the same type the
            // dynamic tier passes, so both tiers see the identical member set.
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
