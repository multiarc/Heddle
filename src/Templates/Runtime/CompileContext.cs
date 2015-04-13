using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Native;

#if DNXCORE50
using Microsoft.Framework.Runtime.Loader;
using System.Runtime.Loader;
#endif

namespace Templates.Runtime {
    /// <summary>
    /// Compile Context class. Doing all work to compile extensions, saving type for each context level extension, import namespace/assembly. 
    /// By loading assembly you can add or override existing extensions or add some extra funtionality parts to template.
    /// </summary>
    public class CompileContext: IDisposable {

        private class DelayedTemplate {
            public CompileContext NewContext;
            public IExtension ForExtension;
            public ParseContext ParseContext;
        }

        internal static readonly SymbolDisplayFormat DisplayFormat =
            new SymbolDisplayFormat(SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                kindOptions: SymbolDisplayKindOptions.None,
                delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
                extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
                localOptions: SymbolDisplayLocalOptions.None, memberOptions: SymbolDisplayMemberOptions.None,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

        internal Assembly CompiledAssembly { get; private set; }

        internal HashSet<Assembly> DependentAssemblies { get; }
            = new HashSet<Assembly>(new[] {typeof (object).GetTypeInfo().Assembly, typeof(Enumerable).GetTypeInfo().Assembly, typeof(InternalsVisibleToAttribute).GetTypeInfo().Assembly });

        public bool Compiled { get; private set; }

        public static TtlCompileResult InitErrors { get; }

        private static readonly TtlTemplate CodeGenerator;

        private static readonly TtlTemplate PreparseGenerator;

        static CompileContext() {
            try {
                CodeGenerator = new TtlTemplate();
                PreparseGenerator = new TtlTemplate();
                var result = CodeGenerator.Compile(File.ReadAllText(@"CSharpClassTemplate.tcs"));
                var resultPre = PreparseGenerator.Compile(File.ReadAllText(@"CSharpPreparseTemplate.tcs"));
                if (!result.Success || !resultPre.Success) {
                    InitErrors = new TtlCompileResult(false);
                    InitErrors.Errors.AddRange(result.ErrorList);
                    InitErrors.Errors.AddRange(resultPre.ErrorList);
                }
                InitErrors = new TtlCompileResult(true);
            }
            catch (Exception e) {
                InitErrors = new TtlCompileResult(false);
                InitErrors.Errors.Add(new TtlCompileError
                {
                    Error = e.Message,
                    Exception = e
                });
            }
        }

        public Guid ClassGuid { get; } = Guid.NewGuid();

        public string ControllerName { get; set; }

        private readonly List<string> _namespaces = new List<string>();

        private readonly List<DelayedTemplate> _delayedTemplates = new List<DelayedTemplate>();
        private int _method;

        private CompileContext(CompileContext context, string fileName = null, ExType modelType = null)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            ControllerName = context.ControllerName;
            Options = new TemplateOptions(context.Options.FileNamePostfix, context.Options.RootPath,
                fileName ?? context.Options.TemplateName, context.Options.EnableFileChangeCheck, context.Options.AllowCSharp);
            ModelType = modelType ?? context.ModelType ?? typeof(object);
            _namespaces = context._namespaces.ToList();
        }

        public CompileContext(ExType modelType = null) {
            ModelType = modelType ?? (ExType)typeof(object);
            Options = new TemplateOptions();
        }

        /// <summary>
        /// Create new untyped (<see cref="System.Object"/>) initial level context to load and compile template from a file.
        /// Enclosing template level = 0
        /// </summary>
        /// <param name="options"></param>
        public CompileContext(TemplateOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FileNamePostfix))
                throw new ArgumentException("File Name postfix (extension) should not be empty");
            if (string.IsNullOrWhiteSpace(options.RootPath))
                throw new ArgumentException("Root Path (directory) should not be empty");
            if (string.IsNullOrWhiteSpace(options.TemplateName))
                throw new ArgumentException("Template Name should not be empty");
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

        public TemplateOptions Options { get; set; }

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

        public IReadOnlyCollection<string> Namespaces => new ReadOnlyCollection<string>(_namespaces);

