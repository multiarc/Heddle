using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.FileProviders;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Helpers;
using Heddle.Native;
using Heddle.Runtime.Parameters;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

namespace Heddle.Runtime
{
    public class CSharpContext
    {
        // Phase 9 D4: the only Microsoft.CodeAnalysis-typed static of this class lives in a nested holder so it is
        // reached (and its type-initializer run) only from the C#-tier body behind the feature switch — a trimmed
        // publish with the switch off drops the holder with the rest of the Roslyn graph.
        private static class RoslynDisplay
        {
            internal static readonly SymbolDisplayFormat Format =
                new SymbolDisplayFormat(SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                    kindOptions: SymbolDisplayKindOptions.None,
                    delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
                    extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
                    localOptions: SymbolDisplayLocalOptions.None, memberOptions: SymbolDisplayMemberOptions.None,
                    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                    miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);
        }

        private static readonly HeddleTemplate PreparseGenerator;

        private static readonly ConcurrentDictionary<string, Tuple<OptionalValue<object>, ExType>> PrecompilationCache =
            new ConcurrentDictionary<string, Tuple<OptionalValue<object>, ExType>>();

        static CSharpContext()
        {
            string document = null;
            try
            {
                PreparseGenerator = new HeddleTemplate();
                var path = $"{AppContext.BaseDirectory}/CSharpPreparseTemplate.tcs";
                if (File.Exists(path))
                {
                    document = File.ReadAllText(path);
                }
                else
                {
                    var provider = new EmbeddedFileProvider(typeof(ContextCompilation).Assembly,
                        "Heddle.LanguageTemplates");
                    var fileInfo = provider.GetFileInfo("CSharpPreparseTemplate.tcs");
                    using var embeddedTemplate = fileInfo.CreateReadStream();
                    if (embeddedTemplate != null)
                    {
                        var templateReader = new StreamReader(embeddedTemplate, Encoding.Unicode);
                        document = templateReader.ReadToEnd();
                    }
                }

                InitErrors = PreparseGenerator.Compile(document);
            }
            catch (Exception e)
            {
                InitErrors = new HeddleCompileResult(false, document, null);
                InitErrors.Errors.Add(new HeddleCompileError
                {
                    Error = e.Message,
                    Exception = e
                });
            }
        }

        public static HeddleCompileResult InitErrors { get; }

        public ICollection<string> Namespaces => _namespaces;

        private readonly HashSet<string> _namespaces = new HashSet<string>();

        public Guid ClassGuid { get; } = Guid.NewGuid();
        public Assembly CompiledAssembly { get; set; }
        public bool Compiled { get; internal set; }
        private int _lastMethodNumber;
        internal List<ExpressionCompilation> Methods { get; } = new List<ExpressionCompilation>();

        public void ImportNamespace(string parameterTemplate)
        {
            if (!string.IsNullOrEmpty(parameterTemplate) && !_namespaces.Contains(parameterTemplate))
                _namespaces.Add(parameterTemplate);
        }

