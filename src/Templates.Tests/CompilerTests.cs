using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;
using AssemblyHelper = Templates.Native.AssemblyHelper;

namespace Templates.Tests
{
    public class CompilerTests
    {
        //[Fact]
        public void RoslynBadImageFormat()
        {
            string code = @"
    namespace Tests
    {
        public static class TestClass
        {
            private static void Main(string[] args) { }

            public static object Test(dynamic value)
            {
                return value.SubCategories.Count > 0;
            }
        }
    }";
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                Guid.NewGuid().ToString("N"), new[] {tree},
                AssemblyHelper.GetApplicationReferences().Where(m => !m.Display.Contains("Templates")),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithOptimizationLevel(OptimizationLevel.Release)
                    .WithGeneralDiagnosticOption(ReportDiagnostic.Default)
                    .WithPlatform(Platform.AnyCpu));
            var diagnostics = compilation.GetDiagnostics();
            if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                throw new Exception(string.Join("\n", diagnostics.Select(d => d.GetMessage())));
            }
            using (var codeStream = new MemoryStream())
            {
                using (var symbolStream = new MemoryStream())
                {
                    var results = compilation.Emit(codeStream, symbolStream,
                            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
                    if (!results.Success)
                    {
                        throw new Exception(string.Join("\n", diagnostics.Select(d => d.GetMessage())));
                    }
                    codeStream.Seek(0, SeekOrigin.Begin);
                    symbolStream.Seek(0, SeekOrigin.Begin);
                    //try
                    //{
                    new AssemblyHelper.TemplateLoadContext().Load(codeStream, symbolStream);
                    //}
                    //catch (BadImageFormatException)
                    //{
                    //    throw new InvalidOperationException(
                    //        $"{string.Join("\n", AssemblyHelper.GetApplicationReferences().Where(m => !m.Display.Contains("Templates")).Select(r => r.Display))}");
                    //}
                }
            }
        }
    }
}
