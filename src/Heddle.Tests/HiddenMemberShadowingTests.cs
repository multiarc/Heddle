using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>The most-derived declaration of a name decides whether a template can reach it. Pins the
    /// sandbox regression where the member walk stepped past a <c>[Hidden]</c> override and bound the base
    /// class's visible property of the same name — which virtual dispatch then answered with the very value
    /// the derived type had hidden. Every row is a positioned <c>HED0001</c> and the getter never runs.</summary>
    public class HiddenMemberShadowingTests
    {
        public static bool TrapExecuted;

        private static string Trap()
        {
            TrapExecuted = true;
            throw new InvalidOperationException("a withheld getter must never run");
        }

        public class VaultBase
        {
            public virtual string Overridden { get; set; } = "base-overridden";
            public string Shadowed { get; set; } = "base-shadowed";
            public string MadePrivate { get; set; } = "base-private";
            public string MadeStatic { get; set; } = "base-static";
            public string MadeField { get; set; } = "base-field";
            public string MadeMethod { get; set; } = "base-method";
            public string Open { get; set; } = "base-open";
            public virtual string Reopened { get; set; } = "base-reopened";

            public virtual string this[int index] => "base-indexer";
        }

        public class Vault : VaultBase
        {
            [Hidden] public override string Overridden { get => Trap(); set { } }

            [Hidden] public new string Shadowed => Trap();

            private new string MadePrivate => Trap();

            public new static string MadeStatic => Trap();

#pragma warning disable CS0649
            public new string MadeField;
#pragma warning restore CS0649

            public new string MadeMethod() => Trap();

            public override string Reopened { get; set; } = "derived-reopened";

            [Hidden] public override string this[int index] => Trap();

            public string Touch() => MadePrivate;
        }

        public interface IByPosition
        {
            object this[int index] { get; }
        }

        public interface IByKey
        {
            object this[object key] { get; }
        }

        /// <summary>Explicit interface indexers are private members named after their interface. They hide
        /// nothing: <c>shelf[0]</c> in C# is still the inherited public indexer.</summary>
        public class Shelf : System.Collections.ObjectModel.Collection<string>, IByPosition, IByKey
        {
            object IByPosition.this[int index] => Trap();

            object IByKey.this[object key] => Trap();
        }

        public class RenamedBase
        {
            [System.Runtime.CompilerServices.IndexerName("Slot")]
            public virtual string this[int index] => "base-slot";
        }

        public class RenamedHidden : RenamedBase
        {
            [Hidden]
            [System.Runtime.CompilerServices.IndexerName("Slot")]
            public override string this[int index] => Trap();
        }

        public class VaultLeaf : Vault
        {
        }

        public class VaultHolder
        {
            public Vault Inner { get; set; } = new Vault();
        }

        public static IEnumerable<object[]> WithheldNames()
        {
            yield return new object[] { "Overridden" };
            yield return new object[] { "Shadowed" };
            yield return new object[] { "MadePrivate" };
            yield return new object[] { "MadeStatic" };
            yield return new object[] { "MadeField" };
            yield return new object[] { "MadeMethod" };
        }

        private static HeddleCompileResult Compile(string template, Type modelType, ExpressionMode mode)
        {
            HeddleTemplate.Configure(typeof(HiddenMemberShadowingTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = mode, OutputProfile = OutputProfile.Text };
            return new HeddleTemplate(template, new CompileContext(options, modelType)).CompileResult;
        }

        /// <summary>Positions are clean-document offsets of the call, which starts after its '@'.</summary>
        private static void AssertNotFound(HeddleCompileResult result, int start, int length)
        {
            Assert.False(result.Success, "expected a compile error; got success");
            var error = result.Errors.FirstOrDefault(e => e.DiagnosticId == HeddleDiagnosticIds.PropertyNotFound);
            Assert.True(error != null, "expected HED0001; got: " + result);
            Assert.Equal(start, error.Position.StartIndex);
            Assert.Equal(length, error.Position.Length);
            Assert.False(TrapExecuted, "a withheld getter ran");
        }

        [Theory]
        [MemberData(nameof(WithheldNames))]
        public void WithheldNameIsNotFoundOnTheMemberTier(string name)
        {
            TrapExecuted = false;
            string template = "v=@(" + name + ")";
            AssertNotFound(Compile(template, typeof(Vault), ExpressionMode.MemberPathsOnly), 3, name.Length + 2);
        }

        [Theory]
        [MemberData(nameof(WithheldNames))]
        public void WithheldNameIsNotFoundOnTheNativeTier(string name)
        {
            TrapExecuted = false;
            var result = Compile("v=@(" + name + " + \"x\")", typeof(Vault), ExpressionMode.Native);
            Assert.False(result.Success, "expected a compile error; got success");
            var error = result.Errors.FirstOrDefault(e => e.DiagnosticId == HeddleDiagnosticIds.PropertyNotFound);
            Assert.True(error != null, "expected HED0001; got: " + result);
            Assert.True(error.Position.Length > 0, "diagnostic must carry a position");
            Assert.False(TrapExecuted, "a withheld getter ran");
        }

        [Theory]
        [MemberData(nameof(WithheldNames))]
        public void WithheldNameStaysWithheldThroughAFurtherDerivedTypeAndANestedHop(string name)
        {
            TrapExecuted = false;
            Assert.False(Compile("@(" + name + ")", typeof(VaultLeaf), ExpressionMode.Native).Success);
            Assert.False(Compile("@(Inner." + name + ")", typeof(VaultHolder), ExpressionMode.Native).Success);
            Assert.Equal(MemberPathResolutionKind.Failed,
                MemberPathResolver.TryResolve(new ExType(typeof(VaultLeaf)), new[] { name }).Kind);
            Assert.False(TrapExecuted, "a withheld getter ran");
        }

        [Fact]
        public void HiddenIndexerOverrideIsNotBoundThroughTheBaseIndexer()
        {
            TrapExecuted = false;
            var result = Compile("@(this[0])", typeof(Vault), ExpressionMode.Native);
            Assert.False(result.Success, "expected a compile error; got success");
            Assert.Contains(result.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.IndexerNotFound);
            Assert.False(TrapExecuted, "a withheld getter ran");
        }

        /// <summary>Only a real indexer of the type can withhold the one it hides. Pins the regression where
        /// any parameterized property of a matching shape did — so a derived type's explicit interface
        /// indexer, private by construction, blocked the inherited public indexer with <c>HED1010</c>.</summary>
        [Fact]
        public void ExplicitInterfaceIndexersDoNotWithholdTheInheritedPublicIndexer()
        {
            TrapExecuted = false;
            HeddleTemplate.Configure(typeof(HiddenMemberShadowingTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text };
            var template = new HeddleTemplate("@(this[0])|@(this[Count - 1])",
                new CompileContext(options, typeof(Shelf)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Equal("first|last", template.Generate(new Shelf { "first", "last" }));
            Assert.False(TrapExecuted, "an explicit interface indexer ran");
        }

        [Fact]
        public void HiddenOverrideOfARenamedIndexerStillWithholdsIt()
        {
            TrapExecuted = false;
            var result = Compile("@(this[0])", typeof(RenamedHidden), ExpressionMode.Native);
            Assert.False(result.Success, "expected a compile error; got success");
            Assert.Contains(result.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.IndexerNotFound);
            Assert.True(Compile("@(this[0])", typeof(RenamedBase), ExpressionMode.Native).Success);
            Assert.False(TrapExecuted, "a withheld getter ran");
        }

        [Fact]
        public void VisibleAndReopenedMembersStillBind()
        {
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text };
            var template = new HeddleTemplate("@(Open)|@(Reopened)",
                new CompileContext(options, typeof(Vault)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Equal("base-open|derived-reopened", template.Generate(new Vault()));
        }

        [Fact]
        public void TheBaseTypeItselfStillExposesWhatItDeclares()
        {
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text };
            var template = new HeddleTemplate("@(Overridden)|@(Shadowed)",
                new CompileContext(options, typeof(VaultBase)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.Equal("base-overridden|base-shadowed", template.Generate(new VaultBase()));
        }

        [Fact]
        public void VisiblePropertiesOfferNoWithheldName()
        {
            var names = MemberPathResolver.GetVisibleProperties(typeof(VaultLeaf)).Select(p => p.Name).ToList();
            foreach (var row in WithheldNames())
                Assert.DoesNotContain((string)row[0], names);
            Assert.Contains("Open", names);
            Assert.Contains("Reopened", names);
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void APropNamedLikeAWithheldMemberRaisesNoShadowWarningAndNoException()
        {
            HeddleTemplate.Configure(typeof(HiddenMemberShadowingTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text };
            var template = new HeddleTemplate(
                "@%<card(Overridden: string = \"p\", Shadowed: string = \"q\")>{{@(Overridden)@(Shadowed)}} :: " +
                typeof(Vault).FullName.Replace('+', '.') + " %@@card(this)",
                new CompileContext(options, typeof(Vault)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            Assert.DoesNotContain(template.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.PropShadowsModelMember);
            Assert.Equal("pq", template.Generate(new Vault()));
        }

        /// <summary><c>[Hidden]</c> is recognized by its full metadata name, not by <c>Type</c> identity. A model
        /// assembly loaded into its own load context can bind its attribute to a second copy of the engine —
        /// the language service's collectible model context did exactly that — and an identity match then sees
        /// no attribute at all, so the member is silently exposed. Matching the name fails closed.</summary>
        [Fact]
        public void HiddenDeclaredAgainstAnotherCopyOfTheAttributeTypeStillHides()
        {
            const string source =
                "namespace Heddle.Attributes { [System.AttributeUsage(System.AttributeTargets.Property)] " +
                "public sealed class HiddenAttribute : System.Attribute { } } " +
                "namespace OtherCopy { public class Model { " +
                "[Heddle.Attributes.Hidden] public string Secret { get; set; } public string Open { get; set; } } }";
            var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
                "OtherCopy_" + Guid.NewGuid().ToString("N"),
                new[] { Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source) },
                new[] { Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                    Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            using var image = new System.IO.MemoryStream();
            var emit = compilation.Emit(image);
            Assert.True(emit.Success, string.Join("; ", emit.Diagnostics));
            var model = Assembly.Load(image.ToArray()).GetType("OtherCopy.Model");
            Assert.NotSame(typeof(HiddenAttribute), model.GetProperty("Secret").GetCustomAttributes(false)[0].GetType());

            Assert.Equal(MemberPathResolutionKind.Failed,
                MemberPathResolver.TryResolve(new ExType(model), new[] { "Secret" }).Kind);
            Assert.Equal(MemberPathResolutionKind.Resolved,
                MemberPathResolver.TryResolve(new ExType(model), new[] { "Open" }).Kind);
            var names = MemberPathResolver.GetVisibleProperties(model).Select(p => p.Name).ToList();
            Assert.DoesNotContain("Secret", names);
            Assert.Contains("Open", names);
        }

        [Theory]
        [MemberData(nameof(WithheldNames))]
        public void TheBuildRecordingCompileRefusesAWithheldNameToo(string name)
        {
            TrapExecuted = false;
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text };
            var template = CompiledFormHarness.BuildRecording("v=@(" + name + ")", options,
                new ExType(typeof(Vault)), out _);
            AssertNotFound(template.CompileResult, 3, name.Length + 2);
        }
    }
}
