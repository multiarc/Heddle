using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The <c>LateBound</c> plan: a call whose target only a run-time registration supplies still precompiles,
    /// and resolves once at first render through the engine's own overload ranker.
    /// <para>Every leg pins the tier with <see cref="DifferentialHarness.ExpectPrecompiled"/> and compares against
    /// the dynamic engine compiled from the same text under the same registry. Without the tier pin these would
    /// prove nothing: the fallback path is byte-identical by design, so a test that let the template degrade would
    /// pass while measuring the tier it was written to replace.</para>
    /// </summary>
    public class LateBoundFunctionTests
    {
        private const string ModelType = "Heddle.Generator.IntegrationTests.Fixtures.LateBoundModel";

        private static string Doc(string body) => "@model(){{" + ModelType + "}}@\\\n" + body;

        private static LateBoundModel Model() => new LateBoundModel { Name = "widget", Stock = 7 };

        /// <summary>A registry with <paramref name="register"/> applied on top of the built-ins.</summary>
        private static TemplateOptions Options(Action<FunctionRegistry> register)
        {
            var registry = new FunctionRegistry();
            register(registry);
            return new TemplateOptions { Functions = registry };
        }

        private static IProcessStrategy Root(DifferentialHarness.GenResult gen, string key)
        {
            DifferentialHarness.ExpectPrecompiled(gen, key);
            var entry = DifferentialHarness.FindEntryTypeByKey(gen.Assembly, key);
            Assert.NotNull(entry);
            return (IProcessStrategy) entry
                .GetField("Root", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
        }

        /// <summary>Renders one template on both tiers under one registry, and returns the two outputs.</summary>
        private static (string precompiled, string dynamic) RenderBoth(string body,
            Action<FunctionRegistry> register)
        {
            const string key = "views/late.heddle";
            var content = Doc(body);
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

            var precompiled = PrecompiledRuntime.GenerateString(Root(gen, key), Model(), null, null,
                Options(register));

            var template = new HeddleTemplate(content,
                new CompileContext(Options(register), new ExType(typeof(LateBoundModel))));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return (precompiled, template.Generate(Model()));
        }

        /// <summary>The headline case: a host-registered delegate under a name no metadata carries. The build
        /// emits a late-bound site instead of the HED7014 marker it used to, and the bytes match the engine's.</summary>
        [Fact]
        public void AHostRegisteredDelegateResolvesLateAndRendersTheEnginesBytes()
        {
            var (precompiled, dyn) = RenderBoth("[@(hostshout(Name))]\n",
                r => r.Register("hostshout", new Func<string, string>(s => s.ToUpperInvariant() + "!")));

            Assert.Equal("[WIDGET!]\n", precompiled);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The same call written as a top-level call item rather than inside an expression — the arm
        /// where the engine dispatches through <c>HeddleCompiler</c>'s registry fallback rather than through the
        /// expression compiler.</summary>
        [Fact]
        public void ATopLevelCallOnALateBoundNameRendersTheEnginesBytes()
        {
            var (precompiled, dyn) = RenderBoth("[@hostshout(Name)]\n",
                r => r.Register("hostshout", new Func<string, string>(s => s.ToUpperInvariant() + "!")));

            Assert.Equal("[WIDGET!]\n", precompiled);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The point of routing through the engine's ranker rather than the consumer's compiler: over an overload
        /// set where the two could disagree, the SAME overload has to win. The <c>object</c> overload is a boxing
        /// conversion (rank 2) and the <c>long</c> one a widening conversion (rank 1) for an <c>int</c> argument,
        /// so Heddle's flat Pareto rank takes <c>long</c> — and says so in the rendered bytes.
        /// </summary>
        [Fact]
        public void AnOverloadSetIsRankedByTheEnginesOwnRankerAndTheSameOverloadWins()
        {
            var (precompiled, dyn) = RenderBoth("[@(hostpick(Stock))]\n", Overloads);

            Assert.Equal("[long:7]\n", precompiled);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The string arm of the same overload set, proving the winner tracks the argument rather than
        /// a fixed choice.</summary>
        [Fact]
        public void TheRankerPicksTheExactOverloadForAStringArgument()
        {
            var (precompiled, dyn) = RenderBoth("[@(hostpick(Name))]\n", Overloads);

            Assert.Equal("[string:widget]\n", precompiled);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The untyped <c>null</c> literal. Inference cannot type it, so the site's parameter is spelled
        /// <see cref="object"/> and the argument's position travels in the mask instead — and the mask is what
        /// makes this bind at all: the ranker converts the null literal to a <c>string</c> parameter, where a
        /// genuinely <c>object</c>-typed argument would be <c>HED1012</c>. So a site that merely typed the
        /// literal <c>object</c> and dropped the flag would fail this, not merely rank differently.
        /// </summary>
        [Fact]
        public void ANullLiteralArgumentRanksAsTheEngineRanksIt()
        {
            var (precompiled, dyn) = RenderBoth("[@(hostnull(null))]\n",
                r => r.Register("hostnull", new Func<string, string>(s => "s:" + (s ?? "none"))));

            Assert.Equal("[s:none]\n", precompiled);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The other half of the same fact: where the engine's ranker finds the null literal
        /// convertible to more than one candidate, both tiers refuse the call rather than one of them picking.
        /// The reference and object overloads of <see cref="Overloads"/> tie on the flat Pareto front.</summary>
        [Fact]
        public void ANullLiteralAcrossTiedOverloadsIsAmbiguousOnBothTiers()
        {
            const string key = "views/late-null-ambiguous.heddle";
            var content = Doc("[@(hostpick(null))]\n");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var options = Options(Overloads);

            var fault = Assert.Throws<TemplateCompileException>(
                () => PrecompiledRuntime.GenerateString(Root(gen, key), Model(), null, null, options));

            var template = new HeddleTemplate(content,
                new CompileContext(options, new ExType(typeof(LateBoundModel))));
            Assert.False(template.CompileResult.Success);
            Assert.Equal(template.CompileResult.ErrorList.Single().Error, Assert.Single(fault.Errors).Error);
        }

        private static void Overloads(FunctionRegistry r)
        {
            r.Register("hostpick", new Func<long, string>(v => "long:" + v));
            r.Register("hostpick", new Func<object, string>(v => "object:" + v));
            r.Register("hostpick", new Func<string, string>(v => "string:" + (v ?? "none")));
        }

        /// <summary>A two-argument call, so the mixed-arity generic site is exercised rather than only the
        /// one-argument shape every other leg uses.</summary>
        [Fact]
        public void ATwoArgumentLateBoundCallRendersTheEnginesBytes()
        {
            var (precompiled, dyn) = RenderBoth("[@(hostjoin(Name, Stock))]\n",
                r => r.Register("hostjoin", new Func<string, int, string>((s, i) => s + "#" + i)));

            Assert.Equal("[widget#7]\n", precompiled);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>
        /// The failure half of byte parity: a name registered nowhere. The engine refuses the template at compile
        /// with a positioned <c>HED1001</c> and every <c>Generate</c> throws
        /// <see cref="TemplateCompileException"/>; the late-bound site reproduces that error — same id, same
        /// sentence, same position — rather than rendering something the engine never would.
        /// </summary>
        [Fact]
        public void AnUnregisteredNameReproducesTheEnginesOwnCompileFailure()
        {
            const string key = "views/late-missing.heddle";
            var content = Doc("[@(hostnosuchfn(Name))]\n");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var root = Root(gen, key);

            var precompiledFault = Assert.Throws<TemplateCompileException>(
                () => PrecompiledRuntime.GenerateString(root, Model(), null, null, new TemplateOptions()));

            var template = new HeddleTemplate(content,
                new CompileContext(new TemplateOptions(), new ExType(typeof(LateBoundModel))));
            Assert.False(template.CompileResult.Success);
            var engineError = template.CompileResult.ErrorList.Single();

            var lateError = Assert.Single(precompiledFault.Errors);
            Assert.Equal(engineError.DiagnosticId, lateError.DiagnosticId);
            Assert.Equal(engineError.Error, lateError.Error);
            Assert.Equal(engineError.Position.StartIndex, lateError.Position.StartIndex);
            Assert.Equal(engineError.Position.Length, lateError.Position.Length);
        }

        /// <summary>The no-overload-accepts-these-arguments failure, which the engine reports as
        /// <c>HED1012</c> with the candidate list spelled out. The site's argument-type names come from the
        /// engine's own <c>FriendlyName</c>, so the two sentences are the same string.</summary>
        [Fact]
        public void ACallNoOverloadAcceptsReproducesTheEnginesHed1012()
        {
            const string key = "views/late-nooverload.heddle";
            var content = Doc("[@(hostneedsint(Name))]\n");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var root = Root(gen, key);

            Action<FunctionRegistry> register =
                r => r.Register("hostneedsint", new Func<int, string>(v => "i" + v));

            var precompiledFault = Assert.Throws<TemplateCompileException>(
                () => PrecompiledRuntime.GenerateString(root, Model(), null, null, Options(register)));

            var template = new HeddleTemplate(content,
                new CompileContext(Options(register), new ExType(typeof(LateBoundModel))));
            Assert.False(template.CompileResult.Success);
            var engineError = template.CompileResult.ErrorList.Single();

            var lateError = Assert.Single(precompiledFault.Errors);
            Assert.Equal("HED1012", lateError.DiagnosticId);
            Assert.Equal(engineError.DiagnosticId, lateError.DiagnosticId);
            Assert.Equal(engineError.Error, lateError.Error);
            Assert.Equal(engineError.Position.StartIndex, lateError.Position.StartIndex);
        }

        /// <summary>The ambiguous-call failure (<c>HED1013</c>): two overloads the flat Pareto front cannot
        /// separate. Reproduced with the engine's own candidate list.</summary>
        [Fact]
        public void AnAmbiguousLateBoundCallReproducesTheEnginesHed1013()
        {
            const string key = "views/late-ambiguous.heddle";
            var content = Doc("[@(hostamb(Name, Stock))]\n");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var root = Root(gen, key);

            Action<FunctionRegistry> register = r =>
            {
                r.Register("hostamb", new Func<object, int, string>((a, b) => "a"));
                r.Register("hostamb", new Func<string, long, string>((a, b) => "b"));
            };

            var precompiledFault = Assert.Throws<TemplateCompileException>(
                () => PrecompiledRuntime.GenerateString(root, Model(), null, null, Options(register)));

            var template = new HeddleTemplate(content,
                new CompileContext(Options(register), new ExType(typeof(LateBoundModel))));
            Assert.False(template.CompileResult.Success);
            var engineError = template.CompileResult.ErrorList.Single();

            var lateError = Assert.Single(precompiledFault.Errors);
            Assert.Equal("HED1013", lateError.DiagnosticId);
            Assert.Equal(engineError.Error, lateError.Error);
        }

        /// <summary>
        /// The first-use bind is racy by construction: every thread that arrives before the field is published
        /// binds. That is safe only because the bind is a pure function of the frozen registry, so the racers
        /// agree — this renders the same template on many threads at once and requires every output to be the one
        /// byte-correct answer, following the parallel-render isolation pattern the shared state rule asks for.
        /// </summary>
        [Fact]
        public void ParallelFirstRendersRacingTheBindAllProduceTheEnginesBytes()
        {
            const string key = "views/late-race.heddle";
            var content = Doc("[@(hostpick(Stock))@(hostpick(Name))]\n");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var root = Root(gen, key);
            var options = Options(Overloads);

            const int threads = 16;
            const int perThread = 40;
            var outputs = new string[threads * perThread];
            using (var gate = new Barrier(threads))
            {
                Parallel.For(0, threads, t =>
                {
                    gate.SignalAndWait();
                    for (int i = 0; i < perThread; i++)
                        outputs[t * perThread + i] =
                            PrecompiledRuntime.GenerateString(root, Model(), null, null, options);
                });
            }

            var distinct = new HashSet<string>(outputs, StringComparer.Ordinal);
            Assert.Equal(new HashSet<string>(new[] { "[long:7string:widget]\n" }, StringComparer.Ordinal), distinct);
        }

        /// <summary>
        /// A render under a different registry re-binds rather than serving the first registry's answer — the
        /// dynamic tier compiles per options, so a cache that ignored the registry would be the one place the two
        /// tiers could disagree about which function a name means.
        /// </summary>
        [Fact]
        public void ASecondRegistryRebindsInsteadOfServingTheFirstBinding()
        {
            const string key = "views/late-rebind.heddle";
            var content = Doc("[@(hostswap(Name))]\n");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            var root = Root(gen, key);

            var first = Options(r => r.Register("hostswap", new Func<string, string>(s => "one:" + s)));
            var second = Options(r => r.Register("hostswap", new Func<string, string>(s => "two:" + s)));

            Assert.Equal("[one:widget]\n", PrecompiledRuntime.GenerateString(root, Model(), null, null, first));
            Assert.Equal("[two:widget]\n", PrecompiledRuntime.GenerateString(root, Model(), null, null, second));
            Assert.Equal("[one:widget]\n", PrecompiledRuntime.GenerateString(root, Model(), null, null, first));
        }

        /// <summary>
        /// The resolver path end to end: the entry crosses the gauntlet under a request whose registry supplies
        /// the name, and the render that follows binds against THAT registry rather than the default set. Without
        /// the adapter carrying the request's options, the gauntlet would validate one registry and the site would
        /// bind another — the one configuration in which the two tiers could mean different functions by one name.
        /// </summary>
        [Fact]
        public void TheResolverPathBindsAgainstTheRequestsOwnRegistry()
        {
            const string key = "views/late-resolver.heddle";
            var content = Doc("[@(hostroute(Name))]\n");
            var corpus = new[] { (key, content) };
            var target = new DifferentialHarness.ResolverTarget(key, content, typeof(LateBoundModel),
                Model());

            var swept = DifferentialHarness.SweepViaResolver(corpus, new[] { target },
                AppContext.BaseDirectory, renderDynamicReference: true,
                configureOptions: o =>
                {
                    var registry = new FunctionRegistry();
                    registry.Register("hostroute", new Func<string, string>(s => "routed:" + s));
                    o.Functions = registry;
                });

            var result = Assert.Single(swept);
            Assert.Equal("[routed:widget]\n", result.Precompiled);
            Assert.Equal(result.Dynamic, result.Precompiled);
        }

        /// <summary>
        /// The gauntlet's half of the contract: a request whose registry does not know the name never reaches the
        /// site at all — it falls back, and the dynamic tier's own compile becomes the failure authority, which is
        /// the parity story for every other unsupported construct too.
        /// </summary>
        [Fact]
        public void AnUnregisteredNameFallsBackAtTheGauntletRatherThanFaultingAtRender()
        {
            var entry = new PrecompiledTemplateInfo(
                key: "views/late-gauntlet.heddle",
                entryPointType: typeof(LateBoundFunctionTests),
                modelType: typeof(object),
                isDynamic: true,
                contentHash: "hash",
                imports: Array.Empty<PrecompiledImport>(),
                optionsFingerprint: new PrecompiledOptionsFingerprint(OutputProfile.Html,
                    ExpressionMode.Native, trimDirectiveLines: true),
                extensionBindings: Array.Empty<PrecompiledExtensionBinding>(),
                functionBindings: new[] { new PrecompiledFunctionBinding("hostabsent", null, 0) },
                capabilities: PrecompiledCapabilities.None,
                strategy: new NullStrategy(),
                registeredName: null,
                linePathForm: PrecompiledLinePathForm.Unspecified);

            var failure = PrecompiledTemplates.Validate(entry,
                new TemplateOptions { Functions = new FunctionRegistry() });

            Assert.NotNull(failure);
            Assert.Equal(PrecompiledFallbackReason.UnsupportedFunction, failure.Value.Reason);
            Assert.Contains("hostabsent", failure.Value.Detail);
        }

        /// <summary>The same gate for the one configuration the site cannot reproduce at all: the name is live as
        /// a registered EXTENSION, whose render protocol is not a value. The request falls back before any
        /// render.</summary>
        [Fact]
        public void ANameLiveAsAnExtensionFallsBackAtTheGauntlet()
        {
            var entry = new PrecompiledTemplateInfo(
                key: "views/late-shadowed.heddle",
                entryPointType: typeof(LateBoundFunctionTests),
                modelType: typeof(object),
                isDynamic: true,
                contentHash: "hash",
                imports: Array.Empty<PrecompiledImport>(),
                optionsFingerprint: new PrecompiledOptionsFingerprint(OutputProfile.Html,
                    ExpressionMode.Native, trimDirectiveLines: true),
                extensionBindings: Array.Empty<PrecompiledExtensionBinding>(),
                // 'if' is a registered extension on every host, and never a function.
                functionBindings: new[] { new PrecompiledFunctionBinding("if", null, 0) },
                capabilities: PrecompiledCapabilities.None,
                strategy: new NullStrategy(),
                registeredName: null,
                linePathForm: PrecompiledLinePathForm.Unspecified);

            var failure = PrecompiledTemplates.Validate(entry,
                new TemplateOptions { Functions = new FunctionRegistry() });

            Assert.NotNull(failure);
            Assert.Equal(PrecompiledFallbackReason.FunctionBindingMismatch, failure.Value.Reason);
            Assert.Contains("extension", failure.Value.Detail);
        }

        private sealed class NullStrategy : IProcessStrategy
        {
            public string Execute(in Heddle.Data.Scope scope) => string.Empty;

            public void Render(in Heddle.Data.Scope scope)
            {
            }
        }

        /// <summary>The manifest carries a null-target row for every late-bound name. That row is the gauntlet's
        /// only handle on a registration the build never saw, so its absence would make the fallback checks
        /// vacuous.</summary>
        [Fact]
        public void ALateBoundNameGetsANullTargetManifestRow()
        {
            const string key = "views/late-row.heddle";
            var gen = DifferentialHarness.Generate(new[] { (key, Doc("[@(hostshout(Name))]\n")) });

            DifferentialHarness.ExpectPrecompiled(gen, key);
            Assert.Contains("new global::Heddle.Precompiled.PrecompiledFunctionBinding(\"hostshout\", null, 0)",
                gen.ManifestSource);
        }
    }
}
