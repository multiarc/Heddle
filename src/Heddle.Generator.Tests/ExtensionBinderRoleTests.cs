using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Generator.Emit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>
    /// WI3 binder unit tests (§8.3): <see cref="ExtensionBinder"/> is the single role source. It resolves
    /// <c>Role</c>/<c>HasScopeChannel</c>/<c>IsBranchParticipant</c> for the four engine built-ins from the
    /// referenced <c>Heddle</c> assembly, reads <c>[BranchRole]</c> from source-declared types (including a
    /// base-type-chain walk for inheritance), and degrades a future/out-of-range value to <c>null</c>.
    /// </summary>
    public class ExtensionBinderRoleTests
    {
        private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var refs = tpa.Split(Path.PathSeparator)
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
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

        private const string TrioSource = @"
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
}";

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
    }
}
