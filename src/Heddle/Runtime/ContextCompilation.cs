using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Native;
using Heddle.Runtime.Parameters;
using Heddle.Strings.Core;
using Platform = Microsoft.CodeAnalysis.Platform;
using Microsoft.Extensions.FileProviders;

namespace Heddle.Runtime
{
    internal static class ContextCompilation
    {
        /// <summary>Serialises expression-class compilation. It used to guard a cache as well; that cache could
        /// never be read from — see <see cref="CompileCSharp"/>.</summary>
        private static readonly object LockObj = new object();

        private static readonly HeddleTemplate CodeGenerator;

        public static HeddleCompileResult InitErrors { get; }

        static ContextCompilation()
        {
            string document = null;
            try
            {
                CodeGenerator = new HeddleTemplate();
                var path = $"{AppContext.BaseDirectory}/CSharpClassTemplate.tcs";
                
                if (File.Exists(path))
                {
                    document = File.ReadAllText(path);
                }
                else
                {
                    var provider = new EmbeddedFileProvider(typeof(ContextCompilation).Assembly, "Heddle.LanguageTemplates");
                    var fileInfo = provider.GetFileInfo("CSharpClassTemplate.tcs");
                    using var embeddedTemplate = fileInfo.CreateReadStream();
                    if (embeddedTemplate != null)
                    {
                        var templateReader = new StreamReader(embeddedTemplate, Encoding.Unicode);
                        document = templateReader.ReadToEnd();
                    }
                }

                InitErrors =
                    CodeGenerator.Compile(document,
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
            if (context.CompileContext.Options.ExpressionMode == ExpressionMode.FullCSharp && context.CSharpContext.Methods.Count > 0 &&
                !context.CSharpContext.Compiled)
            {
                // When feature switch is off, this guard is constant-true and dead code is linker-eliminated.
                if (!HeddleFeatures.CSharpTierEnabled)
                {
                    context.CompileContext.CompileErrors.Add(new HeddleCompileError
                    {
                        Error =
                            "The C# expression tier is not available in this host: the 'Heddle.CSharpTierEnabled' " +
                            "feature switch is disabled. Rewrite the template using native expressions " +
                            "(ExpressionMode.Native), or run it in a host with the C# tier enabled.",
                        DiagnosticId = HeddleFeatures.CSharpTierDisabledDiagnosticId,
                        Position = default(BlockPosition)
                    });
                    return;
                }

                CompileCSharp(context);
            }
        }

        private static void CompileCSharp(CompileScope context)
        {
            {
                if (!InitErrors.Success)
                    throw new TemplateCompileException("Cannot compile base C# generation templates",
                        InitErrors.Errors);
                var code = CodeGenerator.Generate(context.CSharpContext);

                // There was a cache keyed on this source. It could not work: the source declares a class named
                // after CSharpContext.ClassGuid, a fresh Guid per context, so the key was unique per compile and
                // measurably never hit — eight compiles of three distinct expressions produced eight adds and no
                // reads. All it did was retain every generated source string for the life of the process. A hit
                // would in fact have been worse than a miss: the entry class is looked up below by *this* context's
                // guid, which another context's assembly does not contain.
                {
                    lock (LockObj)
                    {
                        {
                            var tree = CSharpSyntaxTree.ParseText(code);
                            var compilation = CSharpCompilation.Create(
                                context.CompileContext.ScopeType + "_" + context.CSharpContext.ClassGuid.ToString("N"),
                                new[] {tree},
                                AssemblyHelper.GetApplicationReferences(),
                                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                                    .WithSpecificDiagnosticOptions(
                                        new Dictionary<string, ReportDiagnostic>
                                        {
                                            {"CS1701", ReportDiagnostic.Suppress}, // Binding redirects
                                            {"CS1702", ReportDiagnostic.Suppress},
                                            {"CS1705", ReportDiagnostic.Suppress}
                                        }).WithOptimizationLevel(OptimizationLevel.Release)
                                    .WithGeneralDiagnosticOption(ReportDiagnostic.Default)
                                    .WithPlatform(Platform.AnyCpu));
                            var diagnostics = compilation.GetDiagnostics();
                            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                            {
                                context.CompileContext.CompileErrors.AddRange(FormatErrors(diagnostics,
                                    context.CSharpContext.Methods.First().Position));
                                return;
                            }

                            using (var codeStream = new MemoryStream())
                            {
                                using var symbolStream = new MemoryStream();
                                var results = compilation.Emit(codeStream, symbolStream,
                                    options: new EmitOptions(
                                        debugInformationFormat: DebugInformationFormat.PortablePdb));
                                if (!results.Success)
                                {
                                    context.CompileContext.CompileErrors.AddRange(FormatErrors(results.Diagnostics,
                                        context.CSharpContext.Methods.First().Position));
                                    return;
                                }

                                context.CSharpContext.CompiledAssembly =
                                    Assembly.Load(codeStream.ToArray(), symbolStream.ToArray());
                                // Ours, not the host's: observation must not map its types.
                                Native.AssemblyHelper.MarkEngineEmitted(context.CSharpContext.CompiledAssembly);
                            }

                        }
                    }
                }

                var classType =
                    context.CSharpContext.CompiledAssembly.GetType(
                        $"Heddle.Runtime.CSE_{context.CSharpContext.ClassGuid:N}");
                var methodNumber = 0;
                foreach (var expressionCompilation in context.CSharpContext.Methods)
                {
                    if (expressionCompilation.RuntimeCallParameter is CompiledParameter compiledParameter)
                    {
                        var method = classType.GetMethod(
                            $"ProcessData_{expressionCompilation.ExtensionName}{methodNumber}",
                            BindingFlags.Public | BindingFlags.Static);
                        if (method != null)
                        {
                            var modelParameter = Expression.Parameter(typeof(object));
                            var chainedParameter = Expression.Parameter(typeof(object));
                            var rootParameter = Expression.Parameter(typeof(object));
                            compiledParameter.ParameterImplementation = Expression
                                .Lambda<Func<object, object, object, object>>(Expression.Convert(Expression.Call(null,
                                            method,
                                            Expression.Convert(modelParameter, expressionCompilation.ModelType.Type),
                                            Expression.Convert(chainedParameter,
                                                expressionCompilation.ChainedType.Type),
                                            Expression.Convert(rootParameter,
                                                expressionCompilation.RootModelType.Type)),
                                        typeof(object)),
                                    modelParameter,
                                    chainedParameter,
                                    rootParameter).Compile();
                            methodNumber++;
                        }
                    }
                }

                context.CSharpContext.Compiled = true;
            }
        }

        internal static IEnumerable<HeddleCompileError> FormatErrors(ImmutableArray<Diagnostic> diagnostics, BlockPosition position)
        {
            return
                diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => new HeddleCompileError {Error = d.GetMessage(), Position = position })
                    .Union(
                        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                            .Select(d => new HeddleCompileWarning {Error = d.GetMessage(), Position = position}));
        }
    }
}