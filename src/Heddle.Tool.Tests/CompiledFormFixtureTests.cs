using System;
using System.IO;
using System.Linq;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Xunit;

namespace Heddle.Tool.Tests
{
    /// <summary>
    /// The stored firewall: <c>TestData/compiled-form-v4.bin</c> is real <c>heddle compile</c> output for a
    /// fixture template exercising all four gauntlet-relevant sections — a template row, an extension row
    /// (<c>@if</c>), a function row (<c>upper</c>), and member rows (the model reads). The build that produced
    /// it: template text <c>Hello @(upper(TemplateName))!</c> + <c>@if(TrimDirectiveLines){{Shown:
    /// @(MaxRecursionCount).}}</c>, model <c>Heddle.Data.TemplateOptions, Heddle</c>, profile Text, mode
    /// Native. A reader change that cannot read this file back is a red build, not a silent fallback —
    /// no live template is needed to prove it.
    /// </summary>
    public class CompiledFormFixtureTests
    {
        private static byte[] FixtureBytes()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "TestData", "compiled-form-v4.bin");
            Assert.True(File.Exists(path), "Missing TestData/compiled-form-v4.bin beside the test assembly.");
            return File.ReadAllBytes(path);
        }

        [Fact]
        public void FixtureIsTheCompiledFormSchema()
        {
            // The point window: exactly one readable shape. The reader itself rejects anything else,
            // so reaching the assertions below IS the version proof; the literal pins the point.
            Assert.Equal(4, PrecompiledSchema.CompiledFormSchemaVersion);
            var artifact = CompiledFormReader.Read(FixtureBytes());
            Assert.NotNull(artifact);
        }

        [Fact]
        public void FixtureCarriesAllFourSections()
        {
            var artifact = CompiledFormReader.Read(FixtureBytes());

            var template = Assert.Single(artifact.Templates);
            Assert.Equal("fixture.heddle", template.Key);
            Assert.Equal("Text", template.Options.Profile);
            Assert.Equal("Native", template.Options.Mode);
            Assert.True(template.Options.Trim);

            Assert.Contains(artifact.Extensions, e => e.RegistryName == "if");

            var function = Assert.Single(artifact.Functions);
            Assert.Equal("upper", function.Name);

            Assert.NotEmpty(artifact.Members);
            var segments = artifact.Members.SelectMany(m => m.Segments).ToArray();
            Assert.Contains("TrimDirectiveLines", segments);
            Assert.Contains("MaxRecursionCount", segments);
        }

        [Fact]
        public void FixtureRoundTripsByteIdentical()
        {
            var bytes = FixtureBytes();
            var reencoded = CompiledFormWriter.Write(CompiledFormReader.Read(bytes));
            Assert.Equal(bytes, reencoded);

            // The writer stamps the digest after encoding and the reader verifies it: a corrupt byte
            // anywhere must fail the read rather than parse.
            var tampered = (byte[])bytes.Clone();
            tampered[tampered.Length - 1] ^= 0xFF;
            Assert.ThrowsAny<Exception>(() => CompiledFormReader.Read(tampered));
        }
    }
}
