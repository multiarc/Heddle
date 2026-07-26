using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 2 WI3 — the pass-order lockstep. <c>DocumentShaping</c>'s header states the relative
    /// order of the shared passes as a normative contract; the two drivers are legitimately different programs, so
    /// nothing but a test keeps them sequencing it the same way. This reads both driver bodies and asserts the
    /// shared-pass call sequence in each equals the contract. A reordering on one side alone is a red build.
    /// </summary>
    public class DocumentShapingPassOrderLockstepTests
    {
        private static readonly string[] ExpectedOrder =
        {
            "ShiftBySkippedTokens", "TrimHiddenRemnantLines", "RemoveDefinitions", "ReplaceRawOutput",
            "StripBranchSets", "RemoveEmptyItem"
        };

        private static string RepoRoot([CallerFilePath] string thisFile = null)
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), ".."));

        private static string MethodBody(string path, string startMarker, string endMarker)
        {
            var text = File.ReadAllText(path);
            int start = text.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"'{startMarker}' not found in {path} — the lockstep anchor moved.");
            int end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.True(end > start, $"'{endMarker}' not found after the anchor in {path}.");
            return text.Substring(start, end - start);
        }

        private static IReadOnlyList<string> SharedPassCalls(string body)
        {
            var calls = new List<string>();
            foreach (Match match in Regex.Matches(body, @"DocumentShaping\.(\w+)\("))
            {
                var name = match.Groups[1].Value;
                if (Array.IndexOf(ExpectedOrder, name) >= 0)
                    calls.Add(name);
            }

            return calls;
        }

        [Fact]
        public void RuntimeDriverInvokesTheSharedPassesInContractOrder()
        {
            var root = RepoRoot();
            // CompileBody's own body, plus ProcessBranchSets (its step-5 wrapper, which hosts the strip call and
            // the runtime-only diagnostics observer).
            var compileBody = MethodBody(Path.Combine(root, "Heddle", "Runtime", "HeddleCompiler.cs"),
                "private static RuntimeDocument CompileBody(", "private enum OrphanState");
            var processBranchSets = MethodBody(Path.Combine(root, "Heddle", "Runtime", "HeddleCompiler.cs"),
                "private static void ProcessBranchSets(", "private sealed class BranchSetDiagnostics");

            var calls = new List<string>(SharedPassCalls(compileBody));
            // step 5 sits between ReplaceRawOutput and the chain-compile loop's RemoveEmptyItem
            calls.Insert(calls.IndexOf("RemoveEmptyItem"), SharedPassCalls(processBranchSets)[0]);

            Assert.Equal(ExpectedOrder, calls);
        }

        [Fact]
        public void GeneratorDriverInvokesTheSharedPassesInContractOrder()
        {
            var shape = MethodBody(
                Path.Combine(RepoRoot(), "Heddle.Generator", "Emit", "DocumentShaper.cs"),
                "public static Result Shape(", "/// <summary>Maps the generator's");

            Assert.Equal(ExpectedOrder, SharedPassCalls(shape));
        }

        /// <summary>
        /// <para>Pin 10's missing half (added by the phase-2 restoration audit, 2026-07-26). WI8's success criterion
        /// is that the empty-default-chain pin "turns red if <b>either</b> side reintroduces the skip" (D10/Q2.1),
        /// but the only pin that landed —
        /// <c>Heddle.Generator.Tests.DocumentShaperAdapterTests.EmptyDefaultChainIsModelledAsAZeroLengthElementAtDocumentEnd</c>
        /// — drives the generator's shaper only, so a runtime-side regression was invisible to it.</para>
        /// <para>The runtime half is not reachable as a machine-level vector: <c>CompileBody</c>'s default-chain
        /// element is minted inside the item-compile loop, gated on the chain's own compiled
        /// <c>returnTypeChainedPrevious</c>. So it is pinned the way this file already pins the pass order — over
        /// the driver bodies: both must construct the zero-length element at document end, and neither may carry the
        /// count-based skip the alignment removed.</para>
        /// </summary>
        [Fact]
        public void NeitherDriverSkipsAnEmptyDefaultChain()
        {
            var root = RepoRoot();
            var compileBody = MethodBody(Path.Combine(root, "Heddle", "Runtime", "HeddleCompiler.cs"),
                "foreach (var extensions in parseContext.DefaultChains)", "return new RuntimeDocument(");
            var shape = MethodBody(Path.Combine(root, "Heddle.Generator", "Emit", "DocumentShaper.cs"),
                "foreach (var chain in parseContext.DefaultChains)", "return new Result(");

            foreach (var pair in new[] { ("runtime CompileBody", compileBody), ("generator Shape", shape) })
            {
                // The zero-length element at document end — the shape both tiers model (bytes unaffected; the
                // element renders nothing, but it defeats RuntimeDocument's single-element fast path identically).
                Assert.Contains("BlockPosition(", pair.Item2);
                Assert.Contains(", 0)", pair.Item2);
                Assert.Matches(@"new BlockPosition\(\s*\w+(\.\w+)*(\.Length)?,\s*0\s*\)", pair.Item2);

                // ...and no reintroduced skip. `Chain == null || Chain.Count == 0` on the default-chain loop is
                // exactly the divergence Q2.1 ruled out; either side growing it back is red here.
                Assert.DoesNotMatch(@"Chain\s*==\s*null", pair.Item2);
                Assert.DoesNotMatch(@"Chain\.Count\s*==\s*0", pair.Item2);
                Assert.DoesNotMatch(@"Count\s*==\s*0", pair.Item2);
            }
        }
    }
}
