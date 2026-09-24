using System;
using System.IO;
using System.Reflection;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Build.Tests
{
    /// <summary>P2-R11: the parity harness's pass over a host-built artifact. A fixture project is built
    /// by real MSBuild through the in-repo task and tool; its assembly is loaded and registered like a
    /// deployed host's, its generated typed entry point renders, and the bytes equal the text compile of
    /// the same template under the options the artifact baked.</summary>
    public class HostBuiltArtifactTests
    {
        private const string Template = "Hello, world! @(1 + 2) and @(\"x\" + \"y\")\n";

        [Fact]
        public void HostBuiltAssemblyRegistersAndRendersByteIdenticallyToTheTextCompile()
        {
            using (var fixture = new MsBuildFixture())
            {
                fixture.Write("templates/hello.heddle", Template);
                string project = fixture.Write("host.csproj",
                    MsBuildFixture.ProjectXml("net10.0", string.Empty,
                        "<HeddleTemplate Include=\"templates/hello.heddle\" />\n"));
                fixture.Build(project).AssertSuccess("host build");
                var assembly = Assembly.LoadFrom(MsBuildFixture.BuiltAssembly(fixture.Root, "host"));

                var saved = PrecompiledTemplates.DefaultOptions;
                try
                {
                    PrecompiledTemplates.Register(assembly);
                    PrecompiledTemplateInfo entry = null;
                    foreach (var candidate in PrecompiledTemplates.Entries)
                        if (string.Equals(candidate.Key, "templates/hello.heddle", StringComparison.Ordinal))
                            entry = candidate;
                    Assert.True(entry != null, "the host-built artifact carries no row for templates/hello.heddle.");
                    var options = new TemplateOptions("host")
                    {
                        OutputProfile = entry.OptionsFingerprint.Profile,
                        ExpressionMode = entry.OptionsFingerprint.ExpressionMode,
                        TrimDirectiveLines = entry.OptionsFingerprint.TrimDirectiveLines,
                        EnableFileChangeCheck = false,
                        PrecompiledMismatchPolicy = PrecompiledMismatchPolicy.Strict
                    };
                    PrecompiledTemplates.DefaultOptions = options;

                    // The generated typed entry point, found the way a host names it: one static
                    // Generate(object) on a class the build emitted under Heddle.Generated.
                    MethodInfo generate = null;
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Namespace != "Heddle.Generated" || !type.Name.StartsWith("Templates_", StringComparison.Ordinal))
                            continue;
                        generate = type.GetMethod("Generate", new[] { typeof(object), typeof(object), typeof(object) })
                            ?? type.GetMethod("Generate", new[] { typeof(object) });
                        if (generate != null)
                            break;
                    }
                    Assert.True(generate != null, "no generated typed entry point found in the host assembly.");
                    var arguments = new object[generate.GetParameters().Length];
                    string precompiled = (string)generate.Invoke(null, arguments);

                    var text = new HeddleTemplate(Template, new CompileContext(new TemplateOptions("text")
                    {
                        OutputProfile = options.OutputProfile,
                        ExpressionMode = options.ExpressionMode,
                        TrimDirectiveLines = options.TrimDirectiveLines
                    }));
                    Assert.True(text.CompileResult.Success, text.CompileResult.ToString());
                    string dynamic = text.Generate(null);
                    Assert.Equal(dynamic, precompiled);
                }
                finally
                {
                    PrecompiledTemplates.DefaultOptions = saved;
                }
            }
        }
    }
}
