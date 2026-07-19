using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// WI7 / D-ROLE-5 (§6.5): the optional runtime drift diagnostic <c>HED3005</c> warns when a branch
    /// <c>Continuation</c>/<c>Terminal</c> extension omits <c>[ScopeChannel]</c> — it can never read the branch
    /// state at render time (R11). Additive: it never fires for the compliant built-ins and does not perturb the
    /// existing HED3001–HED3004 diagnostics.
    /// </summary>
    public class BranchRoleDriftDiagnosticTests
    {
        public class M
        {
            public bool A { get; set; }
            public bool B { get; set; }
        }

        // Opener (no [ScopeChannel] by contract — not drift).
        [ExtensionName("dbegin")]
        [BranchRole(BranchRole.Opener)]
        public class DBeginExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }

        // Continuation WITHOUT [ScopeChannel] — drift (HED3005).
        [ExtensionName("dbetween")]
        [BranchRole(BranchRole.Continuation)]
        public class DBetweenExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }

        // Terminal WITHOUT [ScopeChannel] — drift (HED3005).
        [ExtensionName("dfinish")]
        [BranchRole(BranchRole.Terminal)]
        public class DFinishExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) => string.Empty;
            public override void RenderData(in Scope scope) { }
        }

        private static readonly object Gate = new object();
        private static bool _registered;

        private static void EnsureRegistered()
        {
            HeddleTemplate.Configure(typeof(BranchRoleDriftDiagnosticTests).GetTypeInfo().Assembly);
            lock (Gate)
            {
                if (_registered)
                    return;
                Add("dbegin", typeof(DBeginExtension));
                Add("dbetween", typeof(DBetweenExtension));
                Add("dfinish", typeof(DFinishExtension));
                _registered = true;
            }
        }

        private static void Add(string name, Type type)
        {
            if (!TemplateFactory.Exists(name))
                TemplateFactory.AddExtensions(new[] { new ExtensionType(name, type, false) });
        }

        private static HeddleTemplate Compile(string template)
        {
            EnsureRegistered();
            return new HeddleTemplate(template, new CompileContext(new TemplateOptions(), typeof(M)));
        }

        private static IReadOnlyList<HeddleCompileWarning> Drift(HeddleTemplate t) =>
            t.Context.CompileWarnings
                .Where(w => w.DiagnosticId == HeddleDiagnosticIds.BranchRoleMissingScopeChannel)
                .ToList();

        [Fact]
        public void Hed3005WarnsForDriftContinuation()
        {
            var t = Compile("@dbegin(A){{a}}@dbetween(B){{b}}");
            var w = Assert.Single(Drift(t));
            Assert.Equal(HeddleDiagnosticIds.BranchRoleMissingScopeChannel, w.DiagnosticId);
            Assert.Equal(
                "A branch continuation/terminal '@dbetween' does not carry [ScopeChannel]; it cannot read the branch state at render time.",
                w.Error);
        }

        [Fact]
        public void Hed3005WarnsForDriftTerminal()
        {
            var t = Compile("@dbegin(A){{a}}@dfinish(){{b}}");
            var w = Assert.Single(Drift(t));
            Assert.Contains("@dfinish", w.Error);
        }

        [Fact]
        public void Hed3005DoesNotFireForCompliantBuiltIns()
        {
            var t = Compile("@if(A){{a}}@elif(B){{b}}@else(){{c}}");
            Assert.Empty(Drift(t));
        }
    }
}
