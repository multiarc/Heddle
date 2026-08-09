extern alias gen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
// BranchRole is no longer a generator-local mirror — it is Heddle.Attributes.BranchRole, linked into
// Heddle.Generator as shared source. Both referenced assemblies therefore declare it, so the generator's copy
// (the one ExtensionBinder.Info exposes) is named through the `gen` alias.
using BranchRole = gen::Heddle.Attributes.BranchRole;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// Tests <see cref="ExtensionBinder"/>, which resolves Role/HasScopeChannel/IsBranchParticipant from built-ins and source-declared types.
    /// Verifies inheritance walk and degradation of future/out-of-range values.
    /// </summary>
    public class ExtensionBinderRoleTests
    {
        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = Heddle.Generator.Tests.HostAssemblies.TrustedOrLoaded();
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                // Heddle.Generator is an *analyzer*, never a reference — and it carries linked copies of runtime
                // types (Heddle.Attributes.BranchRole), so handing it to a probe compilation alongside Heddle.dll
                // makes those names ambiguous (CS0433). Same filter the harnesses apply.
                .Where(p => !string.Equals(Path.GetFileNameWithoutExtension(p), "Heddle.Generator",
                    StringComparison.OrdinalIgnoreCase))
                .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
                .ToList();
            // The runtime engine assembly (carries IfExtension/ElifExtension/… with [BranchRole]/[ScopeChannel]).
            refs.Add(MetadataReference.CreateFromFile(
                typeof(Heddle.Precompiled.PrecompiledTemplates).Assembly.Location));
            return refs;
        }

        private static ExtensionBinder Bind(string source = null)
        {
            var trees = source == null
                ? Array.Empty<Microsoft.CodeAnalysis.SyntaxTree>()
                : new[] { CSharpSyntaxTree.ParseText(source) };
            var compilation = CSharpCompilation.Create("BinderRoleTest",
                trees,
                References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return ExtensionBinder.Build(compilation);
        }

        // expectedRole passed as int (0=Opener, 1=Continuation, 2=Terminal) so the public signature does not
        // expose the internal BranchRole enum (CS0051).
        [Theory]
        [InlineData("if", 0, false)]
        [InlineData("ifnot", 0, false)]
        [InlineData("elif", 1, true)]
        [InlineData("elseif", 1, true)]
        [InlineData("else", 2, true)]
        public void ResolvesEngineBuiltInRoles(string name, int expectedRole, bool expectedChannel)
        {
            var binder = Bind();

            Assert.True(binder.TryResolve(name, out var info));
            Assert.True(info.IsEngineAssembly);
            Assert.Equal((BranchRole)expectedRole, info.Role);
            Assert.Equal(expectedChannel, info.HasScopeChannel);
            // IsBranchParticipant is true only for Continuation/Terminal.
            var expectedParticipant = expectedRole == (int)BranchRole.Continuation ||
                                      expectedRole == (int)BranchRole.Terminal;
            Assert.Equal(expectedParticipant, info.IsBranchParticipant);
        }

        [Fact]
        public void EngineOpenersAreNotBranchParticipants()
        {
            var binder = Bind();
            Assert.True(binder.TryResolve("if", out var ifInfo));
            Assert.False(ifInfo.IsBranchParticipant);
            Assert.True(binder.TryResolve("ifnot", out var ifNotInfo));
            Assert.False(ifNotInfo.IsBranchParticipant);
        }

        private static readonly string TrioSource = GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace CustomBranch
{
    [ExtensionName(""begin"")]
    [BranchRole(BranchRole.Opener)]
    public class BeginExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    [ExtensionName(""between"")]
    [ScopeChannel]
    [BranchRole(BranchRole.Continuation)]
    public class BetweenExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    [ExtensionName(""finish"")]
    [ScopeChannel]
    [BranchRole(BranchRole.Terminal)]
    public class FinishExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    // Derives an Opener with no re-attribution — must inherit Opener via the base-type-chain walk.
    [ExtensionName(""begin2"")]
    public class Begin2Extension : BeginExtension
    {
    }

    // Out-of-range ctor value (a future enum member from a newer engine) → Role == null.
    [ExtensionName(""weird"")]
    [BranchRole((BranchRole)99)]
    public class WeirdExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }
}");

        [Fact]
        public void ResolvesSourceDeclaredTrioRoles()
        {
            var binder = Bind(TrioSource);

            Assert.True(binder.TryResolve("begin", out var begin));
            Assert.Equal(BranchRole.Opener, begin.Role);
            Assert.False(begin.HasScopeChannel);
            Assert.False(begin.IsBranchParticipant);
            Assert.False(begin.IsEngineAssembly);

            Assert.True(binder.TryResolve("between", out var between));
            Assert.Equal(BranchRole.Continuation, between.Role);
            Assert.True(between.HasScopeChannel);
            Assert.True(between.IsBranchParticipant);

            Assert.True(binder.TryResolve("finish", out var finish));
            Assert.Equal(BranchRole.Terminal, finish.Role);
            Assert.True(finish.HasScopeChannel);
            Assert.True(finish.IsBranchParticipant);
        }

        [Fact]
        public void InheritanceWalkResolvesBaseRole()
        {
            var binder = Bind(TrioSource);

            Assert.True(binder.TryResolve("begin2", out var begin2));
            Assert.Equal(BranchRole.Opener, begin2.Role);
        }

        [Fact]
        public void OutOfRangeRoleValueDegradesToNull()
        {
            var binder = Bind(TrioSource);

            Assert.True(binder.TryResolve("weird", out var weird));
            Assert.Null(weird.Role);
            Assert.False(weird.IsBranchParticipant);
        }

        /// <summary>The two roles the emitter's slot-projection and child-template routes now dispatch on, read off
        /// the built-ins that declare them rather than off the names they answer to. Every other built-in carries
        /// neither, which is what makes the declaration — and not the name — the thing the routes are keyed by.
        /// </summary>
        [Theory]
        [InlineData("out", true, false)]
        [InlineData("partial", false, true)]
        [InlineData("if", false, false)]
        [InlineData("list", false, false)]
        [InlineData("", false, false)]
        public void ResolvesEngineBuiltInProjectionAndHostRoles(string name, bool projection, bool host)
        {
            var binder = Bind();

            Assert.True(binder.TryResolve(name, out var info));
            Assert.True(info.IsEngineAssembly);
            Assert.Equal(projection, info.HasSlotProjection);
            Assert.Equal(host, info.HasChildTemplateHost);
        }

        private static readonly string RoleSource = GeneratorHarness.WithAllExtensionsExported(@"
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace CustomRoles
{
    [ExtensionName(""project"")]
    [SlotProjection]
    public class ProjectExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    [ExtensionName(""include"")]
    [ChildTemplateHost]
    public class IncludeExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    [ExtensionName(""both"")]
    [SlotProjection]
    [ChildTemplateHost]
    public class BothExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    [ExtensionName(""plain"")]
    public class PlainExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => string.Empty;
        public override void RenderData(in Scope scope) { }
    }

    // Neither re-declares its role: both attributes are Inherited = true, so the base-type-chain walk has to
    // find them, exactly as it does for [ScopeChannel] and [BranchRole].
    [ExtensionName(""myout"")]
    public class MyOutExtension : Heddle.Extensions.OutExtension
    {
    }

    [ExtensionName(""mypartial"")]
    public class MyPartialExtension : Heddle.Extensions.PartialExtension
    {
    }
}");

        [Fact]
        public void ResolvesSourceDeclaredProjectionAndHostRoles()
        {
            var binder = Bind(RoleSource);

            Assert.True(binder.TryResolve("project", out var project));
            Assert.False(project.IsEngineAssembly);
            Assert.True(project.HasSlotProjection);
            Assert.False(project.HasChildTemplateHost);

            Assert.True(binder.TryResolve("include", out var include));
            Assert.False(include.HasSlotProjection);
            Assert.True(include.HasChildTemplateHost);

            Assert.True(binder.TryResolve("plain", out var plain));
            Assert.False(plain.HasSlotProjection);
            Assert.False(plain.HasChildTemplateHost);
        }

        /// <summary>Both roles on one type is reported as both, not silently narrowed to one: the binder says what
        /// the type declares and the emitter decides what to do about it (it refuses — the two roles describe two
        /// incompatible call shapes).</summary>
        [Fact]
        public void BothRolesOnOneTypeAreBothReported()
        {
            var binder = Bind(RoleSource);

            Assert.True(binder.TryResolve("both", out var both));
            Assert.True(both.HasSlotProjection);
            Assert.True(both.HasChildTemplateHost);
        }

        [Fact]
        public void InheritanceWalkResolvesBaseProjectionAndHostRoles()
        {
            var binder = Bind(RoleSource);

            Assert.True(binder.TryResolve("myout", out var myOut));
            Assert.True(myOut.HasSlotProjection);
            Assert.False(myOut.HasChildTemplateHost);

            Assert.True(binder.TryResolve("mypartial", out var myPartial));
            Assert.True(myPartial.HasChildTemplateHost);
            Assert.False(myPartial.HasSlotProjection);
        }

        /// <summary>A compilation whose engine declares <c>AbstractExtension</c> and <c>[ExtensionName]</c> but
        /// neither role attribute — the shape a consumer referencing a Heddle older than the roles is in.
        /// <c>GetTypeByMetadataName</c> answers <c>null</c> for both attribute symbols there.</summary>
        private static ExtensionBinder BindAgainstEngineWithoutRoleAttributes()
        {
            const string olderEngine = @"
namespace Heddle.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class ExtensionNameAttribute : System.Attribute
    {
        public ExtensionNameAttribute(string name) { Name = name; }
        public string Name { get; }
    }
}

