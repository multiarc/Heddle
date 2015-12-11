using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.PlatformAbstractions;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Native;
using Templates.Runtime.Parameters;

namespace Templates.Runtime {
    internal class DelayedTemplate
    {
        public CompileContext NewContext;
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

    /// <summary>
    /// Compile Context class. Doing all work to compile extensions, saving type for each context level extension, import namespace/assembly. 
    /// By loading assembly you can add or override existing extensions or add some extra funtionality parts to template.
    /// </summary>
    public class CompileContext: IDisposable {

        internal static readonly SymbolDisplayFormat DisplayFormat =
            new SymbolDisplayFormat(SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                kindOptions: SymbolDisplayKindOptions.None,
                delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
                extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
                localOptions: SymbolDisplayLocalOptions.None, memberOptions: SymbolDisplayMemberOptions.None,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

        internal HashSet<Assembly> DependentAssemblies { get; }
            =
            new HashSet<Assembly>(new[]
            {
                typeof (object).GetTypeInfo().Assembly, typeof (Enumerable).GetTypeInfo().Assembly,
                typeof (InternalsVisibleToAttribute).GetTypeInfo().Assembly
            });

        public static TtlCompileResult InitErrors { get; }

        public List<TtlCompileError> CompileErrors { get; }

        public List<TtlCompileWarning> CompileWarnings { get; }

        private static readonly TtlTemplate PreparseGenerator;

        static CompileContext()
        {
            string document = null;
            try
            {
                PreparseGenerator = new TtlTemplate();
                IApplicationEnvironment env =
                    (IApplicationEnvironment)
                        CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof (IApplicationEnvironment));
                var path = env.ApplicationBasePath + "/";
                document = File.ReadAllText($"{path}CSharpPreparseTemplate.tcs");
                InitErrors = PreparseGenerator.Compile(document);
            }
            catch (Exception e)
            {
                InitErrors = new TtlCompileResult(false, document);
                InitErrors.Errors.Add(new TtlCompileError
                {
                    Error = e.Message,
                    Exception = e
                });
            }
        }

        internal Dictionary<OutputItem, CompiledElement> CompiledItems { get; }

        public Guid ClassGuid { get; } = Guid.NewGuid();

        public string ControllerName { get; set; }

        private readonly HashSet<string> _namespaces = new HashSet<string>();

        internal Assembly CompiledAssembly { get; set; }
        public bool Compiled { get; internal set; }

        internal List<DelayedTemplate> DelayedTemplates { get; } = new List<DelayedTemplate>();
        private volatile int _method;

        private CompileContext(CompileContext context, string fileName = null, ExType modelType = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            CompiledItems = context.CompiledItems;
            CompileErrors = context.CompileErrors;
            CompileWarnings = context.CompileWarnings;
            ControllerName = context.ControllerName;
            Options = new TemplateOptions(context.Options.FileNamePostfix, context.Options.RootPath,
                fileName ?? context.Options.TemplateName, context.Options.EnableFileChangeCheck, context.Options.AllowCSharp);
            ModelType = modelType ?? context.ModelType ?? typeof(object);
            _namespaces = new HashSet<string>(context._namespaces);
        }

