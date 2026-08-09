using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Language.Expressions;
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

        /// <summary>
        /// Constructs the extension and runs its <b>real</b> compile-time hook against a synthesized compile
        /// scope, supplying the generated body instead of compiling one. Everything <c>InitStart</c> decides is
        /// decided by the extension itself — <c>ListExtension</c>'s count reader, <c>OutExtension</c>'s slot mode and
        /// its five diagnostics, <c>DefinitionBaseExtension</c>'s recursion limit, the render type — rather than being
        /// re-derived at build time. Reproduces <c>HeddleCompiler.InitializeTemplate</c>: the render type comes from
        /// <see cref="RenderTypeRules.Derive"/> over the <b>live</b> type's attributes, <c>SetUpRenderType</c> runs
        /// before the hook, and the delayed-template queue the hook may have appended to is drained exactly as the
        /// engine drains it.
        /// <para><b>A factory, not an instance.</b> <c>new Foo.Bar()</c> written as an argument would run outside
        /// this method's <c>try</c>, so an extension assembly that moved would fault the calling type initializer
        /// before anything could catch it.</para>
        /// <para><b>This method throws only for a null argument.</b> The manifest touches <c>strategy:</c>, so every
        /// call site's initializer runs at registration, and a throwing type initializer is a
        /// <see cref="TypeInitializationException"/> on every later use of that type forever. Every other failure is
        /// recorded on <see cref="PrecompiledInitSite.Fault"/> and answered by the returned object: a hook that threw
        /// or an extension that would not construct costs <b>that call site</b> — the returned substitute renders the
        /// call by compiling its own source text — while compile errors the hook reported, or a hook answer about
        /// body typing that contradicts what the build assumed, cost the <b>template</b>, which belongs on the
        /// dynamic tier.</para>
        /// <para>Cost lands entirely at type-init: one <c>TemplateOptions</c>, one <c>CompileContext</c>, one
        /// <c>CompileScope</c>, one <c>ParseContext</c> and one witness <c>OutputItem</c> per call site, plus
        /// whatever the hook itself allocates. Nothing here is reachable from a render.</para>
        /// <para><b>Two things around <c>InitializeTemplate</c> are deliberately not reproduced here.</b> The
        /// <c>[DataType]</c>/<c>[ChainedType]</c> checks the compiler runs just before it are omitted: they need the
        /// chain's "no producer to the right" state, which a call site records as a type and cannot distinguish from
        /// <see cref="object"/>, and they add only diagnostics. And a parameter-declaring extension's
        /// <c>ExtensionParameterCarrier</c> wrap, which the compiler applies just after, stays with
        /// <see cref="BindExtension"/>.</para>
        /// </summary>
        /// <param name="factory">Constructs the extension. Runs inside the fault capture.</param>
        /// <param name="site">Everything the engine's compiler knew at this call site.</param>
        /// <param name="body">The generated body strategy, or <c>null</c> when the call's body compiled to no
        /// processors or the call has no body.</param>
        /// <returns>The initialized extension, or a substitute for this call site when the hook did not succeed.
        /// Never <c>null</c>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> or <paramref name="site"/> is null.</exception>
        public static AbstractExtension Init(Func<AbstractExtension> factory, PrecompiledInitSite site,
            IProcessStrategy body)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (site == null)
                throw new ArgumentNullException(nameof(site));

            try
            {
                var extension = factory();
                if (extension == null)
                    return Faulted(site, PrecompiledInitFaultScope.CallSite,
                        "The factory for '" + Describe(site) + "' returned null.", null, null, null);

                var scope = BuildScope(site, site.SlotType);
                var run = RunInit(extension, site, scope, BuildParseContext(site), BuildSourceItem(site),
                    site.Body, body, ToExType(site.DataType), ToExType(site.ChainedType), ToExType(site.ParentType),
                    new BlockPosition(site.PositionStart, site.PositionLength));
                if (run != null)
                    return Faulted(site, run.Scope, run.Detail, run.Exception, run.Errors, run.Reason);

                var drained = Drain(site, scope);
                if (drained != null)
                    return Faulted(site, drained.Scope, drained.Detail, drained.Exception, drained.Errors,
                        drained.Reason);
                return extension;
            }
            catch (Exception e)
            {
                return Faulted(site, PrecompiledInitFaultScope.CallSite,
                    "Initializing '" + Describe(site) + "' threw " + e.GetType().Name + ": " + e.Message, e, null,
                    null);
            }
        }

        /// <summary>
        /// The definition-invocation form: <b>two</b> <c>InitStart</c> runs, as
        /// <c>HeddleCompiler.CreateExtension</c> does them. The outer carrier hosts the invocation's caller content
        /// and is typed by the definition's slot type when it declares one, otherwise by the call's model type; the
        /// inner carrier hosts the definition's own body and is typed by the call's model type, with the definition's
        /// slot type installed on the compile context for the length of that run. Both carriers take their recursion
        /// limit from the hook rather than from a baked constant.
        /// </summary>
        /// <param name="site">The call site; <see cref="PrecompiledInitSite.Body"/> is the caller content and
        /// <see cref="PrecompiledInitSite.DefinitionBody"/> the definition body.</param>
        /// <param name="body">The definition body's strategy.</param>
        /// <param name="callerContent">The invocation's caller-content strategy.</param>
        /// <param name="props">The frozen prop prototype, or <c>null</c>.</param>
        /// <param name="dynamicSetters">Per-invocation prop setters, or <c>null</c>.</param>
        /// <returns>The outer carrier, or a substitute for this call site when either hook run did not succeed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="site"/> is null.</exception>
        public static AbstractExtension InitDefinition(PrecompiledInitSite site, IProcessStrategy body,
            IProcessStrategy callerContent, object[] props, PrecompiledPropSetter[] dynamicSetters)
        {
            if (site == null)
                throw new ArgumentNullException(nameof(site));

            try
            {
                // Both carriers are positioned at the definition's declaration, as CompileFromDefenition does it.
                var position = new BlockPosition(site.DefinitionPositionStart, site.DefinitionPositionLength);
                var parseContext = BuildParseContext(site);
                var sourceItem = BuildSourceItem(site);
                var dataType = ToExType(site.DataType);
                var chainedType = ToExType(site.ChainedType);
                var parentType = ToExType(site.ParentType);
                var slotType = site.DefinitionSlotType == null ? null : new ExType(site.DefinitionSlotType);

                var inner = new DefinitionBaseExtension { Position = position };
                var outer = new DefinitionBaseExtension { Position = position, DefinitionParameterTemplate = inner };
                outer.SlotMode = slotType != null;
                outer.ReceivesChainedValue = site.HasProducerToRight && (site.Body == null || site.Body.RawText == null);

                // Caller content compiles under the enclosing slot type, against the slot type when the definition
                // declares one and the call's model type otherwise.
                var outerScope = BuildScope(site, site.SlotType);
                var outerRun = RunInit(outer, site, outerScope, parseContext, sourceItem, site.Body, callerContent,
                    slotType ?? dataType, chainedType, parentType, position);
                if (outerRun != null)
                    return Faulted(site, outerRun.Scope, outerRun.Detail, outerRun.Exception, outerRun.Errors,
                        outerRun.Reason);

                // The definition body compiles under the definition's own slot type.
                var innerScope = BuildScope(site, site.DefinitionSlotType);
                var innerRun = RunInit(inner, site, innerScope, parseContext, sourceItem, site.DefinitionBody, body,
                    dataType, chainedType, parentType, position);
                if (innerRun != null)
                    return Faulted(site, innerRun.Scope, innerRun.Detail, innerRun.Exception, innerRun.Errors,
                        innerRun.Reason);

                var drained = Drain(site, outerScope) ?? Drain(site, innerScope);
                if (drained != null)
                    return Faulted(site, drained.Scope, drained.Detail, drained.Exception, drained.Errors,
                        drained.Reason);

                if (props != null || (dynamicSetters != null && dynamicSetters.Length != 0))
                    outer.SetPrecompiledProps(props, dynamicSetters);
                return outer;
            }
            catch (Exception e)
            {
                return Faulted(site, PrecompiledInitFaultScope.CallSite,
                    "Initializing definition call '" + Describe(site) + "' threw " + e.GetType().Name + ": " +
                    e.Message, e, null, null);
            }
        }

        /// <summary>One <c>InitializeTemplate</c> run with the body supplied rather than compiled. Returns
        /// <c>null</c> on success, or the fault to record.</summary>
        private static InitOutcome RunInit(AbstractExtension extension, PrecompiledInitSite site, CompileScope scope,
            ParseContext parseContext, OutputItem sourceItem, PrecompiledInitBody bodyText, IProcessStrategy body,
            ExType dataType, ExType chainedType, ExType parentType, BlockPosition position)
        {
            var raw = bodyText?.RawText;
            var extensionType = extension.GetType();
            extension.Position = position;
            extension.SetUpRenderType(RenderTypeRules.Derive(
                extensionType.IsHaveAttribute<EncodeOutputAttribute>(true),
                extensionType.IsHaveAttribute<NotEncodeAttribute>(true)));

            var initContext = new InitContext(raw, scope, parseContext) { SourceItem = sourceItem };
            var frame = new PrecompiledBodyFrame(body, raw, bodyText?.ShapedText, bodyText?.NeedsLocals ?? false);

            PrecompiledBodySupply.Arm(frame);
            try
            {
                extension.InitStart(initContext, dataType ?? ExType.Dynamic, chainedType ?? ExType.Dynamic, parentType);
            }
            catch (Exception e)
            {
                return new InitOutcome(PrecompiledInitFaultScope.CallSite,
                    "'" + Describe(site) + "' threw from its compile-time hook: " + e.Message, e, null, null);
            }
            finally
            {
                // Disarmed before anything else runs: draining the delayed-template queue compiles whole child
                // documents, and an armed frame would hand this call's body to the first extension in one of them.
                PrecompiledBodySupply.Disarm();
            }

            if (frame.ConsumeCount > 1)
                return new InitOutcome(PrecompiledInitFaultScope.CallSite,
                    "'" + Describe(site) + "' compiled " + frame.ConsumeCount +
                    " bodies; a precompiled call site supplies exactly one.", null, null, null);

            if (frame.ConsumeCount == 0)
            {
                if (body != null || !string.IsNullOrEmpty(raw))
                    return new InitOutcome(PrecompiledInitFaultScope.CallSite,
                        "'" + Describe(site) + "' never compiled the body it was given, so the generated body would " +
                        "never render.", null, null, null);
                return null;   // A bodiless call whose hook does not delegate: nothing was assumed, nothing to check.
            }

            var errors = Collected(scope);
            if (errors != null)
                return new InitOutcome(PrecompiledInitFaultScope.Template,
                    "'" + Describe(site) + "' reported " + errors.Length + " compile error(s); the dynamic tier " +
                    "would refuse this template too.", null, errors,
                    PrecompiledFallbackReason.ExtensionInitCompileError);

            var assumedData = ToExType(bodyText?.AssumedDataType);
            var assumedChained = ToExType(bodyText?.AssumedChainedType);
            if (!ExType.Equals(assumedData, frame.ConsumedDataType) ||
                !ExType.Equals(assumedChained, frame.ConsumedChainedType))
                return new InitOutcome(PrecompiledInitFaultScope.Template,
                    "'" + Describe(site) + "' compiled its body against (" + Name(frame.ConsumedDataType) + ", " +
                    Name(frame.ConsumedChainedType) + ") but the build assumed (" + Name(assumedData) + ", " +
                    Name(assumedChained) + "); the emitted casts would render bytes the engine does not.", null, null,
                    PrecompiledFallbackReason.ExtensionInitTypingMismatch);

            return null;
        }

        /// <summary>Drains the delayed-template queue the way <c>HeddleTemplate.Compile</c> does, after the hook has
        /// run and the supply is disarmed. <c>PartialExtension</c> is the engine's own user of that queue.</summary>
        private static InitOutcome Drain(PrecompiledInitSite site, CompileScope scope)
        {
            try
            {
                scope.Compile();
            }
            catch (Exception e)
            {
                return new InitOutcome(PrecompiledInitFaultScope.CallSite,
                    "Completing '" + Describe(site) + "' threw " + e.GetType().Name + ": " + e.Message, e, null, null);
            }

            var errors = Collected(scope);
            if (errors == null)
                return null;
            return new InitOutcome(PrecompiledInitFaultScope.Template,
                "Completing '" + Describe(site) + "' reported " + errors.Length + " compile error(s).", null, errors,
                PrecompiledFallbackReason.ExtensionInitCompileError);
        }

        private static HeddleCompileError[] Collected(CompileScope scope)
            => scope.CompileErrors.Count == 0 ? null : scope.CompileErrors.ToArray();

        private static AbstractExtension Faulted(PrecompiledInitSite site, PrecompiledInitFaultScope faultScope,
            string detail, Exception exception, HeddleCompileError[] errors, PrecompiledFallbackReason? reason)
        {
            site.Fault = new PrecompiledInitFault(faultScope, detail, exception, errors, reason);
            return new PrecompiledSiteFallbackExtension(site);
        }

        private static string Describe(PrecompiledInitSite site)
            => (string.IsNullOrEmpty(site.ExtensionName) ? "@()" : "@" + site.ExtensionName) + " at " +
               site.PositionStart + ":" + site.PositionLength;

        private static string Name(ExType type) => type == null ? "dynamic" : type.ToString();

        private static ExType ToExType(Type type) => type == null ? ExType.Dynamic : new ExType(type);

        /// <summary>The engine's compile scope for one call site, rebuilt from what the site records.</summary>
        private static CompileScope BuildScope(PrecompiledInitSite site, Type slotType)
        {
            var options = new TemplateOptions
            {
                OutputProfile = site.OutputProfile,
                ExpressionMode = site.ExpressionMode,
                TrimDirectiveLines = site.TrimDirectiveLines,
                MaxRecursionCount = site.MaxRecursionCount
            };
            var context = new CompileContext(options, ToExType(site.ModelType))
            {
                OutputProfile = site.OutputProfile,
                RootScopeType = ToExType(site.RootModelType),
                SlotParameterType = slotType == null ? null : new ExType(slotType)
            };
            var csharpContext = new CSharpContext();
            if (site.Namespaces != null)
            {
                foreach (var ns in site.Namespaces)
                {
                    if (!string.IsNullOrEmpty(ns))
                        csharpContext.Namespaces.Add(ns);
                }
            }

            return new CompileScope(context, csharpContext);
        }

        /// <summary>
        /// A parse context carrying the one thing a hook reads off it: whether the call sits inside a definition
        /// body. Its token stream and sub-contexts are empty and cannot be otherwise — the token stream <i>is</i> the
        /// parse tree of the enclosing document, and no call site can carry one.
        /// </summary>
        private static ParseContext BuildParseContext(PrecompiledInitSite site)
        {
            if (!site.InsideDefinition)
                return new ParseContext(null, site.PositionStart);
            var enclosing = new ParseContext(null, site.PositionStart) { InDefinition = true };
            return new ParseContext(enclosing, site.PositionStart);
        }

        /// <summary>
        /// The witness <c>OutputItem</c> for <c>InitContext.SourceItem</c>. The engine's whole read closure over
        /// that field is <c>SlotRules.HasOutValue</c>'s collapse to a bool, <c>IsChainedConsumer</c> and
        /// <c>Position</c> — so the call parameter here is built to answer that five-way test and nothing else, and
        /// <c>InitSynthesisFidelityTests</c> reads the engine's source to keep the claim true.
        /// </summary>
        private static OutputItem BuildSourceItem(PrecompiledInitSite site)
        {
            var item = new OutputItem(site.ExtensionName ?? string.Empty,
                new BlockPosition(site.PositionStart, site.PositionLength), site.Body?.RawText)
            {
                IsChainedConsumer = site.IsChainedConsumer
            };
            var call = item.CallParameter;
            call.RootReference = site.RootReference;
            switch (site.CallShape)
            {
                case PrecompiledCallShape.NativeExpression:
                    call.NativeExpression = new LiteralNode(null, item.Position);
                    break;
                case PrecompiledCallShape.Chain:
                    call.ChainParameter = new System.Collections.Generic.List<OutputItem>();
                    break;
                case PrecompiledCallShape.CSharpExpression:
                    call.CSharpExpression = site.SourceText ?? "?";
                    break;
                case PrecompiledCallShape.ModelPath:
                    call.ModelParameter = ModelPathWitness;
                    break;
            }

            if (site.HasPropArguments || site.CallShape == PrecompiledCallShape.PropArguments)
                call.PropArguments = NoPropArguments;
            return item;
        }

        private static readonly string[] ModelPathWitness = { "?" };

        private static readonly Heddle.Language.NamedArgument[] NoPropArguments = new Heddle.Language.NamedArgument[0];

        /// <summary>The outcome of one hook run: <c>null</c> for success, otherwise what to record.</summary>
        private sealed class InitOutcome
        {
            internal InitOutcome(PrecompiledInitFaultScope scope, string detail, Exception exception,
                HeddleCompileError[] errors, PrecompiledFallbackReason? reason)
            {
                Scope = scope;
                Detail = detail;
                Exception = exception;
                Errors = errors;
                Reason = reason;
            }

            internal PrecompiledInitFaultScope Scope { get; }
            internal string Detail { get; }
            internal Exception Exception { get; }
            internal HeddleCompileError[] Errors { get; }
            internal PrecompiledFallbackReason? Reason { get; }
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

        /// <summary>The render currently in flight on this thread, or <c>null</c> outside a render. Read by
        /// <see cref="PrecompiledSiteFallbackExtension"/>, which cannot compile before there is a request to compile
        /// under.</summary>
        internal static TemplateOptions AmbientOptions => _ambientOptions;

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
