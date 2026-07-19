using System.IO;
using Heddle.Tool;
using Xunit;

namespace Heddle.Tool.Tests
{
    /// <summary>
    /// WI12 — the <c>heddle</c> CLI T4-successor scenario: render a C# enum from JSON data through the full dynamic
    /// engine, plus the argument-handling surface. The codegen sample proves a build <c>Exec</c> step can turn data
    /// into source with no runtime Heddle dependency in the produced artifact.
    /// </summary>
    public class HeddleToolTests
    {
        private const string EnumTemplate =
            "@model(){{dynamic}}@\\\n" +
            "public enum @(Name)\n" +
            "{\n" +
            "@list(Values){{    @(this),\n}}}\n";

        private const string EnumModel =
            "{ \"Name\": \"Color\", \"Values\": [ \"Red\", \"Green\", \"Blue\" ] }";

        [Fact]
        public void RendersCSharpEnumFromJsonModel()
        {
            var output = HeddleRenderer.Render(EnumTemplate, EnumModel);
            var expected =
                "public enum Color\n" +
                "{\n" +
                "    Red,\n" +
                "    Green,\n" +
                "    Blue,\n" +
                "}\n";
            Assert.Equal(expected, output);
        }

        [Fact]
        public void ModelLessTemplateRendersWithoutJson()
        {
            var output = HeddleRenderer.Render("static text only\n", null);
            Assert.Equal("static text only\n", output);
        }

        [Fact]
        public void JsonScalarsMapToClrPrimitives()
        {
            var model = HeddleRenderer.ParseModel("{ \"N\": 7, \"F\": 1.5, \"B\": true, \"S\": \"x\" }");
            dynamic d = model;
            Assert.Equal(7L, d.N);
            Assert.Equal(1.5, d.F);
            Assert.True(d.B);
            Assert.Equal("x", d.S);
        }

        [Fact]
        public void CliRenderCommandWritesToOutFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), "heddle-tool-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var templatePath = Path.Combine(dir, "enum.heddle");
                var modelPath = Path.Combine(dir, "model.json");
                var outPath = Path.Combine(dir, "Color.cs");
                File.WriteAllText(templatePath, EnumTemplate);
                File.WriteAllText(modelPath, EnumModel);

                var stdout = new StringWriter();
                var stderr = new StringWriter();
                var code = Program.Run(
                    new[] { "render", templatePath, "--model-json", modelPath, "--out", outPath },
                    stdout, stderr);

                Assert.Equal(0, code);
                Assert.True(File.Exists(outPath), stderr.ToString());
                Assert.Contains("public enum Color", File.ReadAllText(outPath));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void CliRenderToStdout()
        {
            var dir = Path.Combine(Path.GetTempPath(), "heddle-tool-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var templatePath = Path.Combine(dir, "t.heddle");
                File.WriteAllText(templatePath, "hello world\n");
                var stdout = new StringWriter();
                var stderr = new StringWriter();
                var code = Program.Run(new[] { "render", templatePath }, stdout, stderr);
                Assert.Equal(0, code);
                Assert.Equal("hello world\n", stdout.ToString());
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void HelpAndUnknownCommandReturnExpectedCodes()
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            Assert.Equal(0, Program.Run(new[] { "--help" }, stdout, stderr));
            Assert.Contains("heddle render", stdout.ToString());

            var s2 = new StringWriter();
            Assert.Equal(2, Program.Run(new[] { "frobnicate" }, new StringWriter(), s2));
            Assert.Contains("Unknown command", s2.ToString());

            var s3 = new StringWriter();
            Assert.Equal(2, Program.Run(new[] { "render" }, new StringWriter(), s3));
            Assert.Contains("template path is required", s3.ToString());
        }
    }
}
