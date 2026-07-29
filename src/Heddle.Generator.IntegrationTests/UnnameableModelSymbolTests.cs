// The deprecated fixture is named directly, which is a warning by design; the error-obsolete ones cannot be named
// in C# at all — that is the whole point of them — so they are reached through reflection, exactly as the engine
// reaches them.
#pragma warning disable 618

using System;
using System.Linq;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Data;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Model symbols the engine reads and the consumer's compiler refuses, other than by accessibility. Reflection
    /// ignores <c>[Obsolete]</c> and never declares a parameter of the model's type, so the engine renders all of
    /// these; generated code spells the name into a cast, a parameter and a field, and the consumer's build dies on
    /// an error against a <c>.g.cs</c> they did not write — no Heddle id, no <c>.heddle</c> position.
    /// </summary>
    public class UnnameableModelSymbolTests
    {
        private const string ObsoleteType = "Heddle.Generator.IntegrationTests.Fixtures.ObsoleteErrorModel";
        private const string ObsoleteMember = "Heddle.Generator.IntegrationTests.Fixtures.ObsoleteMemberModel";
        private const string Deprecated = "Heddle.Generator.IntegrationTests.Fixtures.DeprecatedModel";
        private const string StaticType = "Heddle.Generator.IntegrationTests.Fixtures.StaticModel";

        /// <summary>The fixture type by name. <c>typeof</c> is not available for an error-obsolete type: the C#
        /// compiler refuses to let this assembly name it, which is the property under test.</summary>
        private static Type Fixture(string fullName) =>
            typeof(UnnameableModelSymbolTests).Assembly.GetType(fullName, throwOnError: true);

        private static string Dynamic(string content, Type modelType, object model)
        {
            var template = new HeddleTemplate(content, new CompileContext(new TemplateOptions(), modelType));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(model);
        }

        /// <summary>The model type itself. Every mention of the name in the generated file — the entry point's
        /// parameter, the strategy's cast, the body's local — is a CS0619, so the template took the consumer's build
        /// down over a model the engine renders without comment.</summary>
        [Fact]
        public void AnErrorObsoleteModelTypeDegradesRatherThanEmittingANameTheConsumerCannotCompile()
        {
            const string key = "views/obsolete-type.heddle";
            var template = "@model(){{" + ObsoleteType + "}}@\\\n@(Title)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var hed7030 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7030"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7030.Severity);
            Assert.Contains("ObsoleteErrorModel", hed7030.GetMessage());
            Assert.Contains(key, hed7030.Location.GetLineSpan().Path);
            DifferentialHarness.ExpectDegrade(gen, key);

            var modelType = Fixture(ObsoleteType);
            Assert.Equal("obsolete\n", Dynamic(template, modelType, Activator.CreateInstance(modelType)));
        }

        /// <summary>One member of a perfectly nameable type. The member tier accepts it — the engine's visibility
        /// policy has nothing to say about deprecation — so only a rule at the member gate keeps the read out of the
        /// generated body.</summary>
        [Fact]
        public void AnErrorObsoleteMemberDegradesRatherThanEmittingAReadTheConsumerCannotCompile()
        {
            const string key = "views/obsolete-member.heddle";
            var template = "@model(){{" + ObsoleteMember + "}}@\\\n@(Bad)\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            var hed7030 = Assert.Single(gen.Diagnostics.Where(d => d.Id == "HED7030"));
            Assert.Equal(DiagnosticSeverity.Warning, hed7030.Severity);
            Assert.Contains("ObsoleteMemberModel.Bad", hed7030.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, key);

            Assert.Equal("bad\n", Dynamic(template, typeof(ObsoleteMemberModel), new ObsoleteMemberModel()));
        }

        /// <summary>
        /// The cost control. A warning-level <c>[Obsolete]</c> is the ordinary state of a large model library mid-
        /// migration; degrading on it would take those templates off the precompiled tier silently and for nothing,
        /// since the generated file opens with a blanket <c>#pragma warning disable</c> and the consumer never sees
        /// a CS0618 from it.
        /// </summary>
        [Theory]
        [InlineData("Title", "deprecated\n")]
        [InlineData("Legacy", "legacy\n")]
        public void AWarningLevelObsoleteModelStillPrecompiles(string member, string expected)
        {
            var key = "views/deprecated-" + member.ToLowerInvariant() + ".heddle";
            var template = "@model(){{" + Deprecated + "}}@\\\n@(" + member + ")\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.DoesNotContain(gen.Diagnostics, d => d.Id == "HED7030");

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(DeprecatedModel),
                new DeprecatedModel());
            Assert.Equal(dyn, precompiled);
            Assert.Equal(expected, dyn);
        }

        /// <summary>
        /// A static class as the model. No member read is needed to break it: the generated entry point takes the
        /// model as a parameter of its declared type, and a static type may not be one (CS0721), nor the target of
        /// the cast the strategy makes (CS0716). The engine declares no such parameter and renders the template.
        /// </summary>
        [Fact]
        public void AStaticModelTypeDegradesRatherThanEmittingAParameterTheConsumerCannotCompile()
        {
            const string key = "views/static-model.heddle";
            var template = "@model(){{" + StaticType + "}}@\\\nhello\n";
            var gen = DifferentialHarness.Generate(new[] { (key, template) });

            DifferentialHarness.ExpectDegrade(gen, key);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

            Assert.Equal("hello\n", Dynamic(template, typeof(StaticModel), null));
        }
    }
}
