using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 partials (README D7): a precompiled <c>@partial(){{name}}</c> resolves its child lazily —
    /// registry first (a precompiled child), dynamic compile second (a runtime-compiled child) — through
    /// <c>PrecompiledRuntime.ResolvePartial</c>, and splices its output exactly as the runtime
    /// <c>PartialExtension.InnerTemplate.Generate</c> does. Both mixed-mode directions are differential-gated.
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class PartialTests
    {
        private static MethodInfo EntryFor(Assembly asm, string key)
        {
            var sanitized = Sanitize(key);
            foreach (var type in asm.GetTypes())
            {
                if (!type.IsClass || !type.IsAbstract || !type.IsSealed) continue;
                if (type.Namespace != DifferentialHarness.GeneratedNamespace) continue;
                if (type.Name == sanitized)
                    // Phase 8: three Generate overloads (string + two sinks) — select the string-returning entry.
                    return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "Generate" && m.ReturnType == typeof(string));
            }
            return null;
        }

        private static string Sanitize(string key)
        {
            var file = key.Contains('/') ? key.Substring(key.LastIndexOf('/') + 1) : key;
            var dot = file.LastIndexOf('.');
            if (dot > 0) file = file.Substring(0, dot);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < file.Length; i++)
            {
                var c = file[i];
                bool valid = c == '_' || char.IsLetter(c) || (i > 0 && char.IsDigit(c));
                if (i == 0 && char.IsDigit(c)) sb.Append('_').Append(c);
                else if (valid) sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
                else sb.Append('_');
            }
            return sb.Length == 0 ? "_" : sb.ToString();
        }

        [Fact]
        public void PrecompiledParentRendersPrecompiledChild()
        {
            // Both templates precompile; registering the manifest lets ResolvePartial bind the child from the registry.
            var parentKey = "ptest-parent-a.heddle";
            var childKey = "ptest-child-a.heddle";
            var parent = "BEFORE @partial(){{ptest-child-a}} AFTER\n";
            var child = "[child says hi]";

            var gen = DifferentialHarness.Generate(new[] { (parentKey, parent), (childKey, child) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.NotNull(gen.Assembly);
            PrecompiledTemplates.Register(gen.Assembly);

            var entry = EntryFor(gen.Assembly, parentKey);
            Assert.NotNull(entry);
            var precompiled = (string) entry.Invoke(null, new object[] { null, null, null });

            var dyn = RenderDynamicWithDiskChildren(parent, new[] { ("ptest-child-a", child) });
            Assert.Equal(dyn, precompiled);
        }

        [Fact]
        public void PrecompiledParentRendersDynamicChildFromDisk()
        {
            // The child is NOT registered; ResolvePartial dynamic-compiles it from disk under the ambient options the
            // resolver-style GenerateString overload establishes (mixed mode, README D7).
            var parentKey = "ptest-parent-b.heddle";
            var parent = "X@partial(){{ptest-child-b}}Y\n";
            var child = "<dynamic-child>";

            var gen = DifferentialHarness.Generate(new[] { (parentKey, parent) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
            Assert.NotNull(gen.Assembly);

            var dir = Path.Combine(Path.GetTempPath(), "heddle_ptest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "ptest-child-b.heddle"), child);
                var options = new TemplateOptions { RootPath = dir + Path.DirectorySeparatorChar, FileNamePostfix = ".heddle" };

                var entry = EntryFor(gen.Assembly, parentKey);
                var root = entry.DeclaringType.GetField("Root", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public)
                    .GetValue(null);
                var precompiled = PrecompiledRuntime.GenerateString((Heddle.Runtime.IProcessStrategy) root, null, null, null, options);

                var dynTemplate = new HeddleTemplate(parent, new CompileContext(options));
                Assert.True(dynTemplate.CompileResult.Success, dynTemplate.CompileResult.ToString());
                var dyn = dynTemplate.Generate(null);

                Assert.Equal(dyn, precompiled);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static string RenderDynamicWithDiskChildren(string parent, (string name, string content)[] children)
        {
            var dir = Path.Combine(Path.GetTempPath(), "heddle_ptest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                foreach (var (name, content) in children)
                {
                    var rel = name.Replace('/', Path.DirectorySeparatorChar) + ".heddle";
                    var full = Path.Combine(dir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    File.WriteAllText(full, content);
                }

                var options = new TemplateOptions { RootPath = dir + Path.DirectorySeparatorChar, FileNamePostfix = ".heddle" };
                var t = new HeddleTemplate(parent, new CompileContext(options));
                Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
                return t.Generate(null);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
