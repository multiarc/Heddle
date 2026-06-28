using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Language;

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
            CompileErrors = context.CompileErrors;
            CompileWarnings = context.CompileWarnings;
            ControllerName = context.ControllerName;
            Options = new TemplateOptions(context.Options, fileName);
            ScopeType = modelType ?? context.ScopeType ?? typeof(object);
            _csharpContext = context._csharpContext;
        }

        public CompileContext(ExType modelType = null) {
            RootScopeType = ScopeType = modelType ?? (ExType)typeof(object);
            Options = new TemplateOptions();
            CompileErrors = new List<HeddleCompileError>();
            CompileWarnings = new List<HeddleCompileWarning>();
            CompiledItems = new Dictionary<OutputItem, CompiledElement>();
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
            CompileErrors = new List<HeddleCompileError>();
            CompileWarnings = new List<HeddleCompileWarning>();
            CompiledItems = new Dictionary<OutputItem, CompiledElement>();
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