        internal IRuntimeParameter PushCompileExpression(ExpressionOptions expressionOptions,
            CompileContext compileContext)
        {
            if (string.IsNullOrEmpty(expressionOptions.Expression))
            {
                throw new ArgumentException(
                    $"[{expressionOptions.Position}]<{expressionOptions.ExtensionName}> Expression cannot be null or empty");
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
            return parameter;
        }

        internal OptionalValue<object> ParseAndGetResultType(CompileContext context,
            ExpressionOptions expressionOptions,
            out ExType objectType)
        {
            if (string.IsNullOrEmpty(expressionOptions.Expression))
            {
                throw new ArgumentException(
                    $"[{expressionOptions.Position}]<{expressionOptions.ExtensionName}> Expression cannot be null or empty");
            }

            // Phase 9 D4 — the parse-time Roslyn entry. When the C#-tier feature switch is trimmed off, this is a
            // constant-true early return, so the Roslyn body below (and the ITypeSymbol-typed ResolveTypeReference)
            // become dead code the linker removes. The method still records the C# expression; the single HED9001 is
            // surfaced later by ContextCompilation.Compile. Behavior is identical when the switch is left on (default).
            if (!HeddleFeatures.CSharpTierEnabled)
            {
                objectType = ExType.Dynamic;
                return new OptionalValue<object>(null, false);
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
            var result = PrecompilationCache.GetOrAdd(generatedCode, code =>
            {
                var tree = CSharpSyntaxTree.ParseText(code);
                var assemblySet = AssemblyHelper.GetApplicationReferences();
                var compilation = CSharpCompilation.Create(null, new[] {tree}, assemblySet);
                var diagnostics = compilation.GetDiagnostics();
                if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                {
                    context.CompileErrors.AddRange(ContextCompilation.FormatErrors(diagnostics,
                        expressionOptions.Position));
                    return new Tuple<OptionalValue<object>, ExType>(new OptionalValue<object>(null, false),
                        typeof(object));
                }

                // The expression we evaluate is the body of the generated PreProcessData wrapper:
                //   return <Expression>;
                // A statement-bodied lambda (or any nested 'return') in <Expression> adds further
                // ReturnStatementSyntax nodes, so we must select the wrapper's own return - the one whose
                // immediate parent is the method body block - rather than assuming a single return exists.
                var syntax = tree.GetRoot().DescendantNodes()
                    .OfType<ReturnStatementSyntax>()
                    .First(r => r.Parent is BlockSyntax block
                                && block.Parent is MethodDeclarationSyntax method
                                && method.Identifier.Text == "PreProcessData")
                    .Expression;
                var model = compilation.GetSemanticModel(tree, false);
                var constantValue = model.GetConstantValue(syntax);
                if (constantValue.HasValue)
                {
                    return new Tuple<OptionalValue<object>, ExType>(constantValue.Value,
                        constantValue.Value?.GetType() ?? typeof(object));
                }

                var typeInfo = model.GetTypeInfo(syntax);
                if (typeInfo.Type?.IsAnonymousType == true || typeInfo.Type?.TypeKind == TypeKind.Dynamic)
                {
                    return new Tuple<OptionalValue<object>, ExType>(new OptionalValue<object>(null, false),
                        ExType.Dynamic);
                }

                var objType = ResolveTypeReference(context, expressionOptions, typeInfo.Type);
                return new Tuple<OptionalValue<object>, ExType>(new OptionalValue<object>(null, false), objType);
            });
            objectType = result.Item2;
            return result.Item1;
        }

        private ExType ResolveTypeReference(CompileContext context,
            ExpressionOptions expressionOptions, ITypeSymbol type)
        {
            ExType objType;
            if (type?.IsTupleType == true && type is INamedTypeSymbol tupleType)
            {
                var types = tupleType.TupleUnderlyingType?.TypeArguments;
                if (types.HasValue)
                {
                    var resolvedTypes = types.Value.Select(x => ResolveTypeReference(context, expressionOptions, x))
                        .ToArray();

                    try
                    {
                        objType = Type.GetType($"System.ValueTuple`{resolvedTypes.Length}", true)
                            .MakeGenericType(resolvedTypes.Select(x => x.Type).ToArray());
                    }
                    catch (InvalidOperationException e)
                    {
                        context.CompileErrors.Add(e.ToError(expressionOptions.Position));
                        objType = typeof(object);
                    }

                    return objType;
                }
            }

            string typeName = type?.ToDisplayString(RoslynDisplay.Format);
            try
            {
                objType =
                    ReflectionHelper.ResolveType(typeName, _namespaces.ToArray());
            }
            catch (InvalidOperationException e)
            {
                context.CompileErrors.Add(e.ToError(expressionOptions.Position));
                objType = typeof(object);
            }

            return objType;
        }

        private static string FormatAssemblyName(AssemblyName assemblyName, bool fullPublic = true)
        {
            if (!fullPublic)
                return assemblyName.FullName;
            var publicKey = assemblyName.GetPublicKey();
            if (publicKey == null)
            {
                return assemblyName.Name;
            }

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
                var systemAssemblyName = AssemblyHelper.GetAssemblyName("System.Runtime");
                if (systemAssemblyName != null)
                    yield return
                        $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = AssemblyHelper.GetAssemblyName("System");
                if (systemAssemblyName != null)
                    yield return
                        $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = typeof(object).GetTypeInfo().Assembly.GetName();
                yield return $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = AssemblyHelper.GetAssemblyName("Microsoft.CSharp");
                if (systemAssemblyName != null)
                    yield return
                        $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
                systemAssemblyName = AssemblyHelper.GetAssemblyName("System.Core");
                if (systemAssemblyName != null)
                    yield return
                        $"{systemAssemblyName.Name},PublicKey={systemAssemblyName.GetPublicKey().ToHexString()}";
            }
        }
    }
}