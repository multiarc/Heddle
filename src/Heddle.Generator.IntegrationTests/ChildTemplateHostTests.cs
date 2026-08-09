using System.IO;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The third-party half of the child-template role. <c>IncludeExtension</c> is a package author's own host —
    /// not derived from the engine's, not named after it — carrying nothing but <c>[ChildTemplateHost]</c>. It has
    /// to reach the same machinery the built-in reaches: precompile in a default build, run its own hook at static
    /// init, and take delivery of its child through the engine's child supply, registry first and the engine's own
    /// dynamic compile second.
    /// <para>Stage A could only assert the emitter's <b>route</b> for such an extension, because nothing served the
    /// route yet. These assert the rendered bytes.</para>
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class ChildTemplateHostTests
    {
        /// <summary>The resolver root does not exist, so the child cannot have been read off disk: a third-party
        /// host's child comes out of the registry exactly as the built-in's does.</summary>
        [Fact]
        public void AThirdPartyHostRendersItsPrecompiledChildFromTheRegistry()
        {
            const string parentKey = "ctest-parent-a.heddle";
            const string childKey = "ctest-child-a.heddle";
            const string parent = "in[@include(){{ctest-child-a}}]out\n";
            const string child = "<3p child>";

            var disk = PartialTests.Stage(("ctest-child-a", child));
            try
            {
                var (precompiled, dyn) = DifferentialHarness.RenderViaResolver(
                    new[] { (parentKey, parent), (childKey, child) }, parentKey, parent,
                    modelType: null, model: null, dynamicRootPath: disk, fileBacked: false);
                Assert.Equal(dyn, precompiled);
                Assert.Contains("<3p child>", precompiled);
            }
            finally
            {
                PartialTests.TryDelete(disk);
            }
        }

        /// <summary>The same host with a child the registry does not hold: the hook's own compile answers, under
        /// the request's options, and the parent keeps its tier.</summary>
        [Fact]
        public void AThirdPartyHostRendersAChildTheRegistryDoesNotHold()
        {
            const string parentKey = "ctest-parent-b.heddle";
            const string parent = "in[@include(){{ctest-child-b}}]out\n";
            const string child = "<3p disk child>";

            var gen = DifferentialHarness.Generate(new[] { (parentKey, parent) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            DifferentialHarness.ExpectPrecompiled(gen, parentKey);

            var disk = PartialTests.Stage(("ctest-child-b", child));
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
                Assert.Contains("<3p disk child>", precompiled);
            }
            finally
            {
                PartialTests.TryDelete(disk);
            }
        }
    }
}
