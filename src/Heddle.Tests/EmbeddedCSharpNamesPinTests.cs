using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Heddle.Language.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 1 WI8 (D9) — the embedded-C# identifier contract. The dynamic tier declares
    /// <c>model</c>/<c>chained</c>/<c>root</c> as the parameter list of the method it generates from two embedded
    /// <c>.tcs</c> resources; the emitter declares the model one as a local and refuses expressions naming the
    /// other two. Because the <c>.tcs</c> side is literal template text, the consts cannot flow into it — so this
    /// pin reads the resources and asserts the spelling instead. Renaming a <c>.tcs</c> parameter silently changes
    /// what a pasted C# expression means on the dynamic tier only; this test is the tripwire.
    /// <para>Verified by mutation during review: renaming <c>chained</c> in either resource reds this test naming
    /// the const that no longer matches.</para>
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

        /// <summary>Extracts the ordered parameter identifiers of the single <c>(@(…Type) name, …)</c> signature in
        /// the resource: every <c>@(SomethingType) identifier</c> pair, in source order.</summary>
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

        /// <summary>The consts themselves, pinned as literals: the test above compares the resources <em>to</em>
        /// the consts, so without this row a rename on both sides would pass silently.</summary>
        [Fact]
        public void TheSharedNamesAreTheDocumentedIdentifiers()
        {
            Assert.Equal("model", EmbeddedCSharpNames.Model);
            Assert.Equal("chained", EmbeddedCSharpNames.Chained);
            Assert.Equal("root", EmbeddedCSharpNames.Root);
        }
    }
}
