using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 4 WI2 — <c>TryCompilation</c> full parity (P4-Q1): the dry run now executes the same
    /// finalization (<c>CompleteInit</c> over delayed subtemplates) and <c>FullCSharp</c> Roslyn passes a real
    /// <c>Compile</c> runs, then discards the artifact, so a green dry run predicts a green real compile.
    /// The dry run is isolated onto a fresh probe context, so the caller's <see cref="CompileContext"/> is
    /// never mutated, and the probe artifact is disposed on every non-store exit including exceptions.
    /// </summary>
    public class TryCompilationParityTests
    {
        public class TypedModel { public string Name { get; set; } }

        private static TemplateOptions FileOptions(string templateName)
        {
            return new TemplateOptions(templateName)
            {
                RootPath = "TestTemplate",
                FileNamePostfix = ".heddle"
            };
        }

        /// <summary>
        /// A <c>FullCSharp</c> template whose embedded C# references an undefined symbol now fails the dry
        /// run with the Roslyn error — matching what a real <c>Compile</c> of the same source reports.
        /// </summary>
        [Fact]
        public void TryCompilationSurfacesFullCSharpRoslynError()
        {
            HeddleTemplate.Configure(typeof(TryCompilationParityTests).GetTypeInfo().Assembly);
            const string document = "@(@ undefined_symbol_xyz )";
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

            using var probeHost = new HeddleTemplate();
            var dryRun = probeHost.TryCompilation(document, options);
            Assert.False(dryRun.Success, "the dry run must surface the Roslyn error");
            Assert.Contains(dryRun.ErrorList, e => e.Error != null && e.Error.Contains("undefined_symbol_xyz"));

            using var real = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp }));
            Assert.False(real.CompileResult.Success, "parity baseline: the real compile fails too");
            Assert.Contains(real.CompileResult.ErrorList,
                e => e.Error != null && e.Error.Contains("undefined_symbol_xyz"));
        }

        /// <summary>
        /// A delayed subtemplate that fails finalization (a <c>@partial</c> naming a missing template file,
        /// which errors in <c>CompleteInit</c>) now fails the dry run with the finalization error.
        /// </summary>
        [Fact]
        public void TryCompilationSurfacesDelayedSubtemplateFinalizationError()
        {
            HeddleTemplate.Configure(typeof(TryCompilationParityTests).GetTypeInfo().Assembly);
            const string document = "@partial(){{trycompile-parity-missing}}";

            using var probeHost = new HeddleTemplate();
            var dryRun = probeHost.TryCompilation(document, FileOptions("inline"));
            Assert.False(dryRun.Success, "the dry run must surface the delayed-subtemplate finalization error");
            Assert.Contains(dryRun.ErrorList, e => e.Error != null && e.Error.Contains("File not found"));

            using var real = new HeddleTemplate(document, new CompileContext(FileOptions("inline")));
            Assert.False(real.CompileResult.Success, "parity baseline: the real compile fails too");
        }

        /// <summary>
        /// The positive contract: a valid <c>FullCSharp</c> template passes the dry run <b>and</b> a real
        /// compile of the same source succeeds and renders — no green-dry-run-then-red-compile pair.
        /// </summary>
        [Fact]
        public void TryCompilationSuccessPredictsCompileSuccess()
        {
            HeddleTemplate.Configure(typeof(TryCompilationParityTests).GetTypeInfo().Assembly);
            const string document = "@(@ 1 + 2 )";

            using var probeHost = new HeddleTemplate();
            var dryRun = probeHost.TryCompilation(document,
                new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp });
            Assert.True(dryRun.Success, dryRun.ToString());

            using var real = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp }));
            Assert.True(real.CompileResult.Success, real.CompileResult.ToString());
            Assert.Equal("3", real.Generate(null));
        }

        /// <summary>
        /// D2 constraint 1: the dry run leaves the caller's context pristine (<c>Compiled == false</c>, no
        /// leaked errors), and a subsequent real <c>Compile</c> of the <b>same instance</b> finalizes from
        /// scratch — the delayed <c>@partial</c> renders its child content instead of being silently skipped.
        /// </summary>
        [Fact]
        public void TryCompilationDoesNotMutateCallerContext()
        {
            HeddleTemplate.Configure(typeof(TryCompilationParityTests).GetTypeInfo().Assembly);
            var context = new CompileContext(FileOptions("trycompile-parity-parent"));

            using var probeHost = new HeddleTemplate();
            var dryRun = probeHost.TryCompilation(context);
            Assert.True(dryRun.Success, dryRun.ToString());
            Assert.False(context.Compiled, "the dry run must not mark the caller's context compiled");
            Assert.Empty(context.CompileErrors);

            using var real = new HeddleTemplate(context);
            Assert.True(real.CompileResult.Success, real.CompileResult.ToString());
            Assert.Equal("A[CHILD]B", real.Generate(null));
        }

        /// <summary>
        /// D2: the probe mirrors the caller's <b>current</b> <c>ScopeType</c> and <c>OutputProfile</c> —
        /// a context mutated after construction (ScopeType diverging from RootScopeType, profile flipped)
        /// is predicted faithfully: the dry run and the real compile of the same mutated context agree.
        /// </summary>
        [Fact]
        public void TryCompilationMirrorsMutatedScopeTypeAndProfile()
        {
            HeddleTemplate.Configure(typeof(TryCompilationParityTests).GetTypeInfo().Assembly);
            var context = new CompileContext(FileOptions("trycompile-parity-typed"));
            context.ScopeType = typeof(TypedModel);       // diverges from RootScopeType (object)
            context.OutputProfile = OutputProfile.Text;   // diverges from the options default (Html)

            using var probeHost = new HeddleTemplate();
            var dryRun = probeHost.TryCompilation(context);
            Assert.True(dryRun.Success, dryRun.ToString());

            using var real = new HeddleTemplate(context);
            Assert.True(real.CompileResult.Success, real.CompileResult.ToString());
            Assert.Equal("ok", real.Generate(new TypedModel { Name = "ok" }));
        }

        /// <summary>
        /// D2 constraint 2: when finalization throws out of <c>compileScope.Compile()</c>, the dry run
        /// returns an error result and the probe artifact is still disposed (the <c>finally</c> ran) —
        /// witnessed by the extension the discarded <c>RuntimeDocument</c> owns being disposed.
        /// </summary>
        [Fact]
        public void TryCompilationOnThrowingFinalizationLeaksNoArtifact()
        {
            HeddleTemplate.Configure(typeof(TryCompilationParityTests).GetTypeInfo().Assembly);
            ThrowingDelayedInitExtension.Register();
            ThrowingDelayedInitExtension.Reset();

            using var probeHost = new HeddleTemplate();
            var dryRun = probeHost.TryCompilation("@p4throwdelayed()", new TemplateOptions());

            Assert.False(dryRun.Success, "the throwing finalization must surface as an error result");
            Assert.Contains(dryRun.ErrorList,
                e => e.Error != null && e.Error.Contains("finalization failure (P4 test)"));
            Assert.Equal(1, ThrowingDelayedInitExtension.DisposeCount);
        }
    }

    /// <summary>
    /// A test extension that registers a delayed compile in <c>InitStart</c> and throws from
    /// <c>CompleteInit</c> — forcing <c>compileScope.Compile()</c> to throw during the dry run — while
    /// counting <c>Dispose</c> calls as the artifact-leak witness (phase 4 WI2, D2 constraint 2).
    /// </summary>
    [ExtensionName("p4throwdelayed")]
    public class ThrowingDelayedInitExtension : AbstractExtension
    {
        private static readonly object Gate = new object();
        private static bool _registered;
        private static int _disposeCount;

        public static int DisposeCount => Volatile.Read(ref _disposeCount);

        public static void Reset() => Volatile.Write(ref _disposeCount, 0);

        public static void Register()
        {
            lock (Gate)
            {
                if (_registered)
                    return;
                if (!TemplateFactory.Exists("p4throwdelayed"))
                    TemplateFactory.AddExtensions(new[]
                        { new ExtensionType("p4throwdelayed", typeof(ThrowingDelayedInitExtension), false) });
                _registered = true;
            }
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.CompileScope == null)
                throw new ArgumentNullException(nameof(initContext.CompileScope));
            initContext.CompileScope.CompileContext.AddDelayedCompileTemplate(
                new CompileScope(new CompileContext(initContext.CompileScope.CompileContext, dataType),
                    initContext.CompileScope.CSharpContext), initContext.ParseContext, this);
            return typeof(string);
        }

        public override void CompleteInit(CompileScope newScope, ParseContext parseContext)
        {
            throw new InvalidOperationException("finalization failure (P4 test)");
        }

        public override object ProcessData(in Scope scope) => string.Empty;

        public override void RenderData(in Scope scope)
        {
        }

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _disposeCount);
            base.Dispose(disposing);
        }
    }
}
