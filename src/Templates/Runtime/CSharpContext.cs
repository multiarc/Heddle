using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Templates.Native;
using Templates.Runtime.Parameters;

namespace Templates.Runtime
{
    public class CSharpContext
    {
        internal static readonly SymbolDisplayFormat DisplayFormat =
            new SymbolDisplayFormat(SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                kindOptions: SymbolDisplayKindOptions.None,
                delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
                extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
                localOptions: SymbolDisplayLocalOptions.None, memberOptions: SymbolDisplayMemberOptions.None,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

        private static readonly TtlTemplate PreparseGenerator;

        private static readonly ConcurrentDictionary<string, Tuple<OptionalValue<object>, ExType>> _precompilationCache =
            new ConcurrentDictionary<string, Tuple<OptionalValue<object>, ExType>>();

        static CSharpContext()
        {
            string document = null;
            try
            {
                PreparseGenerator = new TtlTemplate();
                IApplicationEnvironment env = PlatformServices.Default.Application;
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

        public static TtlCompileResult InitErrors { get; }

        public ICollection<string> Namespaces =>  _namespaces;

        private readonly HashSet<string> _namespaces = new HashSet<string>();

        public Guid ClassGuid { get; } = Guid.NewGuid();
        public Assembly CompiledAssembly { get; set; }
        public bool Compiled { get; internal set; }
        private int _lastMethodNumber;
        internal List<ExpressionCompilation> Methods { get; } = new List<ExpressionCompilation>();

        public HashSet<Assembly> DependentAssemblies { get; }
            =
            new HashSet<Assembly>(new[]
            {
                typeof (object).GetTypeInfo().Assembly, typeof (Enumerable).GetTypeInfo().Assembly,
                typeof (InternalsVisibleToAttribute).GetTypeInfo().Assembly
            });

        public void ImportNamespace(string parameterTemplate)
        {
            if (!string.IsNullOrEmpty(parameterTemplate) && !_namespaces.Contains(parameterTemplate))
                _namespaces.Add(parameterTemplate);
        }

        internal IRuntimeParameter PushCompileExpression(ExpressionOptions expressionOptions, CompileContext compileContext)
        {
            if (string.IsNullOrEmpty(expressionOptions.Expression))
            {
                throw new ArgumentException($"[{expressionOptions.Position}]<{expressionOptions.ExtensionName}> Expression cannot be null or empty");
            }
            IRuntimeParameter parameter = new CompiledParameter();
            Methods.Add(new ExpressionCompilation(expressionOptions)
            {
                RuntimeCallParameter = parameter,
                ModelType = compileContext.ScopeType,
                RootModelType = compileContext.RootScopeType,
                MethodNumber = _lastMethodNumber
            });
            _lastMethodNumber++;
            DependentAssemblies.Add(compileContext.ScopeType.Type.GetTypeInfo().Assembly);
            if (expressionOptions.ChainedType.IsDynamic)
                DependentAssemblies.Add(typeof(CallSite<>).GetTypeInfo().Assembly);
            DependentAssemblies.Add(expressionOptions.ChainedType.Type.GetTypeInfo().Assembly);
            return parameter;
        }

        internal OptionalValue<object> ParseAndGetResultType(CompileContext context, ExpressionOptions expressionOptions,
            out ExType objectType)
        {
            if (string.IsNullOrEmpty(expressionOptions.Expression))
            {
                throw new ArgumentException(
                    $"[{expressionOptions.Position}]<{expressionOptions.ExtensionName}> Expression cannot be null or empty");
            }
            expressionOptions.ModelType = context.ScopeType;
            expressionOptions.RootModelType = context.RootScopeType;
            ImportNamespace(expressionOptions.ModelType.Type.Namespace);
            ImportNamespace(expressionOptions.ChainedType.Type.Namespace);
            var modelTypeInfo = expressionOptions.ModelType.Type.GetTypeInfo();
            if (modelTypeInfo.IsGenericType)
            {
                foreach (var type in modelTypeInfo.GenericTypeArguments)
                {
                    ImportNamespace(type.Namespace);
                }
            }
            var chainTypeInfo = expressionOptions.ChainedType.Type.GetTypeInfo();
            if (chainTypeInfo.IsGenericType)
            {
                foreach (var type in chainTypeInfo.GenericTypeArguments)
                {
                    ImportNamespace(type.Namespace);
                }
            }
            expressionOptions.Namespaces = Namespaces;
            if (!InitErrors.Success)
                throw new TemplateCompileException("Cannot compile base C# generation templates",
                    InitErrors.Errors);
            var generatedCode = PreparseGenerator.Generate(expressionOptions);
            var result = _precompilationCache.GetOrAdd(generatedCode, code =>
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var assemblySet = AssemblyHelper.GetMetadataReferences();
                var compilation = CSharpCompilation.Create(null, new[] {tree}, assemblySet);
                var diagnostics = compilation.GetDiagnostics();
                if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                {
                    context.CompileErrors.AddRange(ContextCompilation.FormatErrors(diagnostics, expressionOptions.Position));
                    return new Tuple<OptionalValue<object>, ExType>(new OptionalValue<object>(null, false), typeof (object));
                }
                var syntax = tree.GetRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;
                var model = compilation.GetSemanticModel(tree, false);
                var constantValue = model.GetConstantValue(syntax);
                if (constantValue.HasValue)
                {
                    return new Tuple<OptionalValue<object>, ExType>(constantValue.Value, constantValue.Value?.GetType() ?? typeof (object));
                }
                var typeInfo = model.GetTypeInfo(syntax);
                if (typeInfo.Type.IsAnonymousType || typeInfo.Type.TypeKind == TypeKind.Dynamic)
                {
                    return new Tuple<OptionalValue<object>, ExType>(new OptionalValue<object>(null, false), ExType.Dynamic);
                }
                string typeName = typeInfo.Type.ToDisplayString(DisplayFormat);
                ExType objType;
                try
                {
                    objType =
                        ReflectionHelper.ResolveType(typeName, _namespaces.ToArray());
                }
                catch (InvalidOperationException e)
                {
                    context.CompileErrors.Add(e.ToError(expressionOptions.Position));
                    objType = typeof (object);
                }
                return new Tuple<OptionalValue<object>, ExType>(new OptionalValue<object>(null, false), objType);
            });
            objectType = result.Item2;
            return result.Item1;
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
#if NETSTANDARD1_5
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
    }
}