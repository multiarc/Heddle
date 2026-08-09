extern alias generator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Precompiled partials, with the tier pinned in every case. A precompiled <c>@partial</c> constructs the real
    /// <c>PartialExtension</c> and runs its real <c>InitStart</c>/<c>CompleteInit</c> at static init: the name is
    /// evaluated by the hook off the body the seam supplies it, the child is queued on the engine's own
    /// delayed-template queue, and <c>CompleteInit</c> takes delivery of it through the engine's child supply —
    /// the registry when the child is precompiled, the engine's own dynamic compile when it is not. Nothing about
    /// the extension is reproduced by generated code.
    /// <para>Every case here pins which tier answered. A registry-only resolver root is what makes the
    /// precompiled-child case conclusive: the resolver's <c>RootPath</c> does not exist, so a child served off
    /// disk would throw rather than pass, and only the registry can answer.</para>
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
                    // Three Generate overloads exist (string + two sinks) — select the string-returning entry.
                    return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "Generate" && m.ReturnType == typeof(string));
            }
            return null;
        }

        /// <summary>The generator's own <c>SanitizeName</c>. This used to be a simplified, already-divergent copy —
        /// it dropped the directory-segment handling entirely — which is exactly how a hand-synchronized
        /// "independent oracle" fails.</summary>
        private static string Sanitize(string key) =>
            generator::Heddle.Generator.HeddleTemplateGenerator.SanitizeName(key);

        /// <summary>Writes the given templates into a fresh temp directory and returns it; the caller deletes it.
        /// This is the disk the dynamic reference reads, and — for the mixed-mode cases — the disk the precompiled
        /// tier's own child compile reads too.</summary>
        internal static string Stage(params (string name, string content)[] files)
        {
            var dir = Path.Combine(Path.GetTempPath(), "heddle_ptest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            foreach (var (name, content) in files)
            {
                var full = Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar) + ".heddle");
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, content);
            }

            return dir;
        }

        internal static void TryDelete(string dir)
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        /// <summary>Both halves precompile and register, and the resolver root does not exist — so the child can
        /// only have come from the registry.</summary>
        [Fact]
        public void APrecompiledParentRendersItsPrecompiledChildFromTheRegistry()
        {
            const string parentKey = "ptest-parent-a.heddle";
            const string childKey = "ptest-child-a.heddle";
            const string parent = "BEFORE @partial(){{ptest-child-a}} AFTER\n";
            const string child = "[child says hi]";

            var disk = Stage(("ptest-child-a", child));
            try
            {
                var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                    new[] { (parentKey, parent), (childKey, child) }, parentKey, parent,
                    modelType: null, model: null, dynamicRootPath: disk, fileBacked: false);
                Assert.Equal(dyn, precompiled);
            }
            finally
            {
                TryDelete(disk);
            }
        }

        /// <summary>The name is computed by a body the hook evaluates itself, once, against <c>Scope.Null</c> —
        /// the same evaluation the dynamic engine does at its compile time. Nothing in generated code names the
        /// child.</summary>
        [Fact]
        public void AComputedNameIsEvaluatedByTheHookAndResolvesTheSameChild()
        {
            const string parentKey = "ptest-parent-c.heddle";
            const string childKey = "ptest-child-c.heddle";
            const string parent = "X@partial(){{ptest-@(\"child\")-c}}Y\n";
            const string child = "<computed>";

            var disk = Stage(("ptest-child-c", child));
            try
            {
                var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                    new[] { (parentKey, parent), (childKey, child) }, parentKey, parent,
                    modelType: null, model: null, dynamicRootPath: disk, fileBacked: false);
                Assert.Equal(dyn, precompiled);
                Assert.Contains("<computed>", precompiled);
            }
            finally
            {
                TryDelete(disk);
            }
        }

        /// <summary>Mixed mode: the child is opted out of precompilation, so the registry has no entry for it and
        /// the hook's own dynamic compile — under the <b>request's</b> options, which is the only place a root path
        /// exists — is what answers. The parent stays on the precompiled tier throughout.</summary>
        [Fact]
        public void APrecompiledParentRendersAChildTheRegistryDoesNotHold()
        {
            const string parentKey = "ptest-parent-b.heddle";
            const string childKey = "ptest-child-b.heddle";
            const string parent = "X@partial(){{ptest-child-b}}Y\n";
            const string child = "<dynamic-child>";

            var optOut = new Dictionary<string, Dictionary<string, string>>
            {
                [childKey] = new Dictionary<string, string>
                {
                    ["build_metadata.AdditionalFiles.Precompile"] = "false"
                }
            };

            var gen = DifferentialHarness.Generate(new[] { (parentKey, parent), (childKey, child) },
                perFileMetadata: optOut);
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, parentKey);
            DifferentialHarness.ExpectDegrade(gen, childKey);

            var disk = Stage(("ptest-child-b", child));
            try
            {
                var options = new TemplateOptions
                {
                    RootPath = disk + Path.DirectorySeparatorChar, FileNamePostfix = ".heddle"
                };
                var precompiled = DifferentialHarness.RenderGenerated(gen, parentKey, null, options);

                var dynamicTemplate = new HeddleTemplate(parent, new CompileContext(options));
                Assert.True(dynamicTemplate.CompileResult.Success, dynamicTemplate.CompileResult.ToString());
                Assert.Equal(dynamicTemplate.Generate(null), precompiled);
            }
            finally
            {
                TryDelete(disk);
            }
        }

        /// <summary>A child that does not compile. The dynamic tier refuses the parent's compile; the precompiled
        /// tier binds the child on first use and raises the same failed compile as a
        /// <see cref="TemplateCompileException"/> there — the shape every precompiled deferred compile fault
        /// takes, and the one the tier has always taken for this case.</summary>
        [Fact]
        public void AChildThatDoesNotCompileFaultsRatherThanRenderingSomethingElse()
        {
            const string parentKey = "ptest-parent-d.heddle";
            const string parent = "X@partial(){{ptest-child-d}}Y\n";
            const string brokenChild = "@list(){{ unterminated";

            var gen = DifferentialHarness.Generate(new[] { (parentKey, parent) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, parentKey);

            var disk = Stage(("ptest-child-d", brokenChild));
            try
            {
                var options = new TemplateOptions
                {
                    RootPath = disk + Path.DirectorySeparatorChar, FileNamePostfix = ".heddle"
                };
                Assert.Throws<TemplateCompileException>(
                    () => DifferentialHarness.RenderGenerated(gen, parentKey, null, options));

                var dynamicTemplate = new HeddleTemplate(parent, new CompileContext(options));
                Assert.False(dynamicTemplate.CompileResult.Success);
            }
            finally
            {
                TryDelete(disk);
            }
        }

        /// <summary>A chain of partials: every link is a precompiled template hosting the next, and every link's
        /// child comes out of the registry rather than off disk.</summary>
        [Fact]
        public void ANestedPartialChainResolvesEveryLink()
        {
            const string aKey = "ptest-chain-a.heddle";
            const string bKey = "ptest-chain-b.heddle";
            const string cKey = "ptest-chain-c.heddle";
            const string a = "[a @partial(){{ptest-chain-b}}]";
            const string b = "(b @partial(){{ptest-chain-c}})";
            const string c = "{c}";

            var disk = Stage(("ptest-chain-b", b), ("ptest-chain-c", c));
            try
            {
                var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                    new[] { (aKey, a), (bKey, b), (cKey, c) }, aKey, a,
                    modelType: null, model: null, dynamicRootPath: disk, fileBacked: false);
                Assert.Equal(dyn, precompiled);
                Assert.Equal("[a (b {c})]", precompiled);
            }
            finally
            {
                TryDelete(disk);
            }
        }

        private static long Measure(int iterations, System.Action action)
        {
            action();   // first pass binds the child; the steady state is what this measures
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
                action();
            return (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
        }

        /// <summary>
        /// The partial render path streams. The extension's own <c>RenderData</c> hands the child the caller's
        /// renderer, so a partial into a writer sink materialises nothing — where the generated code it replaces
        /// called <c>GenerateString</c> on the child and pushed the resulting string through the caller's
        /// renderer, which cost a whole second buffer plus the child's entire output as a string, on every render.
        /// <para>The child here renders 64 KiB, so a materialising path cannot come in under 128 KiB per render
        /// and a streaming one cannot come near it. The budget is three orders of magnitude below the former and
        /// well above the latter, so it measures the difference rather than the noise.</para>
        /// </summary>
        [Fact]
        public void APartialIntoAWriterSinkMaterialisesNothing()
        {
            const string parentKey = "ptest-alloc-parent.heddle";
            const string childKey = "ptest-alloc-child.heddle";
            const string parent = "[@partial(){{ptest-alloc-child}}]";
            var child = new string('x', 64 * 1024);

            var gen = DifferentialHarness.Generate(new[] { (parentKey, parent), (childKey, child) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, parentKey);
            DifferentialHarness.ExpectPrecompiled(gen, childKey);
            PrecompiledTemplates.Register(gen.Assembly);

            var entryType = gen.Assembly.GetType(
                DifferentialHarness.GeneratedNamespace + "." + Sanitize(parentKey));
            Assert.NotNull(entryType);
            var root = (IProcessStrategy) entryType
                .GetField("Root", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);

            var perRender = Measure(8,
                () => PrecompiledRuntime.GenerateToWriter(root, null, null, null, TextWriter.Null));

            Assert.True(perRender < 4096,
                "A partial rendered into a writer sink allocated " + perRender +
                " bytes per render; the child's own output is " + child.Length +
                " chars, so anything of that order means the child was materialised rather than streamed.");
        }
    }
}
