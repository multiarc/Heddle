using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The release-pipeline invariants that CI itself cannot report: a publish step names packages that
    /// exist, nothing publishes over a red Release suite, and a step that calls <c>gh</c> can say which
    /// repository it means. Each failed silently before — a publish step's relative <c>--packagePath</c>
    /// pointed at a directory no step creates, the publishing jobs depended only on the Debug suites, and
    /// the release job calls <c>gh</c> without checking out — because the publishing jobs are skipped on
    /// every pull request, so a green run says nothing about them.
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

        /// <summary>
        /// A job that runs <c>gh</c> can name the repository it acts on. <c>gh</c> resolves it from
        /// <c>--repo</c>, then <c>GH_REPO</c>, then the git remotes of the working directory — it does not
        /// read <c>GITHUB_REPOSITORY</c>. A job that neither checks out nor sets <c>GH_REPO</c> fails with
        /// "no git remotes found", and in the release job that happens after nuget.org is already public.
        /// </summary>
        [Fact]
        public void EveryJobRunningGhCanResolveTheRepository()
        {
            var inspected = 0;
            foreach (var file in WorkflowFiles)
            {
                foreach (var job in Jobs(File.ReadAllText(file).Replace("\r\n", "\n"))
                    .Where(j => Regex.IsMatch(j.Value.Body, @"(?m)^\s*(\S.*\s)?gh\s+[a-z-]+\s")))
                {
                    inspected++;
                    var body = job.Value.Body;
                    Assert.True(
                        body.IndexOf("actions/checkout", StringComparison.Ordinal) >= 0 ||
                        body.IndexOf("GH_REPO:", StringComparison.Ordinal) >= 0 ||
                        body.IndexOf("--repo ", StringComparison.Ordinal) >= 0,
                        Path.GetFileName(file) + ": job '" + job.Key + "' runs gh but neither checks out nor " +
                        "sets GH_REPO, so gh cannot resolve the repository.");
                }
            }

            Assert.True(inspected > 0, "No job runs gh; this gate now measures nothing.");
        }

        /// <summary>
        /// A release artifact that carries packages carries their symbol packages too. <c>*.nupkg</c> does
        /// not match <c>*.snupkg</c>, and <c>dotnet nuget push</c> uploads a symbol package only from beside
        /// its package, so one left out of the artifact never reaches the publishing runner.
        /// </summary>
        [Fact]
        public void ReleaseArtifactsCarrySymbolPackages()
        {
            var opted = Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.csproj",
                    SearchOption.AllDirectories)
                .Where(p => File.ReadAllText(p).IndexOf("<IncludeSymbols>true", StringComparison.Ordinal) >= 0)
                .ToList();
            Assert.True(opted.Count > 0,
                "No project opts into IncludeSymbols; this gate now measures nothing.");

            var inspected = 0;
            foreach (var file in WorkflowFiles)
            {
                // A solution-wide `dotnet pack -c` builds every opted-in project's symbol package; a job that
                // packs one named project (the RID-specific tools) produces none, and is not in scope here.
                foreach (var job in Jobs(File.ReadAllText(file).Replace("\r\n", "\n"))
                    .Where(j => j.Value.Body.IndexOf("upload-artifact", StringComparison.Ordinal) >= 0
                                && Regex.IsMatch(j.Value.Body, @"dotnet pack\s+-c")))
                {
                    inspected++;
                    Assert.True(job.Value.Body.IndexOf("*.snupkg", StringComparison.Ordinal) >= 0,
                        Path.GetFileName(file) + ": job '" + job.Key + "' uploads packages but not *.snupkg, " +
                        "so the symbol packages " + Path.GetFileName(opted[0]) + " produces never ship.");
                }
            }

            Assert.True(inspected > 0, "No job uploads a package artifact; this gate now measures nothing.");
        }

        /// <summary>
        /// Deployment scopes are granted to the job that deploys, never to the whole workflow. A workflow-level
        /// grant is also held by the build job, which runs for pull requests and executes third-party code.
        /// </summary>
        [Fact]
        public void DeploymentScopesAreNotGrantedWorkflowWide()
        {
            var inspected = 0;
            foreach (var file in WorkflowFiles)
            {
                var text = File.ReadAllText(file).Replace("\r\n", "\n");
                var jobs = Regex.Match(text, @"(?m)^jobs:[ \t]*$");
                if (!jobs.Success)
                    continue;

                inspected++;
                var preamble = Regex.Replace(text.Substring(0, jobs.Index), @"(?m)^[ \t]*#.*$", string.Empty);
                foreach (var scope in new[] { "pages: write", "id-token: write" })
                    Assert.True(preamble.IndexOf(scope, StringComparison.Ordinal) < 0,
                        Path.GetFileName(file) + ": '" + scope + "' is granted at workflow level, so every " +
                        "job holds it. Grant it on the job that needs it.");
            }

            Assert.True(inspected > 0, "No workflow was read; this gate now measures nothing.");
        }

        /// <summary>
        /// A drift guard sees every way generated output can change. <c>git diff</c> compares tracked files
        /// only, so a regeneration that emits a new file leaves it untracked and the guard reports nothing;
        /// staging the intent first (<c>git add -N -A</c>) exposes the addition but stages a deletion, which
        /// then hides it from the same <c>git diff</c>. <c>git status --porcelain</c> reports additions,
        /// modifications and deletions alike.
        /// </summary>
        [Fact]
        public void DriftGuardsSeeEveryKindOfChange()
        {
            var inspected = 0;
            foreach (var file in WorkflowFiles)
            {
                foreach (var job in Jobs(File.ReadAllText(file).Replace("\r\n", "\n"))
                    .Where(j => j.Value.Body.IndexOf("git diff --exit-code", StringComparison.Ordinal) >= 0
                                || j.Value.Body.IndexOf("git status --porcelain", StringComparison.Ordinal) >= 0))
                {
                    inspected++;
                    Assert.True(
                        job.Value.Body.IndexOf("git diff --exit-code", StringComparison.Ordinal) < 0,
                        Path.GetFileName(file) + ": job '" + job.Key + "' guards drift with git diff, which " +
                        "cannot see a generated file that is new and therefore untracked. Use " +
                        "git status --porcelain.");
                }
            }

            Assert.True(inspected > 0, "No drift guard found; this gate now measures nothing.");
        }

        /// <summary>
        /// Every self-test script is invoked by some workflow. A self-test nothing runs is the same defect
        /// it exists to prevent, one level up: the subject goes back to being first executed in production.
        /// </summary>
        [Fact]
        public void EverySelfTestScriptIsRunByAWorkflow()
        {
            var scripts = Directory.EnumerateFiles(Path.Combine(RepoRoot, ".github", "scripts"), "*-selftest.sh")
                .Select(Path.GetFileName)
                .ToList();
            Assert.True(scripts.Count > 0, "No self-test script found; this gate now measures nothing.");

            var workflows = WorkflowFiles.Select(File.ReadAllText).ToList();
            foreach (var script in scripts)
                Assert.True(workflows.Any(w => w.IndexOf(script, StringComparison.Ordinal) >= 0),
                    script + " is never invoked by a workflow, so nothing runs it.");
        }

        /// <summary>
        /// A job that talks to the Pages API declares the scope itself. The negative gate above keeps the
        /// deployment scopes off every job; this one keeps them on the job that needs them, so moving them
        /// cannot silently leave the deploy unable to run.
        /// </summary>
        [Fact]
        public void JobsUsingPagesActionsDeclareThePagesScope()
        {
            var inspected = 0;
            foreach (var file in WorkflowFiles)
            {
                foreach (var job in Jobs(File.ReadAllText(file).Replace("\r\n", "\n"))
                    .Where(j => Regex.IsMatch(j.Value.Body, @"uses:\s*actions/(configure|deploy)-pages@")))
                {
                    inspected++;
                    Assert.Matches(@"(?m)^\s{4}permissions:", job.Value.Body);
                    Assert.True(Regex.IsMatch(job.Value.Body, @"(?m)^\s*pages:\s*(read|write)\s*$"),
                        Path.GetFileName(file) + ": job '" + job.Key + "' uses a Pages action but declares no " +
                        "pages scope of its own, so its token has pages: none.");
                }
            }

            Assert.True(inspected > 0, "No job uses a Pages action; this gate now measures nothing.");
        }

        private static string RepoRoot =>
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(
                BuildSurfaceContractTests.FindRepoFile(
                    Path.Combine(".github", "workflows", "dotnet.yml")))));

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
