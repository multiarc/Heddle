using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Pins that the runtime driver invokes the shared DocumentShaping passes in contract order.
    /// The generator-driver half went with the 2.x generator.</summary>
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
                "private static RuntimeDocument CompileBody(", "private static HeddleCompileError CompileItemFault(");
            var processBranchSets = MethodBody(Path.Combine(root, "Heddle", "Runtime", "HeddleCompiler.cs"),
                "private static void ProcessBranchSets(", "private static bool HasScopeChannel(");

            var calls = new List<string>(SharedPassCalls(compileBody));
            calls.Insert(calls.IndexOf("RemoveEmptyItem"), SharedPassCalls(processBranchSets)[0]);

            Assert.Equal(ExpectedOrder, calls);
        }

        /// <summary>The runtime driver must construct a zero-length element at document end for empty
        /// default chains and may not reintroduce the count-based skip removed during alignment.</summary>
        [Fact]
        public void DriverDoesNotSkipAnEmptyDefaultChain()
        {
            var root = RepoRoot();
            var compileBody = MethodBody(Path.Combine(root, "Heddle", "Runtime", "HeddleCompiler.cs"),
                "foreach (var extensions in parseContext.DefaultChains)",
                "var record = compileScope.CompileContext.FormRecord;");

            // Zero-length element at document end.
            Assert.Contains("BlockPosition(", compileBody);
            Assert.Contains(", 0)", compileBody);
            Assert.Matches(@"new BlockPosition\(\s*\w+(\.\w+)*(\.Length)?,\s*0\s*\)", compileBody);

            // No reintroduced count-based skip on default-chain loop.
            Assert.DoesNotMatch(@"Chain\s*==\s*null", compileBody);
            Assert.DoesNotMatch(@"Chain\.Count\s*==\s*0", compileBody);
            Assert.DoesNotMatch(@"Count\s*==\s*0", compileBody);
        }
    }
}
