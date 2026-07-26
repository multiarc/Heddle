using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Attributes;
using Heddle.Data;
using Heddle.Language.Members;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests.MemberConformance
{
    public interface IBaseFacet { string FromBaseInterface { get; } }

    public interface IDerivedFacet : IBaseFacet { string FromDerivedInterface { get; } }

    public class VisibilityBase
    {
        public string PublicOnBase { get; set; }
        internal string InternalOnBase { get; set; }
        protected string ProtectedOnBase { get; set; }
        public virtual string Shadowed { get; set; }
    }

    public class VisibilityModel : VisibilityBase
    {
        public string PublicHere { get; set; }
        internal string InternalHere { get; set; }
        protected internal string ProtectedInternalHere { get; set; }
        protected string ProtectedHere { get; set; }
        private string PrivateHere { get; set; }
        public string WriteOnly { set { } }

        [Hidden] public string HiddenHere { get; set; }

        [Foreign.Hidden] public string ForeignHiddenHere { get; set; }

        public static string StaticHere { get; set; }

        public new string Shadowed { get; set; }

        public string PublicWithPrivateGetter { private get; set; }

        public string Unused => PrivateHere;
    }
}

namespace Foreign
{
    /// <summary>A <b>different</b> <c>HiddenAttribute</c>. The runtime matches <c>[Hidden]</c> by real attribute
    /// type, so this one hides nothing; the generator's unqualified-name match used to hide the member, which this
    /// row guards against.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class HiddenAttribute : Attribute { }
}

