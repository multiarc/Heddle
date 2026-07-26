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
    }
}
