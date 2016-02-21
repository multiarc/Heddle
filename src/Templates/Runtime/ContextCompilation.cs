using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.PlatformAbstractions;
using Templates.Data;
using Templates.Exceptions;
using Templates.Native;
using Templates.Runtime.Parameters;
using Templates.Strings.Core;

namespace Templates.Runtime
{
    internal static class ContextCompilation
    {
        private static readonly TtlTemplate CodeGenerator;

        public static TtlCompileResult InitErrors { get; }

        static ContextCompilation()
        {
            string document = null;
            try
            {
                CodeGenerator = new TtlTemplate();
                IApplicationEnvironment env =
                    (IApplicationEnvironment)
                        CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof (IApplicationEnvironment));
                var path = env.ApplicationBasePath + "/";
                document = File.ReadAllText($"{path}CSharpClassTemplate.tcs");
                InitErrors =
                    CodeGenerator.Compile(document);

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

        public static void Compile(this CompileContext context)
        {
            if (!context.Compiled)
            {
                foreach (var delayedTemplate in context.DelayedTemplates)
                {
                    delayedTemplate.ForExtension.CompleteInit(delayedTemplate.NewScope, delayedTemplate.ParseContext);
                }
                context.DelayedTemplates.Clear();
                context.Compiled = true;
            }
        }

        /// <summary>
        /// Compile delayed Extensions, Compile all dynamic property references and connect into template chain.
        /// </summary>
        public static void Compile(this CompileScope context)
        {
            Compile(context.CompileContext);
            if (context.CompileContext.Options.AllowCSharp && context.CSharpContext.Methods.Count > 0 && !context.CSharpContext.Compiled)
            {
                if (!InitErrors.Success)
                    throw new TemplateCompileException("Cannot compile base C# generation templates",
                        InitErrors.Errors);
                var code = CodeGenerator.Generate(context.CSharpContext);
                context.CSharpContext.CompiledAssembly = GeneratedAssemblyCache.TryGetCached(code);
                if (context.CSharpContext.CompiledAssembly != null)
                    return;
                var tree = CSharpSyntaxTree.ParseText(code);
                var compilation = CSharpCompilation.Create(
                    context.CompileContext.ScopeType + "_" + context.CSharpContext.ClassGuid.ToString("N"), new[] {tree},
                    AssemblyHelper.GetMetadataReferences(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithCryptoPublicKey(
                        context.GetType().GetTypeInfo().Assembly.GetName().GetPublicKey().ToImmutableArray())
                        .WithDelaySign(false));
                var diagnostics = compilation.GetDiagnostics();
                if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                {
                    context.CompileContext.CompileErrors.AddRange(FormatErrors(diagnostics,
                        context.CSharpContext.Methods.First().Position));
                    return;
                }
                var stream = new MemoryStream();
                compilation.Emit(stream);
                stream.Seek(0, SeekOrigin.Begin);
#if !DOTNET5_4
                context.CSharpContext.CompiledAssembly = Assembly.Load(stream.GetBuffer());
#else
                    context.CSharpContext.CompiledAssembly = AssemblyHelper.GetAssemblyLoadContext().LoadStream(stream, null);
#endif
                GeneratedAssemblyCache.AddToCache(code, context.CSharpContext.CompiledAssembly);
                var classType =
                    context.CSharpContext.CompiledAssembly.GetType($"Templates.Runtime.CSE_{context.CSharpContext.ClassGuid.ToString("N")}");
                int methodNumber = 0;
                foreach (var expressionCompilation in context.CSharpContext.Methods)
                {
                    var compiledParameter = expressionCompilation.RuntimeCallParameter as CompiledParameter;
                    if (compiledParameter == null) continue;
                    compiledParameter.ParameterImplementation =
                        GatesCache.CreateCompiledDelegate(
                            classType.GetMethod(
                                $"ProcessData_{expressionCompilation.ExtensionName}{methodNumber}",
                                BindingFlags.Public | BindingFlags.Static), expressionCompilation.ModelType.Type,
                            expressionCompilation.ChainedType.Type, expressionCompilation.RootModelType.Type);
                    methodNumber++;
                }
                context.CSharpContext.Compiled = true;
            }
        }

        internal static IEnumerable<TtlCompileError> FormatErrors(ImmutableArray<Diagnostic> diagnostics, BlockPosition position)
        {
            return
                diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => new TtlCompileError {Error = d.GetMessage()})
                    .Union(
                        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                            .Select(d => new TtlCompileWarning {Error = d.GetMessage(), Position = position}));
        }
    }
}