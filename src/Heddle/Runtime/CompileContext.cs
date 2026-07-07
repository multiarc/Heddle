using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Language;
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
    /// Compile Context class. Doing all work to compile extensions, saving type for each context level extension, import namespace/assembly. 
    /// By loading assembly you can add or override existing extensions or add some extra funtionality parts to template.
    /// </summary>
    public class CompileContext: IDisposable {

        public bool Compiled { get; internal set; }

        public List<HeddleCompileError> CompileErrors { get; }

        public List<HeddleCompileWarning> CompileWarnings { get; }

        internal Dictionary<OutputItem, CompiledElement> CompiledItems { get; }

        /// <summary>
        /// The phase 6 D2 position-indexed scope map: non-null only when
        /// <see cref="Data.TemplateOptions.ProvideLanguageFeatures"/> is true (created in the two root ctors,
        /// reference-copied through the private copy ctor so all child compiles share one). Recorded at the single
        /// body-compile funnel <c>HeddleCompiler.Compile</c>; null on production compiles (one null check per body,
        /// zero allocation).
        /// </summary>
        internal ScopeMap ScopeMap { get; }

        /// <summary>
        /// The once-per-definition-per-compile prop-layout cache (D6). Shared through the private copy ctor
        /// exactly like <see cref="CompiledItems"/> — one compile, one cache. Keyed by a stable definition
        /// identity (name + declaration position) rather than the <see cref="DefinitionItem"/> instance, because
        /// context isolation copies definitions per body: two call sites of one definition therefore share the
        /// same resolved <see cref="PropLayout"/> instance (the D6 two-site invariant).
        /// </summary>
        internal Dictionary<string, PropLayout> ResolvedPropLayouts { get; }

        /// <summary>
        /// The active prop layout while compiling a definition body (D12). <c>null</c> outside a props-declaring
        /// definition body. Copied by the child-context copy ctor so nested bodies keep the layout;
        /// save/set/restore around each definition-body compile in <c>CreateExtension</c>.
        /// </summary>
        internal PropLayout ActivePropLayout { get; set; }

        /// <summary>
        /// The active slot parameter type while compiling a slot-declaring definition body (D12). <c>null</c>
        /// otherwise. Same threading rules as <see cref="ActivePropLayout"/>.
        /// </summary>
        internal ExType SlotParameterType { get; set; }

        public string ControllerName { get; set; }

        internal List<DelayedTemplate> DelayedTemplates { get; } = new List<DelayedTemplate>();
        private ExType _scopeType;
        private readonly CSharpContext _csharpContext;

        private CompileContext(CompileContext context, string fileName = null, ExType modelType = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            RootScopeType = context.RootScopeType;
            CompiledItems = context.CompiledItems;
            ResolvedPropLayouts = context.ResolvedPropLayouts;
            CompileErrors = context.CompileErrors;
            CompileWarnings = context.CompileWarnings;
            ControllerName = context.ControllerName;
            Options = new TemplateOptions(context.Options, fileName);
            ScopeType = modelType ?? context.ScopeType ?? typeof(object);
            OutputProfile = context.OutputProfile;
            ActivePropLayout = context.ActivePropLayout;
            SlotParameterType = context.SlotParameterType;
            ScopeMap = context.ScopeMap;
            _csharpContext = context._csharpContext;
        }

        public CompileContext(ExType modelType = null) {
            RootScopeType = ScopeType = modelType ?? (ExType)typeof(object);
            Options = new TemplateOptions();
            OutputProfile = Options.OutputProfile;
            CompileErrors = new List<HeddleCompileError>();
            CompileWarnings = new List<HeddleCompileWarning>();
            CompiledItems = new Dictionary<OutputItem, CompiledElement>();
            ResolvedPropLayouts = new Dictionary<string, PropLayout>();
            ScopeMap = Options.ProvideLanguageFeatures ? new ScopeMap() : null;
            _csharpContext = new CSharpContext();
        }

        /// <summary>
        /// Create new untyped (<see cref="System.Object"/>) initial level context to load and compile template from a file.
        /// Enclosing template level = 0
        /// </summary>
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
            ScopeMap = Options.ProvideLanguageFeatures ? new ScopeMap() : null;
            _csharpContext = new CSharpContext();
        }

        /// <summary>
        /// Create new untyped (<see cref="System.Object"/>) Context using old Context data with new template file name
        /// Enclosing template level = 0
        /// Use for templates typed explicitly in template file but not in code.
        /// </summary>
        /// <param name="context">Old Context</param>
        /// <param name="newName">New Tempalte File Name</param>
        public CompileContext(
            CompileContext context, string newName)
            : this(context, fileName: newName)
        {
        }


        /// <summary>
        /// Create new typed Context using old Context data just changing Type.
        /// Enclosing level = Old Context level + 1
        /// </summary>
        /// <param name="context">Old Context</param>
        /// <param name="newType">New Enclosing Template Data Type</param>
        public CompileContext(
            CompileContext context, ExType newType)
            : this(context, modelType: newType)
        {
        }

        /// <summary>
        /// Create new typed Context using old Context data, changing type and template file name.
        /// Enclosing template level = 0
        /// Use for templates typed explicitly in code but not in template file.
        /// </summary>
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