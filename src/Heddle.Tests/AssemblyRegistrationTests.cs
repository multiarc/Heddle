using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Native;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The engine loads no assembly on its own, and only assemblies a host registers offer extension names. An
    /// assembly that merely happens to be in the process must never take a name or a type spelling.
    /// </summary>
    public class AssemblyRegistrationTests
    {

        /// <summary>
        /// An explicit static constructor is the shape that force-loaded the entry assembly's whole reference closure
        /// at type init, and <c>beforefieldinit</c> is present exactly when a type declares none.
        /// <para><b>This checks the shape, not the behaviour.</b> A static field initializer runs at type init too and
        /// keeps <c>beforefieldinit</c>, so <c>private static readonly int _ = WalkEverything();</c> would pass here.
        /// <see cref="AnObservedOnDiskAssemblyOffersNoExtensionNameUntilRegistered"/> is what actually pins the
        /// behaviour, whatever triggers it; this one is a cheap guard on the specific shape that caused the defect.</para>
        /// </summary>
        [Fact]
        public void AssemblyHelperDeclaresNoStaticConstructor()
        {
            Assert.True(typeof(AssemblyHelper).Attributes.HasFlag(TypeAttributes.BeforeFieldInit),
                "AssemblyHelper declares an explicit static constructor.");
        }

        /// <summary>The dependency-context walk was the only consumer, so the package reference went with it. Reads
        /// the emitted assembly references, so what this pins is that no engine code <b>uses</b> DependencyModel — a
        /// restored but unused PackageReference stays green, which is the property worth having.</summary>
        [Fact]
        public void NoEngineCodeUsesTheDependencyModelPackage()
        {
            Assert.DoesNotContain("Microsoft.Extensions.DependencyModel",
                typeof(HeddleTemplate).Assembly.GetReferencedAssemblies().Select(name => name.Name));
        }

        /// <summary>
        /// The registration seam end to end: an assembly carrying <c>[ExportExtensions]</c> is compiled and loaded
        /// into the process, then asserted invisible until registered — neither its extension name nor its type
        /// spelling resolves. One <see cref="HeddleTemplate.Register(Assembly)"/> call makes both appear.
        /// </summary>
        [Fact]
        public void LoadedAssemblyOffersNothingUntilRegistered()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var extensionName = "probe" + suffix;
            var typeName = "ProbeExtension" + suffix;
            var probe = CompileAndLoad(ProbeSource(suffix, extensionName, typeName));

            Assert.False(TemplateFactory.Exists(extensionName),
                "an unregistered assembly took an extension name");
            Assert.Throws<InvalidOperationException>(
                () => ReflectionHelper.ResolveType($"ProbeNamespace{suffix}.{typeName}"));

            HeddleTemplate.Register(probe);

            Assert.True(TemplateFactory.Exists(extensionName));
            Assert.NotNull(ReflectionHelper.ResolveType($"ProbeNamespace{suffix}.{typeName}"));
        }

        /// <summary>Registration is repeatable: a host may register in whatever order it establishes precedence.</summary>
        [Fact]
        public void RegisteringTheSameAssemblyTwiceIsIdempotent()
        {
            var assembly = typeof(AssemblyRegistrationTests).Assembly;
            HeddleTemplate.Register(assembly);
            HeddleTemplate.Register(assembly);
        }

        /// <summary>
        /// The behavioural pin, and the one that matters. The sibling tests pin the *mechanism* the old walk used — no
        /// explicit static constructor, no DependencyModel — and a review found that restoring the same discovery
        /// lazily, from <c>GetAssemblies()</c> instead of a type initializer, passed the whole suite. This test states
        /// the behaviour instead: an assembly written to disk and loaded into the default context, which every
        /// observation path therefore sees, must still offer no extension name until the host registers it. Any
        /// reintroduced scan-all reddens here whatever triggers it.
        /// <para>File-backed on purpose: an in-memory probe has no <c>Location</c>, so the observation filter skips it
        /// and the negative assertion would hold for the wrong reason.</para>
        /// </summary>
        [Fact]
        public void AnObservedOnDiskAssemblyOffersNoExtensionNameUntilRegistered()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var extensionName = "ondisk" + suffix;
            var probe = CompileToFileAndLoad(ProbeSource(suffix, extensionName, "OnDiskExtension" + suffix));

            Assert.False(string.IsNullOrEmpty(probe.Location),
                "the probe must be file-backed, or it is skipped by the observation filter and proves nothing");
            Assert.Contains(probe, AssemblyHelper.GetAssemblies());

            Assert.False(TemplateFactory.Exists(extensionName),
                "an observed but unregistered on-disk assembly took an extension name — scan-all discovery is back");

            HeddleTemplate.Register(probe);
            Assert.True(TemplateFactory.Exists(extensionName));
        }

        /// <summary>
        /// An assembly the host loads <b>after</b> the engine has already resolved a type must still become visible.
        /// Before this was pinned, the name maps were built once and never rebuilt from observation, so whether
        /// <c>@model Some.Late.Type</c> resolved depended on whether an unrelated earlier compile had happened —
        /// order-dependence no host can reason about, and it falsified the documented rule that the set is what the
        /// host has loaded plus what it registers.
        /// </summary>
        [Fact]
        public void AnAssemblyLoadedAfterTheFirstResolutionStillResolves()
        {
            // Force the maps to exist first — this is the "unrelated earlier compile" that used to decide the outcome.
            Assert.NotNull(ReflectionHelper.ResolveType("System.DateTime"));

            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var typeName = "LateExtension" + suffix;
            var probe = CompileToFileAndLoad(ProbeSource(suffix, "late" + suffix, typeName));

            Assert.NotNull(ReflectionHelper.ResolveType($"ProbeNamespace{suffix}.{typeName}"));
        }

        /// <summary>
        /// The same promise as <see cref="AnAssemblyLoadedAfterTheFirstResolutionStillResolves"/>, reached the way it
        /// actually broke. Observation skipped its work whenever the loaded-assembly <b>count</b> matched the last
        /// pass, and a count cannot tell "nothing happened" from "one assembly went away and another arrived" — the
        /// exact shape of an unloaded collectible context, which is a scenario the engine supports and which the
        /// language service performs on every model reload. The assembly loaded in that window stayed invisible, and
        /// stayed invisible on retry, until some unrelated later load disturbed the count.
        /// </summary>
        [Fact]
        public void AnAssemblyLoadedAfterACollectibleUnloadStillResolves()
        {
            Assert.NotNull(ReflectionHelper.ResolveType("System.DateTime"));

            // Everything that could itself observe happens up front. Compiling the probe reads the reference set, so
            // emitting it after the unload would disturb the count and repair the very state under test.
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var typeName = "AfterUnload" + suffix;
            var probePath = WriteProbe(ProbeSource(suffix, "afterunload" + suffix, typeName));
            var scratchBytes = Emit(ProbeSource(Guid.NewGuid().ToString("N").Substring(0, 12), "s", "S"), out _);

            var unloaded = LoadIntoCollectibleContextAndUnload(scratchBytes);
            for (var i = 0; i < 12 && unloaded.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Assert.False(unloaded.IsAlive,
                "the collectible context must actually collect, or the count never drops and nothing is being tested");

            // One assembly gone, one arriving: the count lands exactly where observation last left it.
            Assembly.LoadFrom(probePath);

            Assert.NotNull(ReflectionHelper.ResolveType($"ProbeNamespace{suffix}.{typeName}"));
        }

        /// <summary>
        /// The engine must not <b>load</b> anything — the half of the promise its siblings do not cover. They pin
        /// that an observed assembly takes no extension name, which is about scanning; a review showed the deleted
        /// transitive load walk could be restored lazily and the entire suite stayed green.
        /// <para>Stated as behaviour: an assembly that is referenced by a loaded assembly but has not itself been
        /// loaded must still not be loaded after the engine has observed, compiled and rendered. This is also what
        /// makes the published rule true that naming a model type in an assembly the host has never touched does not
        /// resolve it: were the closure walked, it would.</para>
        /// <para><b>What it does not cover.</b> A walk that runs <i>once</i>, at startup, has already run before this
        /// test can build its probe, so a one-shot closure walk still passes — verified, not assumed. Closing that
        /// needs a child process comparing the loaded set before and after the engine is first touched, which no
        /// suite here does. What reddens is any walk on an observation pass, which is where both the deleted walk and
        /// the review's reimplementation of it lived.</para>
        /// </summary>
        [Fact]
        public void ObservingAndRenderingLoadsNoReferencedAssembly()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 12);
            var leafPath = WriteProbe(LeafSource(suffix), out var leafName);
            var referrer = Assembly.LoadFrom(WriteProbeReferencing(suffix, leafPath));

            Assert.Contains(leafName, referrer.GetReferencedAssemblies().Select(n => n.Name));
            Assert.DoesNotContain(leafName, LoadedNames());

            AssemblyHelper.GetAssemblies();
            using (var template = new HeddleTemplate("@model(){{dynamic}}@(Length)",
                       new CompileContext(new TemplateOptions())))
            {
                Assert.True(template.CompileResult.Success);
                template.Generate("probe");
            }

            Assert.DoesNotContain(leafName, LoadedNames());
        }

        /// <summary>
        /// Two assemblies with one identity: one registered by a host out of a context of its own, one the host later
        /// loads normally. The registered one holds the name, so the loaded one is passed over — deliberately, and
        /// without being written off, because the name may be given back.
        /// <para>Giving it back is where this used to stop. Unregistration frees the name but loads and unloads
        /// nothing, so the digest that decides whether a classifying pass is worth doing is unchanged, the pass
        /// returns before reaching the assembly, and the freed name stays unusable until something unrelated happens
        /// to load. The same assertion catches the other half: an assembly marked classified <i>before</i> the name
        /// was actually taken is skipped for the life of the process, and no unregistration can bring it back.</para>
        /// </summary>
        [Fact]
        public void AFreedNameIsRetakenByTheLoadedAssemblyThatLostIt()
        {
            var identity = "HeddleCollisionProbe" + Guid.NewGuid().ToString("N");
            var registeredBytes = CompileAs(identity, "namespace CollisionProbe { public class Registered { } }");
            var loadedBytes = CompileAs(identity, "namespace CollisionProbe { public class Loaded { } }");

            // Out of a context of its own, so observation never sees it and only the registration puts it in play —
            // the shape a workspace's model assemblies arrive in.
            var host = new AssemblyLoadContext("collision-probe", isCollectible: true);
            // Unregistration is process-wide — there is no per-assembly form — so the cleanup runs only on the paths
            // that left this test's registration in place. Calling it unconditionally would also drop registrations
            // made by whatever ran before, which is safe today only because this suite forbids parallel collections.
            var registered = false;
            try
            {
                AssemblyHelper.RegisterModelAssemblies(new[] { host.LoadFromStream(new MemoryStream(registeredBytes)) });
                registered = true;

                // From disk, because that is what lands in the default context — the only one observation looks at.
                var loaded = Assembly.LoadFrom(WriteBytes(loadedBytes, identity));
                Assert.DoesNotContain(loaded, Observed());

                AssemblyHelper.UnregisterModelAssemblies();
                registered = false;
                Assert.Contains(loaded, Observed());
            }
            finally
            {
                if (registered)
                    AssemblyHelper.UnregisterModelAssemblies();
                host.Unload();
            }
        }

        // Two orderings inside ObserveLoadedAssemblies are NOT pinned here, and cannot be from inside this process.
        // That the generation is bumped before the digest is written, and that GetApplicationReferences reads the
        // generation under the same monitor it builds the reference set under, are both statements about what no
        // concurrent caller can observe: with one thread the order of two writes made before a lock is released is
        // unobservable, and a test that races to catch the wrong order passes by luck when the code is wrong. They
        // are arguments made where the code makes them impossible, not properties a test can assert.

        /// <summary>A copy taken under the list's monitor, which callers that enumerate are required to hold.</summary>
        private static Assembly[] Observed()
        {
            var assemblies = AssemblyHelper.GetAssemblies();
            lock (assemblies)
                return assemblies.ToArray();
        }

        private static byte[] CompileAs(string assemblyName, string source)
        {
            var compilation = CSharpCompilation.Create(assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source) },
                AssemblyHelper.GetApplicationReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success,
                string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())));
            return stream.ToArray();
        }

        private static IEnumerable<string> LoadedNames()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name);
        }

        private static string LeafSource(string suffix) => $@"
