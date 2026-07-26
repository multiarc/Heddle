using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Heddle.Language.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The embedded-C# identifiers (<c>model</c>/<c>chained</c>/<c>root</c>) must match across resources and
    /// consts; renaming silently breaks expression semantics on the dynamic tier.
    /// </summary>
    public class EmbeddedCSharpNamesPinTests
    {
        private static string ReadResource(string suffix)
        {
            var assembly = typeof(HeddleTemplate).Assembly;
            var name = assembly.GetManifestResourceNames()
                .Single(n => n.EndsWith(suffix, StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        private static string[] SignatureParameterNames(string template)
        {
            var matches = Regex.Matches(template, @"@\((?<type>\w*Type)\)\s+(?<name>\w+)");
            return matches.Cast<Match>().Select(m => m.Groups["name"].Value).ToArray();
        }

        [Theory]
        [InlineData("CSharpClassTemplate.tcs")]
        [InlineData("CSharpPreparseTemplate.tcs")]
        public void EmbeddedTemplatesDeclareExactlyTheSharedNamesInOrder(string resourceSuffix)
        {
            var template = ReadResource(resourceSuffix);
            var parameters = SignatureParameterNames(template);

            Assert.Equal(
                new[] { EmbeddedCSharpNames.Model, EmbeddedCSharpNames.Chained, EmbeddedCSharpNames.Root },
                parameters);
        }

        /// <summary>Without this literal pin, a rename on both resources and consts would pass silently.</summary>
        [Fact]
        public void TheSharedNamesAreTheDocumentedIdentifiers()
        {
            Assert.Equal("model", EmbeddedCSharpNames.Model);
            Assert.Equal("chained", EmbeddedCSharpNames.Chained);
            Assert.Equal("root", EmbeddedCSharpNames.Root);
        }
    }
}