namespace Heddle.Tests
{
    /// <summary>Member-visibility conformance corpus run against the reflection adapter. Rows are shared data both
    /// adapters must agree on; the generator's Roslyn adapter runs the same rows, making divergent policies structurally
    /// impossible. Every verdict pins the runtime's current observable behavior; departures from the sandbox contract
    /// are breaking-window candidates, not drift fixes.</summary>
    public class MemberVisibilityConformanceTests
    {
        public static IEnumerable<object[]> Rows()
        {
            // (member name, accessible?, why)
            yield return new object[] { "PublicHere", true, "public getter on the receiver" };
            yield return new object[] { "InternalHere", true, "internal getter on the receiver — the sandbox's public-or-internal rule" };
            yield return new object[] { "PublicOnBase", true, "public getter inherited from a base class" };
            yield return new object[] { "InternalOnBase", false, "inherited non-public members are not surfaced (runtime-normative)" };
            yield return new object[] { "ProtectedOnBase", false, "protected is outside the sandbox" };
            yield return new object[] { "ProtectedInternalHere", false, "runtime rejects protected internal; widening is a breaking-window candidate" };
            yield return new object[] { "ProtectedHere", false, "protected is outside the sandbox" };
            yield return new object[] { "PrivateHere", false, "private is outside the sandbox" };
            yield return new object[] { "WriteOnly", false, "not readable" };
            yield return new object[] { "HiddenHere", false, "[Hidden] by the real attribute type" };
            yield return new object[] { "ForeignHiddenHere", true, "a foreign *.HiddenAttribute hides nothing — full-name match" };
            yield return new object[] { "StaticHere", false, "statics are not member-path reachable (error-shape fix: positioned not-found)" };
            yield return new object[] { "Shadowed", true, "new-shadowed: the most-derived accessible one, deterministically" };
            yield return new object[] { "PublicWithPrivateGetter", false, "accessibility is the getter's, not the property's" };
            yield return new object[] { "Missing", false, "no such member" };
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void ReflectionAdapterMatchesTheConformanceRow(string member, bool accessible, string why)
        {
            var resolution = MemberPathResolver.TryResolve(new ExType(typeof(MemberConformance.VisibilityModel)),
                new[] { member });
            Assert.True(accessible == (resolution.Kind == MemberPathResolutionKind.Resolved), why);
            if (!accessible)
            {
                Assert.Equal(MemberPathResolutionKind.Failed, resolution.Kind);
                Assert.Contains("not found", resolution.FailureMessage);
            }
        }

        [Fact]
        public void NewShadowedPropertyResolvesToTheMostDerived_InsteadOfThrowingAmbiguousMatch()
        {
            // Type.GetProperty threw AmbiguousMatchException; now resolves to most-derived.
            var resolution = MemberPathResolver.TryResolve(new ExType(typeof(MemberConformance.VisibilityModel)),
                new[] { "Shadowed" });
            Assert.Equal(MemberPathResolutionKind.Resolved, resolution.Kind);
            Assert.Equal(typeof(MemberConformance.VisibilityModel),
                resolution.Properties[0].Item2.DeclaringType);
        }

        [Fact]
        public void StaticPropertyIsAPositionedNotFound_NotAnArgumentException()
        {
            // Previously threw unpositioned ArgumentException; now emits positioned diagnostic on both tiers.
            var template = new HeddleTemplate("@(StaticHere)",
                new CompileContext(new TemplateOptions(), typeof(MemberConformance.VisibilityModel)));
            Assert.False(template.CompileResult.Success);
            Assert.Contains(template.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.PropertyNotFound);
        }

        [Fact]
        public void BaseInterfaceMembersStayInvisible_TheNarrowRuntimeBehavior()
        {
            // Reflection's GetProperty never searched base interfaces; surfacing them is a breaking-window candidate.
            var self = MemberPathResolver.TryResolve(new ExType(typeof(MemberConformance.IDerivedFacet)),
                new[] { "FromDerivedInterface" });
            Assert.Equal(MemberPathResolutionKind.Resolved, self.Kind);

            var inherited = MemberPathResolver.TryResolve(new ExType(typeof(MemberConformance.IDerivedFacet)),
                new[] { "FromBaseInterface" });
            Assert.Equal(MemberPathResolutionKind.Failed, inherited.Kind);
        }

        [Fact]
        public void VisibleProperties_ApplyTheIdenticalFilter()
        {
            var visible = MemberPathResolver.GetVisibleProperties(typeof(MemberConformance.VisibilityModel))
                .Select(p => p.Name).ToList();
            foreach (var row in Rows())
            {
                var member = (string) row[0];
                bool accessible = (bool) row[1];
                if (member == "Missing")
                    continue;
                Assert.Equal(accessible, visible.Contains(member));
            }

            Assert.Equal(visible.Count, visible.Distinct(StringComparer.Ordinal).Count());
        }


        [Fact]
        public void PolicyTable()
        {
            // (access, declaredOnReceiver) → accessible. Public reaches through inheritance; internal only where reflection surfaces it.
            AssertPolicy(MemberAccess.Public, true, true);
            AssertPolicy(MemberAccess.Public, false, true);
            AssertPolicy(MemberAccess.Internal, true, true);
            AssertPolicy(MemberAccess.Internal, false, false);
            AssertPolicy(MemberAccess.ProtectedOrInternal, true, false);
            AssertPolicy(MemberAccess.Protected, true, false);
            AssertPolicy(MemberAccess.ProtectedAndInternal, true, false);
            AssertPolicy(MemberAccess.Private, true, false);
        }

        private static void AssertPolicy(MemberAccess access, bool declaredOnReceiver, bool expected)
        {
            var facts = new MemberFacts(true, access, false, false);
            Assert.Equal(expected, MemberVisibility.IsAccessible(facts, declaredOnReceiver));
        }

        [Fact]
        public void PolicyRejectsUnreadable_Hidden_AndStatic_RegardlessOfAccess()
        {
            Assert.False(MemberVisibility.IsAccessible(new MemberFacts(false, MemberAccess.Public, false, false)));
            Assert.False(MemberVisibility.IsAccessible(new MemberFacts(true, MemberAccess.Public, true, false)));
            Assert.False(MemberVisibility.IsAccessible(new MemberFacts(true, MemberAccess.Public, false, true)));
            Assert.True(MemberVisibility.IsAccessible(new MemberFacts(true, MemberAccess.Public, false, false)));
        }


        [Fact]
        public void HopFormTable()
        {
            Assert.Equal(HopForm.Direct, MemberHopRule.Form(true, true));
            Assert.Equal(HopForm.Direct, MemberHopRule.Form(true, false));
            Assert.Equal(HopForm.NullDefaultConditional, MemberHopRule.Form(false, true));
            Assert.Equal(HopForm.NullConditional, MemberHopRule.Form(false, false));
        }
    }
}
