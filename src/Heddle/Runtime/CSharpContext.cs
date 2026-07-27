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
        // Roslyn statics live in a nested holder so they initialize only when C# tier is enabled and can be trimmed when disabled.
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

        private static readonly ConcurrentDictionary<string, PreparseResult> PrecompilationCache =
            new ConcurrentDictionary<string, PreparseResult>();

        /// <summary>
        /// One preparse outcome: what the expression evaluated to, its type, and the diagnostics producing it raised.
        /// <para>The diagnostics belong to the entry because the same generated code must report the same thing every
        /// time it is compiled. Caching only the value made diagnostics an accident of ordering — the first caller
        /// received them and every later one silently got none.</para>
        /// <para><b>Only the message survives; the position does not.</b> The key is the generated C#, which says
        /// nothing about where in which document the expression sits, so replaying a stored position stamped the
        /// first caller's coordinates onto every later one — a one-line document being told its error is on line
        /// four. Each caller re-stamps its own. This was a real regression the first time these were cached, and it
        /// was worse than the fault it replaced: an editor navigates by position.</para>
        /// <para><see cref="Generation"/> is the observed-assembly generation the entry was produced under. A
        /// failure that only failed because an assembly had not been registered yet must not outlive the
        /// registration — cached forever, it turned a fault that healed on the next compile into a permanent one
        /// decided by load order.</para>
        /// </summary>
        private sealed class PreparseResult
        {
            public PreparseResult(OptionalValue<object> value, ExType type, string[] diagnostics, int generation)
            {
                Value = value;
                Type = type;
                Diagnostics = diagnostics;
                Generation = generation;
            }

            public OptionalValue<object> Value { get; }
            public ExType Type { get; }
            public string[] Diagnostics { get; }
            public int Generation { get; }

            public bool Failed => Diagnostics.Length > 0;
        }

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

                InitErrors = PreparseGenerator.Compile(document,
                    new CompileContext(new TemplateOptions
                    {
                        OutputProfile = OutputProfile.Text,
                        TrimDirectiveLines = false
                    }));
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

            // When feature switch is trimmed off, this becomes constant-true and Roslyn code below becomes dead code removed by linker.
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
            var generation = Native.AssemblyHelper.Generation;
            if (!PrecompilationCache.TryGetValue(generatedCode, out var cached) || IsStale(cached, generation))
            {
                var firstDiagnostic = context.CompileErrors.Count;
                var preparsed = Preparse(generatedCode, context, expressionOptions);
                cached = new PreparseResult(preparsed.Item1, preparsed.Item2,
                    context.CompileErrors.Skip(firstDiagnostic).Select(e => e.Error).ToArray(), generation);
                PrecompilationCache[generatedCode] = cached;
            }
            else
            {
                foreach (var message in cached.Diagnostics)
                    context.CompileErrors.Add(message.ToError(expressionOptions.Position));
            }

            objectType = cached.Type;
            return cached.Value;
        }

        /// <summary>
        /// A cached <b>failure</b> is only trustworthy while the assembly set that produced it is. An expression
        /// naming a type in an assembly the host had not registered yet fails, and must be retried once it has been —
        /// otherwise the first attempt decides the answer for the life of the process, and whether a template
        /// compiles comes down to load order. A cached success stays valid: nothing a later registration adds can
        /// take a type away.
        /// </summary>
        private static bool IsStale(PreparseResult cached, int generation)
        {
            return cached.Failed && cached.Generation != generation;
        }

        private Tuple<OptionalValue<object>, ExType> Preparse(string code, CompileContext context,
            ExpressionOptions expressionOptions)
        {
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

                // Select the wrapper's return (parent is method body block), not any return in the expression itself.
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
            }
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