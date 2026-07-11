using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Helpers;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// WI6 (§8.2) — universality of the <see cref="BranchRoleAttribute"/> contract. A custom
    /// <c>@begin</c>/<c>@between</c>/<c>@finish</c> trio, declared with only the public attributes and the public
    /// <see cref="Scope.Publish"/>/<see cref="Scope.TryRead"/> channel (never the engine's internal branch
    /// conveniences), gets identical set semantics to the built-in <c>@if</c>/<c>@elif</c>/<c>@else</c> family:
    /// adjacency stripping (R7), orphan diagnostics (R2/R4), terminal-optional (R5), terminal-condition (R6),
    /// definition shadowing (R8), cross-family interoperation, non-branch interposition (R9), inherited roles, and
    /// the roleless-participant regression (R10/I6). Each case asserts byte-parity with the equivalent built-in
    /// template where applicable.
    /// </summary>
    public class BranchRoleUniversalityTests
    {
        public class M
        {
            public bool A { get; set; }
            public bool B { get; set; }
            public string Marker { get; set; }
        }

        // --- Shared, public-API-only branch semantics for the custom trio. Nested types may read these private
        //     helpers of the enclosing test class. ---

        private static bool Truthy(object value) => value != null && (!(value is bool b) || b);

        /// <summary>Opportunistic publish through the public channel (R11): an opener carries no
        /// <c>[ScopeChannel]</c>, so a set with no continuation/terminal sibling provisions no frame and
        /// <see cref="Scope.Publish"/> throws — swallowed, exactly mirroring the built-in openers' frameless
        /// no-op (no reader can exist without a participant sibling).</summary>
        private static void TryPublish(in Scope scope, bool satisfied)
        {
            try
            {
                scope.Publish(BranchState.ReservedKey, new BranchState(satisfied));
            }
            catch (InvalidOperationException)
            {
                // No local frame => no possible reader; nothing to publish. Opportunistic, per R11.
            }
        }

        private static bool ReadSatisfied(in Scope scope, out bool present)
        {
            if (scope.TryRead(BranchState.ReservedKey, out var value) && value is BranchState state)
            {
                present = true;
                return state.Satisfied;
            }

            present = false;
            return false;
        }

        // --- The custom trio (canonical shapes, public API only). ---

        /// <summary>Opener — canonical <c>InitStart</c>; publishes the initial <see cref="BranchState"/>. No
        /// <c>[ScopeChannel]</c> (R11).</summary>
        [ExtensionName("begin")]
        [BranchRole(BranchRole.Opener)]
        public class BeginExtension : AbstractExtension
        {
            public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
            {
                return base.InitStart(initContext, parent, chainedType, null);
            }

            public override object ProcessData(in Scope scope)
            {
                bool satisfied = Truthy(scope.ModelData);
                TryPublish(scope, satisfied);
                if (satisfied)
                    return GetInnerResult(scope.Parent());
                return string.Empty;
            }

            public override void RenderData(in Scope scope)
            {
                bool satisfied = Truthy(scope.ModelData);
                TryPublish(scope, satisfied);
                if (satisfied)
                    RenderInnerResult(scope.Parent());
            }
        }

        /// <summary>Continuation — elif-shaped: reads the channel, republishes an updated state, may render.</summary>
        [ExtensionName("between")]
        [ScopeChannel]
        [BranchRole(BranchRole.Continuation)]
        public class BetweenExtension : AbstractExtension
        {
            public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
            {
                return base.InitStart(initContext, parent, chainedType, null);
            }

            public override object ProcessData(in Scope scope)
            {
                if (ReadSatisfied(scope, out _))
                    return string.Empty; // an earlier branch already fired — leave the state unchanged.

                bool truthy = Truthy(scope.ModelData);
                scope.Publish(BranchState.ReservedKey, new BranchState(truthy));
                if (truthy)
                    return GetInnerResult(scope.Parent());
                return string.Empty;
            }

            public override void RenderData(in Scope scope)
            {
                if (ReadSatisfied(scope, out _))
                    return;

                bool truthy = Truthy(scope.ModelData);
                scope.Publish(BranchState.ReservedKey, new BranchState(truthy));
                if (truthy)
                    RenderInnerResult(scope.Parent());
            }
        }

        /// <summary>Terminal — else-shaped: reads the channel, renders when unsatisfied, and throws when no set is
        /// open. The public channel has no remove; the set is closed by consumption (no following reader exists in
        /// a well-formed set).</summary>
        [ExtensionName("finish")]
        [ScopeChannel]
        [BranchRole(BranchRole.Terminal)]
        public class FinishExtension : AbstractExtension
        {
            internal const string NoOpenerMessage =
                "'@finish' is a branch terminal with no matching opener in this scope.";

            public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
            {
                return base.InitStart(initContext, parent, chainedType, null);
            }

            public override object ProcessData(in Scope scope)
            {
                bool satisfied = ReadSatisfied(scope, out var present);
                if (!present)
                    throw new TemplateProcessingException(NoOpenerMessage);
                if (satisfied)
                    return string.Empty;
                return GetInnerResult(scope.Parent());
            }

            public override void RenderData(in Scope scope)
            {
                bool satisfied = ReadSatisfied(scope, out var present);
                if (!present)
                    throw new TemplateProcessingException(NoOpenerMessage);
                if (satisfied)
                    return;
                RenderInnerResult(scope.Parent());
            }
        }

        /// <summary>Derived Opener with a new name and no re-attribution — must stay an Opener via
        /// <c>Inherited = true</c> (case 10).</summary>
        [ExtensionName("begin2")]
        public class Begin2Extension : BeginExtension
        {
        }

        /// <summary>A roleless <c>[ScopeChannel]</c> participant that drives a set (R10/I6). Same shape as the
        /// documented <c>SatisfyExtension</c>.</summary>
        [ExtensionName("satisfy2")]
        [ScopeChannel]
        public class Satisfy2Extension : AbstractExtension
        {
            public override object ProcessData(in Scope scope)
            {
                scope.Publish(BranchState.ReservedKey, new BranchState(true));
                return string.Empty;
            }

            public override void RenderData(in Scope scope)
            {
                scope.Publish(BranchState.ReservedKey, new BranchState(true));
            }
        }

        // --- Registration (mirrors ScopeChannelDocExampleTests: gate + Configure + AddExtensions-if-absent). ---

        private static readonly object Gate = new object();
        private static bool _registered;

        private static void EnsureRegistered()
        {
            HeddleTemplate.Configure(typeof(BranchRoleUniversalityTests).GetTypeInfo().Assembly);
            lock (Gate)
            {
                if (_registered)
                    return;
                Add("begin", typeof(BeginExtension));
                Add("between", typeof(BetweenExtension));
                Add("finish", typeof(FinishExtension));
                Add("begin2", typeof(Begin2Extension));
                Add("satisfy2", typeof(Satisfy2Extension));
                _registered = true;
            }
        }

        private static void Add(string name, Type type)
        {
            if (!TemplateFactory.Exists(name))
                TemplateFactory.AddExtensions(new[] { new ExtensionType(name, type, false) });
        }

        private static HeddleTemplate Compile(string template, TemplateOptions options = null)
        {
            EnsureRegistered();
            options ??= new TemplateOptions();
            return new HeddleTemplate(template, new CompileContext(options, typeof(M)));
        }

        private static IReadOnlyList<HeddleCompileWarning> Warnings(HeddleTemplate t, string id) =>
            t.Context.CompileWarnings.Where(w => w.DiagnosticId == id).ToList();

        private static bool NoBranchDiagnostics(HeddleTemplate t) =>
            !t.Context.CompileWarnings.Any(w => w.DiagnosticId != null && w.DiagnosticId.StartsWith("HED3", StringComparison.Ordinal)) &&
            !t.CompileResult.ErrorList.Any(e => e.DiagnosticId != null && e.DiagnosticId.StartsWith("HED3", StringComparison.Ordinal));

        // ================================================================================================
        // Case 1 — full truth table, parity with @if/@elif/@else.
        // ================================================================================================
        [Fact]
        public void Case01_FullSetTruthTableMatchesIfElifElse()
        {
            const string custom = "@begin(A){{a}}@between(B){{b}}@finish(){{c}}";
            const string builtin = "@if(A){{a}}@elif(B){{b}}@else(){{c}}";
            var ct = Compile(custom);
            var bt = Compile(builtin);
            Assert.True(ct.CompileResult.Success, ct.CompileResult.ToString());
            Assert.True(NoBranchDiagnostics(ct));

            foreach (var (model, expected) in new[]
            {
                (new M { A = true, B = true }, "a"),
                (new M { A = true, B = false }, "a"),
                (new M { A = false, B = true }, "b"),
                (new M { A = false, B = false }, "c"),
            })
            {
                var got = ct.Generate(model);
                Assert.Equal(expected, got);
                Assert.Equal(bt.Generate(model), got); // parity with the built-in family
            }
        }

        // ================================================================================================
        // Case 2 — terminal-optional (R5): a set may end on a continuation.
        // ================================================================================================
        [Fact]
        public void Case02_TerminalOptional()
        {
            var ct = Compile("@begin(false){{a}}@between(true){{b}}");
            Assert.True(ct.CompileResult.Success, ct.CompileResult.ToString());
            Assert.True(NoBranchDiagnostics(ct));
            Assert.Equal("b", ct.Generate(new M()));

            var bt = Compile("@if(false){{a}}@elif(true){{b}}");
            Assert.Equal(bt.Generate(new M()), ct.Generate(new M()));
        }

        // ================================================================================================
        // Case 3 — adjacency strip + HED3001 (R7): non-whitespace warns, whitespace is silent.
        // ================================================================================================
        [Fact]
        public void Case03_AdjacencyStripAndHed3001()
        {
            // Non-whitespace gap: stripped WITH exactly one HED3001, positioned at the @finish block.
            const string stray = "@begin(true){{a}} STRAY @finish(){{b}}";
            var st = Compile(stray);
            Assert.True(st.CompileResult.Success, st.CompileResult.ToString());
            var w = Assert.Single(Warnings(st, HeddleDiagnosticIds.BranchTextStripped));
            Assert.Equal("Text between branch blocks is never rendered.", w.Error);
            int at = stray.IndexOf("@finish", StringComparison.Ordinal);
            Assert.InRange(w.Position.StartIndex, at, at + "@finish".Length + 1);
            Assert.Equal("a", st.Generate(new M()));
            Assert.Equal(Compile("@if(true){{a}} STRAY @else(){{b}}").Generate(new M()), st.Generate(new M()));

            // Whitespace gap: stripped SILENTLY.
            var wt = Compile("@begin(true){{a}}\n@finish(){{b}}");
            Assert.True(wt.CompileResult.Success, wt.CompileResult.ToString());
            Assert.Empty(Warnings(wt, HeddleDiagnosticIds.BranchTextStripped));
            Assert.Equal("a", wt.Generate(new M()));
        }

        // ================================================================================================
        // Case 4 — orphan continuation (R2): HED3002 with new text, behaves as an opener.
        // ================================================================================================
        [Fact]
        public void Case04_OrphanContinuationWarnsHed3002AndActsAsOpener()
        {
            const string template = "@between(A){{b}}";
            var t = Compile(template);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var w = Assert.Single(Warnings(t, HeddleDiagnosticIds.ElifWithoutIf));
            Assert.Equal(HeddleDiagnosticIds.ElifWithoutIf, w.DiagnosticId);
            Assert.Equal(
                "'@between' is a branch continuation with no preceding opener in this scope — it starts a new set.",
                w.Error);
            int at = template.IndexOf("@between", StringComparison.Ordinal);
            Assert.InRange(w.Position.StartIndex, at, at + "@between".Length + 1);

            // Behaves like an opener at runtime (parity with an orphan @elif).
            Assert.Equal("b", t.Generate(new M { A = true }));
            Assert.Equal("", t.Generate(new M { A = false }));
            Assert.Equal(Compile("@elif(A){{b}}").Generate(new M { A = true }), t.Generate(new M { A = true }));
        }

        // ================================================================================================
        // Case 5 — orphan terminal (R4): HED3003 compile error; render path throws.
        // ================================================================================================
        [Fact]
        public void Case05_OrphanTerminalIsHed3003ErrorAndRenderThrows()
        {
            const string template = "@finish(){{b}}";
            var t = Compile(template);
            Assert.False(t.CompileResult.Success);
            var e = Assert.Single(t.CompileResult.ErrorList, x => x.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
            Assert.Equal("'@finish' is a branch terminal with no matching opener in this scope.", e.Error);
            int at = template.IndexOf("@finish", StringComparison.Ordinal);
            Assert.InRange(e.Position.StartIndex, at, at + "@finish".Length + 1);

            // Render path (a chained finish is invisible to the static scan): the extension's own throw.
            var chained = new HeddleTemplate("@out():finish(){{b}}", new CompileContext(typeof(M)));
            Assert.True(chained.CompileResult.Success, chained.CompileResult.ToString());
            var ex = Assert.Throws<TemplateProcessingException>(() => chained.Generate(new M()));
            Assert.Equal(FinishExtension.NoOpenerMessage, ex.Message);
        }

        // ================================================================================================
        // Case 6 — terminal condition (R6): HED3004 warning; parameter ignored.
        // ================================================================================================
        [Fact]
        public void Case06_TerminalConditionWarnsHed3004()
        {
            const string template = "@begin(A){{a}}@finish(B){{b}}";
            var t = Compile(template);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var w = Assert.Single(Warnings(t, HeddleDiagnosticIds.ElseConditionIgnored));
            Assert.Equal("A branch terminal takes no condition — its parameter is ignored.", w.Error);
            int at = template.IndexOf("@finish", StringComparison.Ordinal);
            Assert.InRange(w.Position.StartIndex, at, at + "@finish".Length + 1);

            Assert.Equal("a", t.Generate(new M { A = true, B = true }));   // begin satisfied -> a; finish ignored
            Assert.Equal("b", t.Generate(new M { A = false, B = true }));  // begin unsatisfied -> finish renders b
            Assert.Equal(Compile("@if(A){{a}}@else(B){{b}}").Generate(new M { A = false, B = true }),
                t.Generate(new M { A = false, B = true }));
        }

        // ================================================================================================
        // Case 7 — definition shadowing (R8): a definition named 'between' is not a branch.
        // ================================================================================================
        [Fact]
        public void Case07_DefinitionShadowingSuppressesRole()
        {
            var t = Compile("@%<between>{{DEF}}%@@between()");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Empty(Warnings(t, HeddleDiagnosticIds.ElifWithoutIf)); // no orphan-continuation effect
            Assert.DoesNotContain(t.CompileResult.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.ElifWithoutIf);
            Assert.Equal("DEF", t.Generate(new M()));
        }

        // ================================================================================================
        // Case 8 — mixed set (cross-family): @if / @between / @else interoperate in one set.
        // ================================================================================================
        [Fact]
        public void Case08_MixedCrossFamilySet()
        {
            const string mixed = "@if(A){{x}}@between(B){{y}}@else(){{z}}";
            const string builtin = "@if(A){{x}}@elif(B){{y}}@else(){{z}}";
            var mt = Compile(mixed);
            var bt = Compile(builtin);
            Assert.True(mt.CompileResult.Success, mt.CompileResult.ToString());
            Assert.True(NoBranchDiagnostics(mt));

            foreach (var (model, expected) in new[]
            {
                (new M { A = true, B = true }, "x"),
                (new M { A = false, B = true }, "y"),
                (new M { A = false, B = false }, "z"),
            })
            {
                Assert.Equal(expected, mt.Generate(model));
                Assert.Equal(bt.Generate(model), mt.Generate(model));
            }
        }

        // ================================================================================================
        // Case 9 — non-branch interposer (R9): strip adjacency ends, terminal still binds.
        // ================================================================================================
        [Fact]
        public void Case09_NonBranchInterposerLeavesSetOpen()
        {
            const string custom = "@begin(false){{a}} @raw(Marker) @finish(){{b}}";
            const string builtin = "@if(false){{a}} @raw(Marker) @else(){{b}}";
            var ct = Compile(custom);
            var bt = Compile(builtin);
            Assert.True(ct.CompileResult.Success, ct.CompileResult.ToString());
            // The interposer splits stripping only (Other block); the gaps render, no HED3001.
            Assert.Empty(Warnings(ct, HeddleDiagnosticIds.BranchTextStripped));

            var model = new M { A = false, B = false, Marker = "M" };
            var got = ct.Generate(model);
            Assert.Contains("M", got);
            Assert.Contains("b", got);           // the terminal still bound to the open set
            Assert.Equal(bt.Generate(model), got);
        }

        // ================================================================================================
        // Case 10 — derived inheritance: begin2 : BeginExtension, no re-attribution, still an Opener.
        // ================================================================================================
        [Fact]
        public void Case10_DerivedExtensionInheritsOpenerRole()
        {
            const string custom = "@begin2(A){{a}}@between(B){{b}}@finish(){{c}}";
            const string builtin = "@if(A){{a}}@elif(B){{b}}@else(){{c}}";
            var ct = Compile(custom);
            var bt = Compile(builtin);
            Assert.True(ct.CompileResult.Success, ct.CompileResult.ToString());
            // begin2 classifies as an Opener (inherited), so @between is not orphaned.
            Assert.True(NoBranchDiagnostics(ct));

            foreach (var model in new[]
            {
                new M { A = true, B = false },
                new M { A = false, B = true },
                new M { A = false, B = false },
            })
            {
                Assert.Equal(bt.Generate(model), ct.Generate(model));
            }
        }

        // ================================================================================================
        // Case 11 — roleless participant regression (R10/I6): a [ScopeChannel] publisher suppresses the
        // orphan error and satisfies the set so the following terminal renders nothing.
        // ================================================================================================
        [Fact]
        public void Case11_RolelessParticipantSuppressesOrphanTerminal()
        {
            var t = Compile("@satisfy2()@finish(){{fallback}}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.CompileResult.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
            Assert.Equal("", t.Generate(new M()));
        }

        // ================================================================================================
        // Case 12 — classification follows the registered TYPE's [BranchRole], not the literal directive name
        // (D-ROLE-2). This pins the intended behaviour behind the adversary's F2 note: the old engine keyed the
        // five built-in names in a hardcoded switch, so replacing (e.g.) "else" with a roleless extension via
        // [ExtensionReplace] was still forced to Terminal by name; the role-based classifier instead reads the
        // role off whatever type is registered, so a roleless replacement is (correctly) no longer a branch and
        // a role-carrying replacement keeps the semantics. `Classify` resolves the type by name and calls
        // `GetBranchRole()`, so pinning that helper's type-drivenness pins the classification's type-drivenness.
        // (A test that actually rebinds a built-in name is deliberately omitted: TemplateFactory's registry is
        // process-static, and mutating "else" would corrupt every other branch test running in parallel.)
        // ================================================================================================
        [Fact]
        public void Case12_RoleIsResolvedFromTypeNotName()
        {
            // Built-in branch types report their declared role regardless of the name they were reached by.
            Assert.Equal(BranchRole.Opener, typeof(Heddle.Extensions.IfExtension).GetBranchRole());
            Assert.Equal(BranchRole.Continuation, typeof(Heddle.Extensions.ElifExtension).GetBranchRole());
            Assert.Equal(BranchRole.Terminal, typeof(Heddle.Extensions.ElseExtension).GetBranchRole());

            // A roleless extension is not a branch, even if it were registered under a former branch name:
            // the classifier would see a null role and fall through to Participant/Other, not Terminal.
            Assert.Null(typeof(Satisfy2Extension).GetBranchRole());
        }
    }
}
