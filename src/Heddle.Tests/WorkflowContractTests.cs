using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The two release-pipeline invariants that CI itself cannot report: a publish step names packages that
    /// exist, and nothing publishes over a red Release suite. Both failed silently before — a publish step's
    /// relative <c>--packagePath</c> pointed at a directory no step creates, and the publishing jobs depended
    /// only on the Debug suites — because the publishing jobs are skipped on every pull request, so a green
    /// run says nothing about them.
    /// </summary>
    public class WorkflowContractTests
    {
        private const string ReleaseSuitesWorkflow = "./.github/workflows/tests-release.yml";

        /// <summary>A command that ships bytes to a registry. Any job running one is a publishing job.</summary>
        private static readonly string[] PublishCommands = { "dotnet nuget push", "npm publish", "vsce publish" };

        private static IEnumerable<string> WorkflowFiles =>
            Directory.EnumerateFiles(
                Path.GetDirectoryName(BuildSurfaceContractTests.FindRepoFile(
                    Path.Combine(".github", "workflows", "dotnet.yml"))),
                "*.yml");

        /// <summary>
        /// Packages are named from the workspace root, never relative to a step's working directory, and the
        /// directory named is one an artifact download wrote. <c>--packagePath ../vsix/*.vsix</c> from a step
        /// running in <c>editors/vscode</c> resolved to <c>editors/vsix</c>, so the Marketplace release could
        /// not find a single package it had just built.
        /// </summary>
        [Fact]
        public void PublishedPackagesAreNamedFromTheWorkspaceRoot()
        {
            var inspected = 0;
            foreach (var file in WorkflowFiles)
            {
                var text = File.ReadAllText(file).Replace("\r\n", "\n");
                foreach (Match m in Regex.Matches(text, @"--packagePath\s+(?<p>\S+)"))
                {
                    inspected++;
                    var path = m.Groups["p"].Value;
                    Assert.StartsWith("\"$GITHUB_WORKSPACE\"/", path, StringComparison.Ordinal);

                    // The first segment after the workspace root must be a directory some step downloads into,
                    // or the glob matches nothing and vsce is handed a literal.
                    var directory = path.Substring("\"$GITHUB_WORKSPACE\"/".Length).Split('/')[0];
                    Assert.Matches(@"(?m)^\s*path:\s*" + Regex.Escape(directory) + @"\s*$", text);
                }
            }

            Assert.True(inspected > 0, "No --packagePath found in any workflow; this gate now measures nothing.");
        }

        /// <summary>
        /// Every job that publishes waits — directly or through its dependency chain — on the job that runs the
        /// Release suites. testing-standards.md requires both configurations, and the Release leg lives in its
        /// own callable workflow precisely so a publishing job can depend on it.
        /// </summary>
        [Fact]
        public void EveryPublishingJobWaitsForTheReleaseSuites()
        {
            var publishers = new List<string>();
            foreach (var file in WorkflowFiles)
            {
                var jobs = Jobs(File.ReadAllText(file).Replace("\r\n", "\n"));
                var gates = jobs.Where(j => j.Value.Body.IndexOf(ReleaseSuitesWorkflow, StringComparison.Ordinal) >= 0)
                    .Select(j => j.Key)
                    .ToList();

                foreach (var job in jobs.Where(j => PublishCommands.Any(c =>
                    j.Value.Body.IndexOf(c, StringComparison.Ordinal) >= 0)))
                {
                    publishers.Add(Path.GetFileName(file) + ":" + job.Key);
                    Assert.True(ReachesAny(jobs, job.Key, gates),
                        Path.GetFileName(file) + ": job '" + job.Key + "' publishes but no job in its needs " +
                        "chain runs " + ReleaseSuitesWorkflow + ".");
                }
            }

            Assert.True(publishers.Count >= 3,
                "Expected the NuGet, npm and Marketplace publishing jobs; found: " + string.Join(", ", publishers));
        }

        private readonly struct Job
        {
            public Job(string body, IReadOnlyList<string> needs) { Body = body; Needs = needs; }
            public string Body { get; }
            public IReadOnlyList<string> Needs { get; }
        }

        /// <summary>Jobs and their declared needs. Workflow files here are hand-written and flat: a job is a
        /// two-space-indented key under <c>jobs:</c>, and its body runs to the next one.</summary>
        private static Dictionary<string, Job> Jobs(string text)
        {
            var result = new Dictionary<string, Job>(StringComparer.Ordinal);
            var jobs = Regex.Match(text, @"(?m)^jobs:[ \t]*$");
            if (!jobs.Success)
                return result;

            text = text.Substring(jobs.Index);
            var starts = Regex.Matches(text, @"(?m)^  (?<id>[A-Za-z0-9_-]+):[ \t]*$").Cast<Match>().ToList();
            for (var i = 0; i < starts.Count; i++)
            {
                var start = starts[i].Index;
                var end = i + 1 < starts.Count ? starts[i + 1].Index : text.Length;
                // Comment lines are dropped: a block runs to the next job header, so the comments that
                // introduce the NEXT job belong to this one, and one of them quotes a publish command.
                var body = Regex.Replace(text.Substring(start, end - start), @"(?m)^[ \t]*#.*$", string.Empty);
                result[starts[i].Groups["id"].Value] = new Job(body, Needs(body));
            }

            return result;
        }

        private static IReadOnlyList<string> Needs(string body)
        {
            var m = Regex.Match(body, @"(?m)^\s{4}needs:\s*(?<v>.+)$");
            if (!m.Success)
                return Array.Empty<string>();

            return m.Groups["v"].Value.Trim().Trim('[', ']')
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        private static bool ReachesAny(Dictionary<string, Job> jobs, string from, IReadOnlyCollection<string> targets)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id) || !jobs.TryGetValue(id, out var job))
                    continue;
                if (targets.Contains(id))
                    return true;
                foreach (var need in job.Needs)
                    queue.Enqueue(need);
            }

            return false;
        }
    }
}
