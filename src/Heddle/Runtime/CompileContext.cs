using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Language;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime.Expressions;

namespace Heddle.Runtime {
    internal class DelayedTemplate
    {
        public CompileScope NewScope;
        public IExtension ForExtension;
        public ParseContext ParseContext;
    }

    internal class ObjectReference<T>
    {
        public ObjectReference(T obj)
        {
            Object = obj;
        }

        public T Object { get; set; }

        public static implicit operator T(ObjectReference<T> reference)
        {
            return reference.Object;
        }
    }

    internal class CompiledElement
    {
        public TemplateItem CompiledItem;
        public ExType ReturnTypeChainedPrevious;
    }

    internal struct OptionalValue<T>
    {
        public OptionalValue(T value, bool hasValue = true)
        {
            Value = value;
            HasValue = hasValue;
        }

        public T Value { get; }

        public bool HasValue { get; }

        public static implicit operator OptionalValue<T>(T value)
        {
            return new OptionalValue<T>(value);
        }
    }

    /// <summary>
    /// By loading assemblies in this context, you can add or override extensions available during compilation.
    /// <para>Not thread-safe: a compile writes to its context without synchronization, so a context belongs to
    /// one compile at a time. Compiles that may run concurrently each take a context of their own.</para>
    /// </summary>
    public class CompileContext: IDisposable {

        public bool Compiled { get; internal set; }

        public List<HeddleCompileError> CompileErrors { get; }

        public List<HeddleCompileWarning> CompileWarnings { get; }

        internal Dictionary<OutputItem, CompiledElement> CompiledItems { get; }

        /// <summary>
        /// The position-indexed scope map: non-null only when
        /// <see cref="Data.TemplateOptions.ProvideLanguageFeatures"/> is true (created in the two root ctors,
        /// reference-copied through the private copy ctor so all child compiles share one). Recorded at the single
        /// body-compile funnel <c>HeddleCompiler.Compile</c>; null on production compiles (one null check per body,
        /// zero allocation). A refusal fragment records none: its offsets are its own, not the document's.
        /// </summary>
        internal ScopeMap ScopeMap { get; }

        /// <summary>
        /// The once-per-definition-per-compile prop-layout cache. Shared through the private copy ctor
        /// exactly like <see cref="CompiledItems"/> — one compile, one cache. Keyed by a stable definition
        /// identity (name + declaration position) rather than the <see cref="DefinitionItem"/> instance, because
        /// context isolation copies definitions per body: two call sites of one definition therefore share the
        /// same resolved <see cref="PropLayout"/> instance (the two-site invariant). A refusal fragment takes its
        /// own, because it parses its own text and its definition positions are not this document's.
        /// </summary>
        internal Dictionary<string, PropLayout> ResolvedPropLayouts { get; }

        /// <summary>
        /// The active prop layout while compiling a definition body. <c>null</c> outside a props-declaring
        /// definition body. Copied by the child-context copy ctor so nested bodies keep the layout;
        /// save/set/restore around each definition-body compile in <c>CreateExtension</c>.
        /// </summary>
        internal PropLayout ActivePropLayout { get; set; }

        /// <summary>
        /// <para>The slot parameter type of the definition whose body is being compiled — the <c>T</c> of
        /// <c>&lt;name(out:: T)&gt;</c> — and <c>null</c> anywhere else. A <c>[SlotProjection]</c> extension reads
        /// it in its own <c>InitStart</c>: non-<c>null</c> means the call is projecting this definition's slot and
        /// the value it was passed must satisfy <c>T</c>; <c>null</c> means there is no slot here.</para>
        /// <para>Set by the engine around each definition-body compile and read-only from outside it — an
        /// extension that installed its own would be telling every later call in the body that a slot it does not
        /// have is open. Same threading rules as <see cref="ActivePropLayout"/>: compile-time state on a
        /// single-threaded compile, copied to child contexts, never read at render.</para>
        /// </summary>
        public ExType SlotParameterType { get; internal set; }

        /// <summary>
        /// The call-scoped region fill scope active while compiling a definition body whose call site
        /// matched region-fill candidates. <c>null</c> outside such a body. Copied by the child-context copy ctor
        /// (the proven <see cref="ActivePropLayout"/> propagation seam) so a fill reaches nested region calls at
        /// any depth; save/set/restore around each definition-body compile in <c>HeddleCompiler.CreateExtension</c>.
        /// </summary>
        internal RegionFillScope RegionFillScope { get; set; }

        /// <summary>
        /// The once-per-component-per-compile region-layout cache, mirroring
        /// <see cref="ResolvedPropLayouts"/> (same stable name+position key; shared through the copy ctor).
        /// </summary>
        internal Dictionary<string, RegionLayout> ResolvedRegionLayouts { get; }

        public string ControllerName { get; set; }

