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

        [Theory]
        [MemberData(nameof(Rows))]
        public void CommittedExpectationEqualsTheLiveClrRelation(string source, string target, bool expected,
            string family)
        {
            var sourceType = Resolve(source);
            var targetType = Resolve(target);
            Assert.True(targetType.IsAssignableFrom(sourceType) == expected,
                $"{family}: {source} -> {target} — committed expectation {expected} disagrees with live reflection.");
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void ReflectionAdapterMatchesTheCorpus(string source, string target, bool expected, string family)
        {
            ITypeFacts<Type> facts = ReflectionTypeFacts.Instance;
            Assert.True(facts.IsAssignableFrom(Resolve(target), Resolve(source)) == expected,
                $"{family}: {source} -> {target}");
        }

        [Fact]
        public void CorpusCoversEveryDeclaredFamily()
        {
            var families = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in AssignabilityCorpus.Rows)
                families.Add(row.Family);

            // The seed families the corpus must always carry. Losing one silently is how a disagreement class
            // stops being tested.
            foreach (var required in new[]
                     {
                         "identity", "reference", "boxing", "nullable", "nullable-correction-A",
                         "nullable-correction-C", "numeric", "hierarchy", "variance", "valuetuple", "array"
                     })
                Assert.Contains(required, families);
        }
    }
}
