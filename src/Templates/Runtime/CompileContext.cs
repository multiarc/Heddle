using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Templates.Data;
using Templates.Language;

namespace Templates.Runtime {
    /// <summary>
    /// Compile Context class. Doing all work to compile extensions, saving type for each context level extension, import namespace/assembly. 
    /// By loading assembly you can add or override existing extensions or add some extra funtionality parts to template.
    /// </summary>
    public class CompileContext {

        private class DelayedTemplate {
            public CompileContext NewContext;
            public IExtension ForExtension;
            public ParseContext ParseContext;
        }

        public string ControllerName { get; set; }

        private readonly List<string> _namespaces = new List<string>();

        private readonly List<DelayedTemplate> _delayedTemplates = new List<DelayedTemplate>();

        private CompileContext(CompileContext context, string fileName = null, Type modelType = null)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            ControllerName = context.ControllerName;
            Options = new TemplateOptions(context.Options.FileNamePostfix, context.Options.RootPath,
                fileName ?? context.Options.TemplateName, context.Options.EnableFileChangeCheck);
            ModelType = modelType ?? context.ModelType ?? typeof(object);
            _namespaces = context._namespaces.ToList();
        }

        public CompileContext(Type modelType = null) {
            ModelType = modelType ?? typeof(object);
            Options = new TemplateOptions();
        }

        /// <summary>
        /// Create new untyped (<see cref="System.Object"/>) initial level context to load and compile template from a file.
        /// Enclosing template level = 0
        /// </summary>
        /// <param name="options"></param>
        public CompileContext(TemplateOptions options)
        {
            ModelType = typeof (object);
            Options = options;
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
            CompileContext context, Type newType)
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
            CompileContext context, Type newType, string newName)
            : this(context, newName, newType)
        {
        }

        public TemplateOptions Options { get; set; }

        /// <summary>
        /// Model Type can be changed at any time you running your template extension.
        /// Be carefull changing this type without re-creating context. 
        /// Recommendation is to change it only once maximum per chained template block.
        /// Used in &lt;model&gt; base extension. <see cref="Templates.Extensions.ModelExtension"/>
        /// </summary>
        public Type ModelType
        {
            get;
            set;
        }

        public IReadOnlyCollection<string> Namespaces => _namespaces.AsReadOnly();

        /// <summary>
        /// Compile delayed Extensions, Compile all dynamic property references and connect into template chain.
        /// </summary>
        public virtual void Compile() {
            foreach (var delayedTemplate in _delayedTemplates) {
                delayedTemplate.ForExtension.CompleteInit(delayedTemplate.NewContext, delayedTemplate.ParseContext);
            }
            _delayedTemplates.Clear();
        }

        public virtual void AddDelayedCompileTemplate(CompileContext newContext, ParseContext parserContext, IExtension forExtension)
        {
            _delayedTemplates.Add(new DelayedTemplate
            {
                NewContext = newContext,
                ForExtension = forExtension,
                ParseContext = parserContext
            });
        }

        public void ImportNamespace(string parameterTemplate)
        {
            if (!string.IsNullOrEmpty(parameterTemplate) && !_namespaces.Contains(parameterTemplate))
                _namespaces.Add(parameterTemplate);
        }
    }
}