        internal List<DelayedTemplate> DelayedTemplates { get; } = new List<DelayedTemplate>();
        private ExType _scopeType;
        private readonly CSharpContext _csharpContext;
        private bool _recordForm;

        /// <summary>
        /// Whether unbound function names defer instead of failing. Off on every ordinary compile; the
        /// build tier arms it together with <see cref="RecordForm"/>. Propagates into child contexts so
        /// nested body compiles of one build defer alike.
        /// </summary>
        internal bool DeferUnboundFunctions { get; set; }

        /// <summary>
        /// The <c>@&lt;&lt;</c> import reader over an in-memory spelling map, or null for the file
        /// ladder. The build host maps every item's key and registered name; the loader maps every
        /// artifact row's. Propagates into child contexts so nested body compiles resolve alike.
        /// </summary>
        internal Func<string, string> ImportReader { get; set; }

        /// <summary>
        /// The import cycle identity for <see cref="ImportReader"/>, from the same resolution: a
        /// spelling the map answers to identifies as its canonical entry, anything else as its file
        /// path. Null follows the reader (file ladder both ways).
        /// </summary>
        internal Func<string, string> ImportIdentifier { get; set; }

        /// <summary>
        /// Whether this compile records the compiled form. Off on every ordinary compile; enabling
        /// allocates one record shared across the compile's child contexts.
        /// </summary>
        internal bool RecordForm
        {
            get { return _recordForm; }
            set
            {
                _recordForm = value;
                if (value && FormRecord == null)
                    FormRecord = new FormRecord();
            }
        }

        /// <summary>The form record, or null when <see cref="RecordForm"/> is off. Shared by reference
        /// through the child-context copy constructor so nested body compiles record into one form.</summary>
        internal FormRecord FormRecord { get; private set; }

        /// <summary>The context a recorded refusal fragment recompiles under at load: the enclosing compile's
        /// place in the document, with the enclosing compile's own caches and build state left behind.
        /// <paramref name="scopeType"/> is the scope the refused call site stood in.</summary>
        internal static CompileContext ForRefusalFragment(CompileContext context, ExType scopeType)
        {
            return new CompileContext(context, modelType: scopeType, refusalFragment: true);
        }

        private CompileContext(CompileContext context, string fileName = null, ExType modelType = null,
            bool refusalFragment = false)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            // Two kinds of parent state meet here, and a refusal fragment keeps only the first.
            //
            // Where the compile SITS in the document is inherited by every child, the fragment included:
            // the root type a '::' read resolves against, the prop layout and slot type of the definition
            // body around it, the call's region fills, the reader an '@<<' import expands through, the
            // controller, the options and the output profile. A fragment recompiles a call that stood
            // exactly where the recorded one stood, and the dynamic tier compiles that same text under
            // all of it — anything dropped here is a tier divergence.
            //
            // What the enclosing compile has already DECIDED is not inherited by a fragment. It binds for
            // real at load, so it takes neither the form record nor unbound-function deferral, both of
            // which are the build's and carry the build's possibly-deferred typings. It collects its own
            // errors, warnings, compiled items and resolved layouts because it parses its own text and
            // its positions stay fragment-local until the caller re-anchors them, and it records no scope
            // map for the same reason: fragment offsets are not the outer document's coordinates.
            _recordForm = !refusalFragment && context._recordForm;
            FormRecord = refusalFragment ? null : context.FormRecord;
            DeferUnboundFunctions = !refusalFragment && context.DeferUnboundFunctions;
            ImportReader = context.ImportReader;
            ImportIdentifier = context.ImportIdentifier;
            RootScopeType = context.RootScopeType;
            CompiledItems = refusalFragment
                ? new Dictionary<OutputItem, CompiledElement>()
                : context.CompiledItems;
            ResolvedPropLayouts = refusalFragment
                ? new Dictionary<string, PropLayout>()
                : context.ResolvedPropLayouts;
            CompileErrors = refusalFragment ? new List<HeddleCompileError>() : context.CompileErrors;
            CompileWarnings = refusalFragment ? new List<HeddleCompileWarning>() : context.CompileWarnings;
            ControllerName = context.ControllerName;
            // The fragment is the same file under the same options, so it shares the instance rather than
            // taking a renamed copy the way a partial or an import does.
            Options = refusalFragment ? context.Options : new TemplateOptions(context.Options, fileName);
            ScopeType = modelType ?? context.ScopeType ?? typeof(object);
            OutputProfile = context.OutputProfile;
            ActivePropLayout = context.ActivePropLayout;
            SlotParameterType = context.SlotParameterType;
            RegionFillScope = context.RegionFillScope;
            ResolvedRegionLayouts = refusalFragment
                ? new Dictionary<string, RegionLayout>()
                : context.ResolvedRegionLayouts;
            ScopeMap = refusalFragment ? null : context.ScopeMap;
            _csharpContext = context._csharpContext;
            _memberAccessors = context.MemberAccessors;
        }
        private Dictionary<Parameters.MemberPathKey, System.Func<object, object>> _memberAccessors;

