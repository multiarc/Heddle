using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Pins that both drivers invoke the shared DocumentShaping passes in contract order.</summary>
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
            var compileBody = MethodBody(Path.Combine(root, "Heddle", "Runtime", "HeddleCompiler.cs"),
                "private static RuntimeDocument CompileBody(", "private enum OrphanState");
            var processBranchSets = MethodBody(Path.Combine(root, "Heddle", "Runtime", "HeddleCompiler.cs"),
                "private static void ProcessBranchSets(", "private sealed class BranchSetDiagnostics");

            var calls = new List<string>(SharedPassCalls(compileBody));
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

        /// <summary>Both drivers must construct a zero-length element at document end for empty default chains
        /// and neither may reintroduce the count-based skip removed during alignment.</summary>
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
                // Zero-length element at document end for both tiers.
                Assert.Contains("BlockPosition(", pair.Item2);
                Assert.Contains(", 0)", pair.Item2);
                Assert.Matches(@"new BlockPosition\(\s*\w+(\.\w+)*(\.Length)?,\s*0\s*\)", pair.Item2);

                // No reintroduced count-based skip on default-chain loop.
                Assert.DoesNotMatch(@"Chain\s*==\s*null", pair.Item2);
                Assert.DoesNotMatch(@"Chain\.Count\s*==\s*0", pair.Item2);
                Assert.DoesNotMatch(@"Count\s*==\s*0", pair.Item2);
            }
        }
    }
}
