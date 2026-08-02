using System;
using System.Collections.Generic;
using Heddle.Helpers;
using Heddle.Language.Binding;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The <b>reflection-side</b> driver of the shared assignability conformance corpus. It proves
    /// two things: that every committed expectation equals the live CLR relation (so the corpus is generated data,
    /// not belief), and that the reflection <see cref="ITypeFacts{TType}"/> adapter answers it row for row.
    /// <para>The symbol-side driver in <c>Heddle.Generator.Tests</c> reads the same file. Corrupting one row turns
    /// both red — that is the point.</para>
    /// </summary>
    public class AssignabilityCorpusReflectionTests
    {
        public static IEnumerable<object[]> Rows()
        {
            foreach (var row in AssignabilityCorpus.Rows)
                yield return new object[] { row.Source, row.Target, row.Expected, row.Family };
        }

        private static Type Resolve(string spelling) =>
            ReflectionHelper.ResolveType(spelling, Array.Empty<string>());

        /// <summary>
        /// The expectation for the CLR actually running this test.
        ///
        /// <para>The shared corpus commits the relation as CoreCLR and Roslyn's symbol model both
        /// answer it, because that is the pair the generator and the modern runtime must agree on.
        /// The .NET Framework CLR answers one row differently: on x64 it permits
        /// <c>IntPtr[]</c> -&gt; <c>Int64[]</c> array covariance, reducing <c>IntPtr</c> to its
        /// 64-bit underlying primitive, where CoreCLR refuses and the symbol tier refuses. Skipping
        /// the row on netfx would drop a real assertion; flipping the committed value would break
        /// both the CoreCLR driver and the generator's symbol driver, which read the same file. So
        /// the divergence is recorded here and the netfx run asserts the behaviour netfx actually
        /// has. A change on either side turns a run red, which is the point of the corpus.</para>
        /// </summary>
        private static bool ExpectedOnThisRuntime(string source, string target, bool committed)
        {
#if NETFRAMEWORK
            if (source == "System.IntPtr[]" && target == "System.Int64[]") return true;
#endif
            return committed;
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void CommittedExpectationEqualsTheLiveClrRelation(string source, string target, bool expected,
            string family)
        {
            var sourceType = Resolve(source);
            var targetType = Resolve(target);
            var here = ExpectedOnThisRuntime(source, target, expected);
            Assert.True(targetType.IsAssignableFrom(sourceType) == here,
                $"{family}: {source} -> {target} — expectation {here} disagrees with live reflection.");
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void ReflectionAdapterMatchesTheCorpus(string source, string target, bool expected, string family)
        {
            ITypeFacts<Type> facts = ReflectionTypeFacts.Instance;
            Assert.True(facts.IsAssignableFrom(Resolve(target), Resolve(source))
                        == ExpectedOnThisRuntime(source, target, expected),
                $"{family}: {source} -> {target}");
        }

        [Fact]
        public void CorpusCoversEveryDeclaredFamily()
        {
            var families = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in AssignabilityCorpus.Rows)
                families.Add(row.Family);

            // Losing a seed family silently breaks test coverage.
            foreach (var required in new[]
                     {
                         "identity", "reference", "boxing", "nullable", "nullable-correction-A",
                         "nullable-correction-C", "numeric", "hierarchy", "variance", "valuetuple", "array",
                         "array-covariance", "array-interface-covariance", "array-rank"
                     })
                Assert.Contains(required, families);
        }
    }
}