        /// <summary>
        /// Compile delayed Extensions, Compile all dynamic property references and connect into template chain.
        /// </summary>
        public virtual void Compile() {
            if (!Compiled)
            {
                foreach (var delayedTemplate in _delayedTemplates)
                {
                    delayedTemplate.ForExtension.CompleteInit(delayedTemplate.NewContext, delayedTemplate.ParseContext);
                }
                _delayedTemplates.Clear();
                if (Options.AllowCSharp && Methods.Count > 0)
                {
                    if (!InitErrors.Success)
                        throw new TemplateCompileException("Cannot compile base C# generation templates", InitErrors.Errors);
                    var code = CodeGenerator.Generate(this);
                    CompiledAssembly = GeneratedAssemblyCache.TryGetCached(code);
                    if (CompiledAssembly != null)
                        return;
                    var tree = CSharpSyntaxTree.ParseText(code);
                    var compilation = CSharpCompilation.Create(ModelType.ToString() + "_" + ClassGuid.ToString("N"), new[] {tree},
#if DNXCORE50 || DNX451
                        NativeHelper.GetMetadataReferences(),
#else
                        DependentAssemblies.Select(MetadataReference.CreateFromAssembly),
#endif
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                    var diagnostics = compilation.GetDiagnostics();
                    if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        throw new TemplateCompileException(FormatErrors(diagnostics));
                    }
                    var stream = new MemoryStream();
                    compilation.Emit(stream);
                    stream.Seek(0, SeekOrigin.Begin);
#if !DNXCORE50
                    CompiledAssembly = Assembly.Load(stream.GetBuffer());
#else
                    CompiledAssembly = NativeHelper.GetAssemblyLoadContext().LoadStream(stream, null);
#endif
                    GeneratedAssemblyCache.AddToCache(code, CompiledAssembly);
                    var classType =
                        CompiledAssembly.GetType(
                            string.Format("Templates.Runtime.CSE_{0}",
                                ClassGuid.ToString("N")));
                    foreach (var expressionCompilation in Methods)
                    {
                        expressionCompilation.RuntimeCallParameter.ParameterImplementation =
                            GatesCache.CreateCompiledDelegate(
                                classType.GetMethod(string.Format("ProcessData_{0}{1}",
                                    expressionCompilation.ExtensionName, expressionCompilation.MethodNumber),
                                    BindingFlags.Public | BindingFlags.Static), expressionCompilation.ModelType.Type,
                                expressionCompilation.ChainedType.Type);
                    }
                }
                Compiled = true;
            }
        }

        private static IEnumerable<TtlCompileError> FormatErrors(ImmutableArray<Diagnostic> diagnostics)
        {
            return diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => new TtlCompileError
                {
                    Error = d.GetMessage()
                })
                .Union(
                    diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                        .Select(d => new TtlCompileWarning
                        {
                            Error = d.GetMessage()
                        }));
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

        internal RuntimeCallParameter PushCompileExpression(ExpressionOptions expressionOptions)
        {
            if (string.IsNullOrEmpty(expressionOptions.Expression))
            {
                throw new ArgumentException("Expression cannot be null or empty");
            }
            RuntimeCallParameter parameter = new RuntimeCallParameter(null, null);
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

        internal ExType ParseAndGetResultType(ExpressionOptions expressionOptions)
        {
            if (string.IsNullOrEmpty(expressionOptions.Expression))
            {
                throw new ArgumentException("Expression cannot be null or empty");
            }
            expressionOptions.Namespaces = Namespaces;
            expressionOptions.ModelType = ModelType;
            var code = PreparseGenerator.Generate(expressionOptions);
            var tree = CSharpSyntaxTree.ParseText(code);
#if DNX451 || DNXCORE50
            var assemblySet = NativeHelper.GetMetadataReferences();
#else
            HashSet<MetadataReference> assemblySet = new HashSet<MetadataReference>
            {
                MetadataReference.CreateFromAssembly(typeof (object).GetTypeInfo().Assembly),
                MetadataReference.CreateFromAssembly(ModelType.Type.GetTypeInfo().Assembly)
            };
            if (ModelType.IsDynamic) 
                MetadataReference.CreateFromAssembly(typeof(CallSite<>).GetTypeInfo().Assembly);
            assemblySet.Add(MetadataReference.CreateFromAssembly(typeof(Enumerable).GetTypeInfo().Assembly));
            if (expressionOptions.ChainedType.IsDynamic)
                MetadataReference.CreateFromAssembly(typeof(CallSite<>).GetTypeInfo().Assembly);
            assemblySet.Add(MetadataReference.CreateFromAssembly(expressionOptions.ChainedType.Type.GetTypeInfo().Assembly));
#endif
            var compilation = CSharpCompilation.Create(null, new[] { tree }, assemblySet);
            var diagnostics = compilation.GetDiagnostics();
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                throw new TemplateCompileException(FormatErrors(diagnostics));
            }
            var syntax = tree.GetRoot().DescendantNodes().OfType<ReturnStatementSyntax>().First().Expression;
            var model = compilation.GetSemanticModel(tree);
            var typeInfo = model.GetTypeInfo(syntax);
            if (typeInfo.Type.IsAnonymousType || typeInfo.Type.TypeKind == TypeKind.Dynamic)
            {
                return ExType.Dynamic;
            }
            return
                ReflectionHelper.ResolveType(typeInfo.Type.ToDisplayString(DisplayFormat), _namespaces.ToArray());
        }

        internal List<ExpressionCompilation> Methods { get; } = new List<ExpressionCompilation>();

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
        }
    }
}