namespace LeafNamespace{suffix}
{{
    public class Leaf{suffix} {{ public int Value {{ get; set; }} }}
}}";

        /// <summary>Emits an assembly whose public surface uses the leaf's type, so the reference is real and the
        /// runtime still has no reason to load it until something touches that type.</summary>
        private static string WriteProbeReferencing(string suffix, string leafPath)
        {
            var source = $@"
namespace ReferrerNamespace{suffix}
{{
    public class Referrer{suffix}
    {{
        public LeafNamespace{suffix}.Leaf{suffix} Make() => new LeafNamespace{suffix}.Leaf{suffix}();
    }}
}}";
            var references = AssemblyHelper.GetApplicationReferences();
            references.Add(MetadataReference.CreateFromFile(leafPath));
            return WriteBytes(Compile(source, references, out var name), name);
        }

        /// <summary>Loads into a collectible context and unloads it, in its own frame. A debug build keeps every
        /// local of a method rooted until that method returns, so doing this inline leaves the context uncollectable
        /// and the loaded-assembly count never falls.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference LoadIntoCollectibleContextAndUnload(byte[] assembly)
        {
            var context = new AssemblyLoadContext("collectible-probe", isCollectible: true);
            context.LoadFromStream(new MemoryStream(assembly));
            AssemblyHelper.GetAssemblies();
            context.Unload();
            return new WeakReference(context);
        }

        [Fact]
        public void RegisterRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => HeddleTemplate.Register(null));
        }

        private static string ProbeSource(string suffix, string extensionName, string typeName) => $@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

