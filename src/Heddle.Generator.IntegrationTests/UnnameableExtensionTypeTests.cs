using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A bound host extension whose <b>type name</b> the consumer's assembly may not write. The engine discovers an
    /// extension by asking every type in an exporting assembly whether it implements the interface and carries the
    /// name attribute, then instantiates it with <c>Activator.CreateInstance</c> — neither step asks about
    /// accessibility or deprecation, so a non-public or error-obsolete extension registers and renders. Generated
    /// code spells that same type into a field declaration and a <c>new</c> expression in the consumer's own
    /// compilation, where CS0122/CS0619 stop a build over a <c>.g.cs</c> nobody can edit.
    /// <para>The neighbours are the point of the suite: every ordinary public extension, a deprecation the compiler
    /// merely comments on, and an extension whose <i>prop</i> type is unnameable all have to keep pre-compiling.
    /// Nothing on the parameter path spells a prop type, so refusing those would cost templates and prevent
    /// nothing.</para>
    /// </summary>
    public class UnnameableExtensionTypeTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        private static Cart Model() => new Cart { Name = "ab", Count = 4 };

        private static string Dynamic(string content, object model)
        {
            var template = new HeddleTemplate(content,
                new CompileContext(new TemplateOptions(), new ExType(typeof(Cart))));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(model);
        }

        private static DifferentialHarness.GenResult Generate(string key, string template,
            IReadOnlyList<MetadataReference> extra = null) =>
            DifferentialHarness.Generate(new[] { (key, template) }, globalOptions: null, extraReferences: extra);

        private static void ExpectNoErrorsAndOneWarningNaming(DifferentialHarness.GenResult gen, string key,
            string fragment)
        {
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var reported = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7030"));
            Assert.Equal(DiagnosticSeverity.Warning, reported.Severity);
            Assert.Contains(fragment, reported.GetMessage());
            Assert.Contains(key, reported.Location.GetLineSpan().Path);
            DifferentialHarness.ExpectDegrade(gen, key);
        }

        /// <summary>The plain custom-extension writer, which spells the type twice — once as the field's declared
        /// type and once in the <c>new</c> it initialises the field with.</summary>
        [Fact]
        public void AnInternalHostExtensionDegradesRatherThanEmittingANameTheConsumerCannotCompile()
        {
            const string key = "views/ext-internal.heddle";
            var template = "@model(){{" + CartType + "}}@\\\n@secret(Name)\n";

            ExpectNoErrorsAndOneWarningNaming(Generate(key, template), key, "SecretExtension");
            Assert.Equal("<ab>\n", Dynamic(template, Model()));
        }

        /// <summary>The parameter-declaring writer. A separate emission site with its own <c>new</c>, reached only
        /// by a call that supplies a parameter, so the row above cannot stand in for it.</summary>
        [Fact]
        public void AnInternalParameterDeclaringHostExtensionDegradesRatherThanEmittingAnUnwritableNew()
        {
            const string key = "views/ext-internal-props.heddle";
            var template = "@model(){{" + CartType + "}}@\\\n@boxed(Name, width: 7)\n";

            ExpectNoErrorsAndOneWarningNaming(Generate(key, template), key, "BoxedExtension");
            Assert.Equal("{7:ab}\n", Dynamic(template, Model()));
        }

        /// <summary>Deprecation the compiler treats as an error. Reflection ignores <c>[Obsolete]</c> outright, so
        /// the engine renders; the generated <c>new</c> is CS0619.</summary>
        [Fact]
        public void AnErrorObsoleteHostExtensionDegradesRatherThanEmittingANameTheConsumerCannotCompile()
        {
            const string key = "views/ext-obsolete.heddle";
            var template = "@model(){{" + CartType + "}}@\\\n@legacy(Name)\n";

            ExpectNoErrorsAndOneWarningNaming(Generate(key, template, ProbeReference()), key, "LegacyExtension");
            Assert.Equal("[ab]\n", Dynamic(template, Model()));
        }

        /// <summary>The near-neighbour inside the same probe assembly: a public extension discovered by the same
        /// parameterless <c>[ExportExtensions]</c>, in the same reference, still pre-compiles and renders the
        /// engine's bytes. Without it the rows above cannot tell a rule from a refusal of the whole assembly.
        /// </summary>
        [Fact]
        public void APublicHostExtensionInTheSameProbeAssemblyStillPrecompiles()
        {
            const string key = "views/ext-probe-public.heddle";
            var template = "@model(){{" + CartType + "}}@\\\n@plain(Name)\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart), Model(),
                extraReferences: ProbeReference());
            Assert.Equal("|ab|\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>An ordinary public host extension. The refusal must be keyed on the name being unwritable, not
        /// on the extension coming from a reference at all.</summary>
        [Fact]
        public void APublicHostExtensionStillPrecompiles()
        {
            const string key = "views/ext-public.heddle";
            var template = "@model(){{" + CartType + "}}@\\\n@yell(Name)\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart), Model());
            Assert.Equal("AB!\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A public extension carrying warning-level <c>[Obsolete]</c>. The consumer's compiler writes a
        /// CS0618 and emits the assembly, so the name is writable and the template pre-compiles.</summary>
        [Fact]
        public void AWarningLevelObsoleteHostExtensionStillPrecompiles()
        {
            const string key = "views/ext-deprecated.heddle";
            var template = "@model(){{" + CartType + "}}@\\\n@aging(Name)\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart), Model());
            Assert.Equal("(ab)\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>A public extension whose declared <c>[Prop]</c> type this assembly may not name. Nothing on the
        /// parameter path writes a prop type, so the extension-type rule must not reach it.</summary>
        [Fact]
        public void APublicExtensionWithAnUnnameablePropTypeStillPrecompiles()
        {
            const string key = "views/ext-unnameable-prop.heddle";
            var template = "@model(){{" + CartType + "}}@\\\n@badged(Name, size: 7)\n";

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart), Model());
            Assert.Equal("badge=none/size=7:ab\n", dyn);
            Assert.Equal(dyn, precompiled);
        }

        // ---- the probe assembly -------------------------------------------------------------------------------
        //
        // An error-obsolete type cannot be named in C# at all, so it cannot appear in this assembly's
        // [ExportExtensions] list — typeof() on it is the very CS0619 under test. It is reached the way a host
        // reaches one instead: a separate assembly carrying the parameterless [ExportExtensions], which exports
        // whatever it declares and needs no cooperation from the extension author.

        private const string ProbeSource = @"
using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

[assembly: ExportExtensions]

namespace Probe.Extensions
{
    [ExtensionName(""legacy"")]
    [Obsolete(""gone"", true)]
    public sealed class LegacyExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) =>
            ""["" + (scope.ModelData?.ToString() ?? string.Empty) + ""]"";

        public override void RenderData(in Scope scope) =>
            scope.Renderer.Render((string) ProcessData(scope));
    }

    [ExtensionName(""plain"")]
    public sealed class PlainExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) =>
            ""|"" + (scope.ModelData?.ToString() ?? string.Empty) + ""|"";

        public override void RenderData(in Scope scope) =>
            scope.Renderer.Render((string) ProcessData(scope));
    }
}";

        /// <summary>Built, loaded and registered exactly once per run: the engine's extension registry is
        /// process-global, so a second probe assembly declaring the same two names would be a second registration
        /// rather than a second fixture. The file is removed when the process ends — it is mapped for the whole
        /// run, so nothing earlier can delete it.</summary>
        private static readonly Lazy<string> Probe = new Lazy<string>(() =>
        {
            var name = "HeddleProbeExtensions" + Guid.NewGuid().ToString("N");
            var compilation = CSharpCompilation.Create(name,
                new[] { CSharpSyntaxTree.ParseText(ProbeSource) },
                DifferentialHarness.BaseReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var path = Path.Combine(Path.GetTempPath(), name + ".dll");
            var emit = compilation.Emit(path);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));

            HeddleTemplate.Register(Assembly.LoadFrom(path));
            AppDomain.CurrentDomain.ProcessExit += (_, __) =>
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            };
            return path;
        });

        private static IReadOnlyList<MetadataReference> ProbeReference() =>
            new[] { (MetadataReference) MetadataReference.CreateFromFile(Probe.Value) };
    }
}
