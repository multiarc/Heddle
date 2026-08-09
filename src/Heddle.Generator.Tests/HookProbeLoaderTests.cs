extern alias gen;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ProbeAssemblyLoader = gen::Heddle.Generator.Probe.ProbeAssemblyLoader;
using ReflectionHookProbe = gen::Heddle.Generator.Probe.ReflectionHookProbe;
using BodyModelSource = gen::Heddle.Language.BodyModelSource;
using ChainedModelSource = gen::Heddle.Language.ChainedModelSource;
using HookProbeOutcome = gen::Heddle.Language.HookProbeOutcome;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// The two halves of hook probing that can be tested without a build: the rule about <b>where</b> an assembly
    /// may come from, and the driver that reads a hook's answer out of one.
    /// </summary>
    public class HookProbeLoaderTests
    {
        private static string P(params string[] parts) =>
            Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar.ToString(), parts);

        private static IReadOnlyList<string> Roots(params string[] roots)
        {
            var joined = new List<string>();
            foreach (var r in roots)
                joined.Add(r);
            return ProbeAssemblyLoader.SplitPackageFolders(string.Join(";", joined));
        }

        /// <summary>The rule the whole design rests on. <c>LoadFrom</c> locks the file for the life of the
        /// compiler process, and the compiler is a server: loading a build output once breaks the NEXT build of
        /// that project. So a package-cache path is loadable and a build output never is — not even one that
        /// happens to sit under a package folder, which is why the <c>bin</c>/<c>obj</c> segment test is a second,
        /// independent term rather than a consequence of the first.</summary>
        [Theory]
        // Under the package root: the content-addressed directory nothing rewrites in place.
        [InlineData("home/u/.nuget/packages/acme.ext/1.2.3/lib/netstandard2.0/Acme.Ext.dll", true)]
        // A consumer's own build output, which is exactly what must never be locked.
        [InlineData("repo/src/App/bin/Release/net8.0/Acme.Ext.dll", false)]
        [InlineData("repo/src/App/obj/Release/net8.0/Acme.Ext.dll", false)]
        // Under the root and still a build output: both terms are load-bearing.
        [InlineData("home/u/.nuget/packages/acme.ext/1.2.3/bin/Acme.Ext.dll", false)]
        // Outside every declared root.
        [InlineData("opt/somewhere/Acme.Ext.dll", false)]
        public void OnlyPackageRootPathsAreLoadable(string relative, bool expected)
        {
            var path = P(relative.Split('/'));
            Assert.Equal(expected, ProbeAssemblyLoader.IsImmutableRoot(path, Roots(P("home", "u", ".nuget", "packages"))));
        }

        /// <summary>No roots means no probing — not "anything goes". A project that was never restored through
        /// NuGet has no immutable directory this loader knows of, and the safe reading of that is refusal.</summary>
        [Fact]
        public void WithNoDeclaredRootsNothingIsLoadable()
        {
            var path = P("home", "u", ".nuget", "packages", "acme.ext", "1.2.3", "lib", "Acme.Ext.dll");
            Assert.False(ProbeAssemblyLoader.IsImmutableRoot(path, Roots()));
            Assert.False(ProbeAssemblyLoader.IsImmutableRoot(path, null));
        }

        /// <summary>A relative path names nothing stable, so it is refused before it is resolved against whatever
        /// the process's current directory happens to be.</summary>
        [Fact]
        public void ARelativePathIsNeverLoadable()
        {
            Assert.False(ProbeAssemblyLoader.IsImmutableRoot(
                Path.Combine("lib", "Acme.Ext.dll"), Roots(P("home", "u", ".nuget", "packages"))));
        }

        /// <summary>NuGet writes the folder list semicolon-separated and with a trailing separator; both spellings
        /// have to reach the same prefix test, or the rule passes or fails on punctuation.</summary>
        [Fact]
        public void PackageFolderListIsSplitAndNormalized()
        {
            var roots = ProbeAssemblyLoader.SplitPackageFolders(
                P("home", "u", ".nuget", "packages") + Path.DirectorySeparatorChar + ";" +
                P("opt", "fallback") + "; ;");
            Assert.Equal(2, roots.Count);
            Assert.True(ProbeAssemblyLoader.IsImmutableRoot(
                P("opt", "fallback", "acme", "1.0", "lib", "A.dll"), roots));
        }

        /// <summary>An assembly this process already holds is handed back rather than loaded again: loading is
        /// what locks a file, and reuse locks nothing. It is also how this suite gets a real engine to drive the
        /// driver against without ever putting a lock on a build output.</summary>
        [Fact]
        public void AnAlreadyLoadedAssemblyIsReusedWithoutLoadingAnything()
        {
            var engine = typeof(global::Heddle.HeddleTemplate).Assembly;
            var loader = new ProbeAssemblyLoader(Roots(), new[] { engine.Location });

            Assert.False(ProbeAssemblyLoader.IsImmutableRoot(engine.Location, Roots()));
            Assert.Same(engine, loader.TryLoad(engine.Location));
            Assert.Same(engine, loader.TryLoadBySimpleName("Heddle"));
        }

        /// <summary>A path that is neither already loaded nor under an immutable root comes back null — and does
        /// not throw, because a hook the build cannot read is a degraded call site, never a failed build.</summary>
        [Fact]
        public void AnUnloadablePathIsRefusedWithoutThrowing()
        {
            var loader = new ProbeAssemblyLoader(Roots(P("home", "u", ".nuget", "packages")), new string[0]);
            Assert.Null(loader.TryLoad(P("repo", "bin", "Debug", "Nope.dll")));
            Assert.Null(loader.TryLoad(null));
            Assert.Null(loader.TryLoadBySimpleName("Nope"));
        }

        /// <summary><b>The driver, end to end.</b> The reflection driver compiles the protocol's documents through a
        /// real engine assembly it did not link, hands it sentinel types that live in the <i>generator's</i>
        /// assembly, and reads the hook's own answers back out of the engine's compile-time state. The rows below
        /// are the roles the engine-side lockstep suite proves against the same protocol — this asserts the
        /// reflection half reaches them too.</summary>
        [Theory]
        [InlineData("if", "Parent", "None", false)]
        [InlineData("for", "Parent", "Int32Index", false)]
        [InlineData("list", "ElementOfData", "Int32Index", false)]
        [InlineData("string", "Parent", "None", false)]
        [InlineData("guid", "Parent", "None", false)]
        [InlineData("out", "Chained", "Parent", false)]
        [InlineData("swap", "Chained", "Data", false)]
        [InlineData("", "Data", "None", false)]
        [InlineData("model", "Data", "None", true)]
        public void TheReflectionDriverReadsEveryRoleOffARealEngine(string name, string body, string chained,
            bool zeroOutput)
        {
            var probe = CreateProbe();
            Assert.True(probe.TryProbe(name, out var result), "'" + name + "' was not probeable.");
            Assert.Equal(HookProbeOutcome.Classified, result.Outcome);
            Assert.Equal(Enum.Parse(typeof(BodyModelSource), body), result.Body);
            Assert.Equal(Enum.Parse(typeof(ChainedModelSource), chained), result.Chained);
            Assert.Equal(zeroOutput, result.ZeroOutput);
        }

        /// <summary><b>The shadow-mode half that lives on this side.</b> For every name
        /// <c>BodyModelRules</c> pins, the reflection driver must produce that exact row. The engine-side lockstep
        /// suite proves the same thing for every registered extension against the engine it is compiled with; this
        /// proves the reading survives the assembly boundary, which is the only new thing the build tier adds.
        /// <para>The probe is ground truth, not the table. Adjusting one until it agreed with the other would
        /// invert the point: the table is the artifact under suspicion, and it was already caught wrong once.</para>
        /// </summary>
        [Fact]
        public void TheProbeAgreesWithEveryPinnedTableRow()
        {
            var probe = CreateProbe();
            var disagreements = new List<string>();
            foreach (var name in gen::Heddle.Language.BodyModelRules.PinnedNames)
            {
                Assert.True(gen::Heddle.Language.BodyModelRules.TryGet(name, out var body, out var chained));
                if (!probe.TryProbe(name, out var observed))
                {
                    disagreements.Add("@" + name + ": the table has a row and the probe has no answer");
                    continue;
                }

                if (observed.Outcome != HookProbeOutcome.Classified || !Equals(observed.Body, body) ||
                    !Equals(observed.Chained, chained))
                    disagreements.Add("@" + name + ": table says (" + body + ", " + chained + "), probe says (" +
                                      observed.Outcome + ", " + observed.Body + ", " + observed.Chained + ")");
            }

            Assert.True(disagreements.Count == 0, string.Join("; ", disagreements));
        }

        /// <summary>An extension that compiles no body is a shape, not a failure: the driver says so rather than
        /// inventing a body typing for it.</summary>
        [Fact]
        public void AnExtensionThatCompilesNoBodyIsReportedAsSuch()
        {
            var probe = CreateProbe();
            Assert.True(probe.TryProbe("import", out var result));
            Assert.Equal(HookProbeOutcome.NoBody, result.Outcome);
            Assert.True(result.ZeroOutput);
        }

        /// <summary>A name the engine does not register has no hook to read, so the driver has no answer — the
        /// call site keeps whatever the emitter would have done without a probe. Asked rather than inferred: an
        /// unknown name compiles no body and returns nothing, which is indistinguishable from a real
        /// <c>NoBody</c> extension if the registry is not consulted first.</summary>
        [Fact]
        public void AnUnregisteredNameHasNoAnswer()
        {
            var probe = CreateProbe();
            Assert.False(probe.TryProbe("no-such-extension-name", out _));
            Assert.False(probe.TryProbe(null, out _));
        }

        /// <summary>The bootstrap gates construction itself: an engine whose <c>@param</c> does not behave as the
        /// protocol describes yields no probe at all, because the chained channel of two of the three documents is
        /// built on exactly that property.</summary>
        [Fact]
        public void AnEngineWithoutTheProtocolShapeYieldsNoProbe()
        {
            Assert.Null(ReflectionHookProbe.TryCreate(typeof(HookProbeLoaderTests).Assembly));
            Assert.Null(ReflectionHookProbe.TryCreate(null));
        }

        private static ReflectionHookProbe CreateProbe()
        {
            var probe = ReflectionHookProbe.TryCreate(typeof(global::Heddle.HeddleTemplate).Assembly);
            Assert.NotNull(probe);
            return probe;
        }
    }
}