[assembly: ExportExtensions(typeof(ProbeNamespace{suffix}.{typeName}))]

namespace ProbeNamespace{suffix}
{{
    [ExtensionName(""{extensionName}"")]
    public class {typeName} : AbstractExtension
    {{
        public override object ProcessData(in Scope scope) => ""probe"";
        public override void RenderData(in Scope scope) => scope.Renderer.Render(""probe"");
    }}
}}";

        /// <summary>Emits the probe to a real file and loads it by path, so it enters the default load context with a
        /// non-empty <see cref="Assembly.Location"/> — the shape the observation path accepts.</summary>
        private static Assembly CompileToFileAndLoad(string source)
        {
            return Assembly.LoadFrom(WriteProbe(source));
        }

        /// <summary>Emits the probe to a real file and returns the path, loading nothing.</summary>
        private static string WriteProbe(string source)
        {
            return WriteProbe(source, out _);
        }

        private static string WriteProbe(string source, out string assemblyName)
        {
            var bytes = Compile(source, AssemblyHelper.GetApplicationReferences(), out assemblyName);
            return WriteBytes(bytes, assemblyName);
        }

        /// <summary>Writes beside the test assembly, not to the temp directory: a referenced probe has to be
        /// <b>findable</b> for the negative assertion to mean the runtime chose not to load it. Cleaned up at
        /// process exit — see <see cref="ProbeAssemblyFiles"/> for why not sooner.</summary>
        private static string WriteBytes(byte[] bytes, string assemblyName) =>
            ProbeAssemblyFiles.WriteBesideTestAssembly(bytes, assemblyName);

        private static Assembly CompileAndLoad(string source)
        {
            return Assembly.Load(Emit(source, out _));
        }

        private static byte[] Emit(string source, out string assemblyName)
        {
            return Compile(source, AssemblyHelper.GetApplicationReferences(), out assemblyName);
        }

        private static byte[] Compile(string source, List<MetadataReference> references, out string assemblyName)
        {
            assemblyName = "Probe" + Guid.NewGuid().ToString("N");
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            Assert.True(result.Success,
                string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())));

            return stream.ToArray();
        }
    }
}