namespace Heddle.Core
{
    public abstract class AbstractExtension
    {
    }
}

namespace OlderEngine
{
    [Heddle.Attributes.ExtensionName(""out"")]
    public class OlderOutExtension : Heddle.Core.AbstractExtension
    {
    }

    [Heddle.Attributes.ExtensionName(""partial"")]
    public class OlderPartialExtension : Heddle.Core.AbstractExtension
    {
    }
}";
            var references = References
                .Where(r => !(r.Display ?? string.Empty).EndsWith("Heddle.dll", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var compilation = CSharpCompilation.Create("BinderRoleOlderEngine",
                new[] { CSharpSyntaxTree.ParseText(olderEngine) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return ExtensionBinder.Build(compilation);
        }

        /// <summary>The unresolvable-symbol degrade, which is the whole reason the roles are read through the
        /// guarded attribute walk: against an engine that predates them nothing carries either role, the reads
        /// answer false instead of throwing, and the call falls to the ordinary bound-extension route — the name
        /// it happens to answer to buys it nothing.</summary>
        [Fact]
        public void AnEngineWithoutTheRoleAttributesResolvesBothRolesAsFalse()
        {
            var binder = BindAgainstEngineWithoutRoleAttributes();

            Assert.True(binder.TryResolve("out", out var outInfo));
            Assert.False(outInfo.HasSlotProjection);
            Assert.False(outInfo.HasChildTemplateHost);

            Assert.True(binder.TryResolve("partial", out var partialInfo));
            Assert.False(partialInfo.HasSlotProjection);
            Assert.False(partialInfo.HasChildTemplateHost);
        }
    }
}