        public CompileContext(ExType modelType = null) {
            ModelType = modelType ?? (ExType)typeof(object);
            Options = new TemplateOptions();
            CompileErrors = new List<TtlCompileError>();
            CompileWarnings = new List<TtlCompileWarning>();
            CompiledItems = new Dictionary<OutputItem, CompiledElement>();
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
            CompileErrors = new List<TtlCompileError>();
            CompileWarnings = new List<TtlCompileWarning>();
            CompiledItems = new Dictionary<OutputItem, CompiledElement>();
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
        /// Used in &lt;model&gt; base extension. <see cref="Templates.Extensions.ModelExtension"/>
        /// </summary>
        public ExType ModelType
        {
            get;
            set;
        }

        private static string FormatAssemblyName(AssemblyName assemblyName, bool fullPublic = true)
        {
            if (!fullPublic)
                return assemblyName.FullName;
            var publicKey = assemblyName.GetPublicKey();
            return publicKey.Length > 0
                ? $"{assemblyName.Name},PublicKey={assemblyName.GetPublicKey().ToHexString()}"
                : assemblyName.Name;
        }

        public IEnumerable<string> InternalsVisibleTo
        {
            get
            {
                var currentAssemblyName = GetType().GetTypeInfo().Assembly.GetName();
                yield return FormatAssemblyName(currentAssemblyName);
#if DOTNET5_4
                var systemAssemblyName = AssemblyHelper.GetAssemblyName("System.Dynamic.Runtime");
                if (systemAssemblyName != null)
                    yield return $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = AssemblyHelper.GetAssemblyName("System.Core");
                if (systemAssemblyName != null)
                    yield return $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = AssemblyHelper.GetAssemblyName("System.Runtime");
                if (systemAssemblyName != null)
                    yield return $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = AssemblyHelper.GetAssemblyName("System");
                if (systemAssemblyName != null)
                    yield return $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = typeof(object).GetTypeInfo().Assembly.GetName();
                if (systemAssemblyName != null)
                    yield return $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = AssemblyHelper.GetAssemblyName("Microsoft.CSharp");
                if (systemAssemblyName != null)
                    yield return $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
#endif
            }
        }

        public IEnumerable<string> Namespaces => _namespaces;

        public virtual void AddDelayedCompileTemplate(CompileContext newContext, ParseContext parserContext, IExtension forExtension)
        {
            DelayedTemplates.Add(new DelayedTemplate
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

        internal List<ExpressionCompilation> Methods { get; } = new List<ExpressionCompilation>();

        internal IRuntimeParameter PushCompileExpression(ExpressionOptions expressionOptions)
        {
            if (string.IsNullOrEmpty(expressionOptions.Expression))
            {
                throw new ArgumentException($"[{expressionOptions.Position}]<{expressionOptions.ExtensionName}> Expression cannot be null or empty");
            }
            IRuntimeParameter parameter = new CompiledParameter();
            Methods.Add(new ExpressionCompilation(expressionOptions)
            {
                RuntimeCallParameter = parameter,
                MethodNumber = _method,
                ModelType = ModelType
            });
            DependentAssemblies.Add(ModelType.Type.GetTypeInfo().Assembly);
            if (expressionOptions.ChainedType.IsDynamic)
                DependentAssemblies.Add(typeof(CallSite<>).GetTypeInfo().Assembly);
            DependentAssemblies.Add(expressionOptions.ChainedType.Type.GetTypeInfo().Assembly);
            _method++;
            return parameter;
        }

        internal ExType ParseAndGetResultType(ExpressionOptions expressionOptions, out object constantResult)
        {
            if (string.IsNullOrEmpty(expressionOptions.Expression))
            {
                throw new ArgumentException(
                    $"[{expressionOptions.Position}]<{expressionOptions.ExtensionName}> Expression cannot be null or empty");
            }
            expressionOptions.ModelType = ModelType;
            _namespaces.Add(expressionOptions.ModelType.Type.Namespace);
            _namespaces.Add(expressionOptions.ChainedType.Type.Namespace);
            var modelTypeInfo = expressionOptions.ModelType.Type.GetTypeInfo();
            if (modelTypeInfo.IsGenericType)
            {
                foreach (var type in modelTypeInfo.GenericTypeArguments)
                {
                    _namespaces.Add(type.Namespace);
                }
            }
            var chainTypeInfo = expressionOptions.ChainedType.Type.GetTypeInfo();
            if (chainTypeInfo.IsGenericType)
            {
                foreach (var type in chainTypeInfo.GenericTypeArguments)
                {
                    _namespaces.Add(type.Namespace);
                }
            }
            expressionOptions.Namespaces = Namespaces;
            if (!InitErrors.Success)
                throw new TemplateCompileException("Cannot compile base C# generation templates",
                    InitErrors.Errors);
            var code = PreparseGenerator.Generate(expressionOptions);
            var tree = CSharpSyntaxTree.ParseText(code);
            var assemblySet = AssemblyHelper.GetMetadataReferences();
            var compilation = CSharpCompilation.Create(null, new[] {tree}, assemblySet);
            var diagnostics = compilation.GetDiagnostics();
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                CompileErrors.AddRange(ContextCompilation.FormatErrors(diagnostics, expressionOptions.Position));
                constantResult = null;
                return typeof (object);
            }
            var syntax = tree.GetRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;
            var model = compilation.GetSemanticModel(tree, false);
            var constantValue = model.GetConstantValue(syntax);
            if (constantValue.HasValue)
            {
                constantResult = constantValue.Value;
                return constantValue.Value.GetType();
            }
            var typeInfo = model.GetTypeInfo(syntax);
            if (typeInfo.Type.IsAnonymousType || typeInfo.Type.TypeKind == TypeKind.Dynamic)
            {
                constantResult = null;
                return ExType.Dynamic;
            }
            constantResult = null;
            string typeName = typeInfo.Type.ToDisplayString(DisplayFormat);
            try
            {
                return
                    ReflectionHelper.ResolveType(typeName, _namespaces.ToArray());
            }
            catch (InvalidOperationException e)
            {
                CompileErrors.Add(e.ToError(expressionOptions.Position));
                return typeof (object);
            }
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