        /// <summary>The member-path accessors this compile has built, by the hops they read. An accessor is a pure
        /// function of its hops, and a document reads the same few paths again and again — <c>Title</c> in every
        /// row of a list of pages — while building one costs a delegate compile. Shared by the contexts a compile
        /// derives from this one, and gone with it.</summary>
        internal Dictionary<Parameters.MemberPathKey, System.Func<object, object>> MemberAccessors =>
            _memberAccessors ?? (_memberAccessors = new Dictionary<Parameters.MemberPathKey, System.Func<object, object>>());

        public CompileContext(ExType modelType = null) {
            RootScopeType = ScopeType = modelType ?? (ExType)typeof(object);
            Options = new TemplateOptions();
            OutputProfile = Options.OutputProfile;
            CompileErrors = new List<HeddleCompileError>();
            CompileWarnings = new List<HeddleCompileWarning>();
            CompiledItems = new Dictionary<OutputItem, CompiledElement>();
            ResolvedPropLayouts = new Dictionary<string, PropLayout>();
            ResolvedRegionLayouts = new Dictionary<string, RegionLayout>();
            ScopeMap = Options.ProvideLanguageFeatures ? new ScopeMap() : null;
            _csharpContext = new CSharpContext();
        }

        /// <summary>Create an untyped context (System.Object) for template compilation.</summary>
        /// <param name="options"></param>
        /// <param name="modelType"></param>
        public CompileContext(TemplateOptions options, ExType modelType = null)
        {
            RootScopeType = ScopeType = modelType ?? typeof (object);
            Options = options;
            OutputProfile = Options.OutputProfile;
            CompileErrors = new List<HeddleCompileError>();
            CompileWarnings = new List<HeddleCompileWarning>();
            CompiledItems = new Dictionary<OutputItem, CompiledElement>();
            ResolvedPropLayouts = new Dictionary<string, PropLayout>();
            ResolvedRegionLayouts = new Dictionary<string, RegionLayout>();
            ScopeMap = Options.ProvideLanguageFeatures ? new ScopeMap() : null;
            _csharpContext = new CSharpContext();
        }

        /// <summary>Create a context with a new template file name.</summary>
        /// <param name="context">Old Context</param>
        /// <param name="newName">New Tempalte File Name</param>
        public CompileContext(
            CompileContext context, string newName)
            : this(context, fileName: newName)
        {
        }


        /// <summary>Create a context with a new model type.</summary>
        /// <param name="context">Old Context</param>
        /// <param name="newType">New Enclosing Template Data Type</param>
        public CompileContext(
            CompileContext context, ExType newType)
            : this(context, modelType: newType)
        {
        }

        /// <summary>Create a context with a new model type and template file name.</summary>
        /// <param name="context">Old Context</param>
        /// <param name="newType">New Template Data Type</param>
        /// <param name="newName">New Tempalte File Name</param>
        public CompileContext(
            CompileContext context, ExType newType, string newName)
            : this(context, newName, newType)
        {
        }

        public TemplateOptions Options { get; }

        /// <summary>
        /// <para>The effective output profile for items compiled from this context onward. Initialized from
        /// <see cref="TemplateOptions.OutputProfile"/>; flipped by the <c>@profile()</c> directive;
        /// snapshotted by child contexts (bodies, partials, imports) at creation.</para>
        /// <para>Compile-time state on a single-threaded compile — never read at render time.</para>
        /// </summary>
        public OutputProfile OutputProfile { get; set; }

        /// <summary>
        /// Set when a bodiless unnamed <c>@(...)</c> / standalone-function carrier is resolved in this
        /// context (both profiles). Drives the <c>HED2002</c> "profile directive after output" warning.
        /// Per-context and deliberately not copied to child contexts.
        /// </summary>
        internal bool UnnamedOutputCompiled { get; set; }

        /// <summary>
        /// Model Type can be changed at any time you running your template extension.
        /// Be carefull changing this type without re-creating context. 
        /// Recommendation is to change it only once maximum per chained template block.
        /// Used in &lt;model&gt; base extension. <see cref="Heddle.Extensions.ModelExtension"/>
        /// </summary>
        public ExType ScopeType
        {
            get { return _scopeType; }
            set
            {
                _scopeType = value;
                if (RootScopeType == null)
                {
                    RootScopeType = value;
                }
            }
        }

        public ExType RootScopeType { get; internal set; }

        public virtual void AddDelayedCompileTemplate(CompileScope compileScope, ParseContext parserContext, IExtension forExtension)
        {
            DelayedTemplates.Add(new DelayedTemplate
            {
                NewScope = compileScope,
                ForExtension = forExtension,
                ParseContext = parserContext
            });
        }
        
        ~CompileContext()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing) {
            //TODO: Unload the assembly, since coreclr should support Assembly.Unload in the future.
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}