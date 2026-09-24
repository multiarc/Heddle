using System.Linq;
using Heddle.Data;
using Heddle.LanguageServices;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Workspace configuration reading: how <c>.heddle-lsp.json</c> values reach the analyzer via shared
    /// <c>OutputProfileRules</c>/<c>HeddleBuildOptions</c> code. On bad values: keep the default, log a message,
    /// never throw, and keep analyzing.
    /// </summary>
    public class WorkspaceConfigTests
    {
        [Theory]
        [InlineData("text", OutputProfile.Text)]
        [InlineData("TEXT", OutputProfile.Text)]
        [InlineData("  html  ", OutputProfile.Html)]
        [InlineData("Html", OutputProfile.Html)]
        public void ProfileTokensParseThroughTheSharedRules(string token, OutputProfile expected)
        {
            var options = WorkspaceConfig.Read("/ws", "{\"outputProfile\":\"" + token + "\"}");
            Assert.Equal(expected, options.OutputProfile);
            Assert.Empty(options.ConfigurationMessages);
        }

        [Theory]
        [InlineData("memberPathsOnly", ExpressionMode.MemberPathsOnly)]
        [InlineData("FullCSharp", ExpressionMode.FullCSharp)]
        [InlineData(" native ", ExpressionMode.Native)]
        public void ModeTokensParseThroughTheSharedRules(string token, ExpressionMode expected)
        {
            var options = WorkspaceConfig.Read("/ws", "{\"expressionMode\":\"" + token + "\"}");
            Assert.Equal(expected, options.ExpressionMode);
            Assert.Empty(options.ConfigurationMessages);
        }

        [Fact]
        public void AnUnknownProfileKeepsTheDefaultAndNamesTheAcceptedTokens()
        {
            var options = WorkspaceConfig.Read("/ws", "{\"outputProfile\":\"xml\"}");

            Assert.Equal(OutputProfile.Html, options.OutputProfile);
            var message = Assert.Single(options.ConfigurationMessages);
            Assert.Contains("outputProfile", message);
            Assert.Contains("xml", message);
            Assert.Contains(OutputProfileRules.ValidProfileValues, message);
        }

        [Fact]
        public void AnUnknownModeKeepsTheDefaultAndNamesTheAcceptedTokens()
        {
            var options = WorkspaceConfig.Read("/ws", "{\"expressionMode\":\"lisp\"}");

            Assert.Equal(ExpressionMode.Native, options.ExpressionMode);
            var message = Assert.Single(options.ConfigurationMessages);
            Assert.Contains("expressionMode", message);
            Assert.Contains(nameof(ExpressionMode.MemberPathsOnly), message);
        }

        [Fact]
        public void TheParityKeysAreRead()
        {
            var options = WorkspaceConfig.Read("/ws",
                "{\"trimDirectiveLines\":false,\"maxRecursionCount\":3}");

            Assert.False(options.TrimDirectiveLines);
            Assert.Equal(3, options.MaxRecursionCount);
            Assert.Empty(options.ConfigurationMessages);
        }

        /// <summary>String spellings go through the same helpers the MSBuild properties use, so an editor accepts
        /// exactly what a build property accepts.</summary>
        [Fact]
        public void TheParityKeysAlsoAcceptTheirBuildPropertySpellings()
        {
            var options = WorkspaceConfig.Read("/ws",
                "{\"trimDirectiveLines\":\"false\",\"maxRecursionCount\":\"7\"}");

            Assert.False(options.TrimDirectiveLines);
            Assert.Equal(7, options.MaxRecursionCount);
            Assert.Empty(options.ConfigurationMessages);
        }

        [Theory]
        [InlineData("{\"maxRecursionCount\":0}")]
        [InlineData("{\"maxRecursionCount\":-4}")]
        [InlineData("{\"maxRecursionCount\":\"lots\"}")]
        [InlineData("{\"maxRecursionCount\":true}")]
        public void ABadRecursionBoundKeepsTheDefaultAndLogs(string json)
        {
            var options = WorkspaceConfig.Read("/ws", json);

            Assert.Equal(new TemplateOptions().MaxRecursionCount, options.MaxRecursionCount);
            Assert.Contains("maxRecursionCount", Assert.Single(options.ConfigurationMessages));
        }

        [Fact]
        public void AValueOfTheWrongJsonKindIsIgnoredWithALogLine()
        {
            var options = WorkspaceConfig.Read("/ws", "{\"outputProfile\":42,\"assemblies\":\"a.dll\"}");

            Assert.Equal(OutputProfile.Html, options.OutputProfile);
            Assert.Empty(options.AssemblyPaths);
            Assert.Equal(2, options.ConfigurationMessages.Count);
            Assert.Contains(options.ConfigurationMessages, m => m.Contains("outputProfile"));
            Assert.Contains(options.ConfigurationMessages, m => m.Contains("assemblies"));
        }

        /// <summary>Whatever the config says, reading it never throws — the editor keeps working on a broken workspace file.</summary>
        [Fact]
        public void EveryComplaintIsALogLineRatherThanAFailure()
        {
            var options = WorkspaceConfig.Read("/ws",
                "{\"outputProfile\":\"nope\",\"expressionMode\":\"nope\",\"trimDirectiveLines\":\"nope\"," +
                "\"maxRecursionCount\":\"nope\",\"rootPath\":5}");

            Assert.Equal(5, options.ConfigurationMessages.Count);
            Assert.All(options.ConfigurationMessages, m => Assert.Contains(WorkspaceConfig.FileName, m));
            Assert.Equal(OutputProfile.Html, options.OutputProfile);
            Assert.Equal("/ws", options.RootPath);
        }

        [Fact]
        public void RelativePathsStillResolveAgainstTheWorkspaceRoot()
        {
            var options = WorkspaceConfig.Read("/ws", "{\"rootPath\":\"templates\",\"assemblies\":[\"bin/m.dll\"]}");

            Assert.Equal(System.IO.Path.Combine("/ws", "templates"), options.RootPath);
            Assert.Equal(System.IO.Path.Combine("/ws", "bin/m.dll"), options.AssemblyPaths.Single());
            Assert.Empty(options.ConfigurationMessages);
        }
    }
}
