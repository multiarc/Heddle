using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.ArgTyping;
using Heddle.Generator.IntegrationTests.Fixtures;
using Microsoft.CodeAnalysis;
using Xunit;

[assembly: Heddle.Attributes.ExportFunctions(typeof(Heddle.Generator.IntegrationTests.ArgTyping.ArgTypedExports))]

namespace Heddle.Generator.IntegrationTests.ArgTyping
{
    /// <summary>An interface no generated code in another assembly may write the name of.</summary>
    [System.Obsolete("retired", true)]
    public interface IRetired
    {
    }

    /// <summary>Implementing it needs an obsolete context of its own; warning-level is the weakest one that gives
    /// it, and it also keeps this type nameable so only the interface is out of reach.</summary>
    [System.Obsolete("implementing a retired interface needs an obsolete context")]
    public sealed class RetiredHolder : IRetired
    {
    }

    /// <summary>The nameable counterpart, so a test can tell "this parameter type" from "this call shape".</summary>
    public interface ILive
    {
    }

    public sealed class LiveHolder : ILive
    {
    }

    public sealed class HolderModel
    {
        [System.Obsolete("the property's type is only declarable in an obsolete context")]
        public RetiredHolder Retired { get; set; }

        public LiveHolder Live { get; set; }

        public int Count { get; set; }
    }

    /// <summary>
    /// Exported host functions reached with arguments whose types the shared operand descriptor does not name — a
    /// struct, an enum, a class, <c>object</c>. Reflection ranks the argument's real type; the descriptor names the
    /// numeric primitives, <c>bool</c> and <c>string</c> and nothing else.
    /// </summary>
    public static class ArgTypedExports
    {
        /// <summary>Takes anything by boxing, so an argument the descriptor cannot name still binds and must keep
        /// rendering — the neighbour that tells an applicability check apart from a refusal of such arguments.</summary>
        public static string ArgBox(object value) => "b:" + value;

        /// <summary>Two overloads, so the ranking path runs and the winner's parameter type is written as a cast.
        /// That name is <c>[Obsolete(error: true)]</c> and is CS0619 in the consumer's build.</summary>
        [System.Obsolete("a retired parameter type is only declarable in an obsolete context")]
        public static string ArgSink(IRetired value) => "retired";

        public static string ArgSink(int value) => "i" + value;

        /// <summary>The same two-overload shape over a parameter type generated code may name.</summary>
        public static string ArgLive(ILive value) => "live";

        public static string ArgLive(int value) => "n" + value;

        /// <summary>A sole <c>params</c> overload — the shape the single-candidate shortcut exists to keep on the
        /// tier even when an argument cannot be typed, since the ranker refuses untyped arguments before its
        /// expanded tier is reached.</summary>
        public static string JoinArgs(params string[] parts) => "j:" + string.Join("+", parts);
    }
}

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// What the export binder ranks an argument as. The engine's <c>NativeExpressionCompiler</c> hands
    /// <c>OverloadRank</c> the compiled expression's own <c>Type</c>; the generator handed it a shared
    /// <c>OperandKind</c> descriptor instead, which names the numeric primitives, <c>bool</c> and <c>string</c> and
    /// nothing else. So a struct, an enum, a class and <c>object</c> all arrived indistinguishable from an argument
    /// nothing had resolved — and an unresolved argument is exactly what the sole-candidate shortcut is permitted to
    /// bind past, since having one candidate settles which overload is meant. The result was a call written into the
    /// consumer's assembly that the C# compiler answers with <c>CS1503</c>, twice, off a <c>.g.cs</c> nobody can
    /// edit, where the engine answers <c>HED1012</c> before it renders a byte.
    /// <para>The cure is to stop the estimate being lossy at the seam rather than to delete the shortcut: the
    /// binder is handed the symbol the writer already resolved. That <i>shrinks</i> what "cannot say" covers instead
    /// of widening what it is allowed to mean — deleting the shortcut would send every genuinely untypeable
    /// argument to a degrade.</para>
    /// </summary>
    public class ExportArgumentTypingTests
    {
        private const string OrderType = "Heddle.Generator.IntegrationTests.Fixtures.Order";
        private const string HolderType = "Heddle.Generator.IntegrationTests.ArgTyping.HolderModel";

        private static string Template(string modelType, string expression) =>
            "@model(){{" + modelType + "}}@\\\nvalue: @(" + expression + ")\n";

        private static Diagnostic[] Unbindable(DifferentialHarness.GenResult gen) =>
            gen.Diagnostics.Where(d => d.Id == HeddleDiagnosticIds.BuildFunctionCallNotBindable).ToArray();

        private static TemplateOptions HostOptions()
        {
            var options = new TemplateOptions();
            var registry = new Runtime.Expressions.FunctionRegistry();
            registry.RegisterFrom(typeof(TemplateFunctions).Assembly);
            options.Functions = registry;
            return options;
        }

        private static Order SampleOrder() =>
            new Order { Count = 3, Name = "ab", Total = new Money(4m), Status = OrderStatus.Open };

        /// <summary>
        /// The three argument categories the descriptor cannot name, each against a <b>sole</b> exported overload
        /// that takes none of them. The engine refuses the template outright; the build now refuses it with the
        /// engine's own sentence instead of pre-compiling a call that cannot be compiled.
        /// </summary>
        [Theory]
        [InlineData("struct", "rokstr(Total)", "rokstr", "(Money)")]
        [InlineData("enum", "rokstr(Status)", "rokstr", "(OrderStatus)")]
        [InlineData("class", "rokstr(Maker)", "rokstr", "(Manufacturer)")]
        [InlineData("struct-to-string", "titlecase(Total)", "titlecase", "(Money)")]
        public void AnArgumentTheDescriptorCannotNameStillRulesOutASoleOverload(string name, string call,
            string function, string arguments)
        {
            var key = "argtyping/sole-" + name + ".heddle";
            var content = Template(OrderType, call);
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var single = Assert.Single(Unbindable(gen));
            Assert.Equal(DiagnosticSeverity.Error, single.Severity);
            Assert.Contains("'" + function + "'", single.GetMessage());
            Assert.Contains(arguments, single.GetMessage());
            Assert.Contains(HeddleDiagnosticIds.NoFunctionOverload, single.GetMessage());
            DifferentialHarness.ExpectDegrade(gen, key);

            // The same verdict about the same template on the tier that is the reference.
            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(HostOptions(), typeof(Order)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains(compiled.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.NoFunctionOverload && e.Error.Contains(arguments));
        }

        /// <summary>
        /// The near neighbours, without which the rows above would be satisfied by a blanket refusal of every
        /// argument the descriptor cannot name. Each of these is such an argument, each one <i>fits</i> a candidate,
        /// and each must still precompile and render the engine's bytes — the struct and the enum through the
        /// boxing rank into an <c>object</c> parameter, the fitting primitive through the exact one.
        /// </summary>
        [Theory]
        [InlineData("struct-boxes", "argbox(Total)", "value: b:4\n")]
        [InlineData("enum-boxes", "argbox(Status)", "value: b:Open\n")]
        [InlineData("int-boxes", "argbox(Count)", "value: b:3\n")]
        [InlineData("string-boxes", "argbox(Name)", "value: b:ab\n")]
        [InlineData("sole-fitting-int", "rokstr(Count)", "value: os3\n")]
        [InlineData("sole-fitting-string", "titlecase(Name)", "value: Ab\n")]
        public void AnArgumentTheDescriptorCannotNameStillPrecompilesWhereItFits(string name, string call,
            string expected)
        {
            var key = "argtyping/fits-" + name + ".heddle";
            var content = Template(OrderType, call);
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            Assert.Empty(Unbindable(gen));
            var precompiled = DifferentialHarness.RenderGenerated(gen, key, SampleOrder(), HostOptions());

            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(HostOptions(), typeof(Order)));
            Assert.True(compiled.CompileResult.Success, compiled.CompileResult.ToString());
            var dynamic = compiled.Generate(SampleOrder());

            Assert.Equal(expected, dynamic);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>
        /// The cast the binder writes is a name in the consumer's assembly, so it goes through the classifier every
        /// other spelled name does. It used to be out of reach of an unnameable type only because of what the
        /// argument estimator could reach — the primitives — and nothing checked; typing the arguments puts a host's
        /// whole signature set within reach, where an <c>[Obsolete(error: true)]</c> parameter type is CS0619
        /// against a <c>.g.cs</c> the consumer cannot edit. Reflection ignores the attribute, so the engine binds
        /// this call and renders: the template degrades rather than stopping a build its author did not break.
        /// </summary>
        [Fact]
        public void AParameterTypeGeneratedCodeMayNotNameDegradesRatherThanBreakingTheConsumersBuild()
        {
            const string key = "argtyping/retired-parameter.heddle";
            var content = Template(HolderType, "argsink(Retired)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            DifferentialHarness.ExpectDegrade(gen, key);

#pragma warning disable CS0618
            var model = new HolderModel { Retired = new RetiredHolder(), Live = new LiveHolder(), Count = 1 };
#pragma warning restore CS0618
            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(HostOptions(), typeof(HolderModel)));
            Assert.True(compiled.CompileResult.Success, compiled.CompileResult.ToString());
            Assert.Equal("value: retired\n", compiled.Generate(model));
        }

        /// <summary>The neighbour that keeps the row above about the parameter type and not about the call shape:
        /// the same two-overload set, the same reference-conversion rank, a parameter type this compilation may
        /// name — precompiles and renders the engine's bytes.</summary>
        [Fact]
        public void TheSameShapeOverANameableParameterTypePrecompilesAndMatches()
        {
            const string key = "argtyping/live-parameter.heddle";
            var content = Template(HolderType, "arglive(Live)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            Assert.DoesNotContain(gen.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
#pragma warning disable CS0618
            var model = new HolderModel { Retired = new RetiredHolder(), Live = new LiveHolder(), Count = 1 };
#pragma warning restore CS0618
            var precompiled = DifferentialHarness.RenderGenerated(gen, key, model, HostOptions());

            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(HostOptions(), typeof(HolderModel)));
            Assert.True(compiled.CompileResult.Success, compiled.CompileResult.ToString());
            var dynamic = compiled.Generate(model);

            Assert.Equal("value: live\n", dynamic);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>A <c>params</c> signature is the shape the sole-candidate shortcut exists for, and typing the
        /// arguments must not cost it: the fixed-arity call through the sole overload keeps binding without any
        /// ranking being imposed.</summary>
        [Fact]
        public void ASoleParamsOverloadKeepsBindingThroughTheShortcut()
        {
            const string key = "argtyping/params.heddle";
            var content = Template(OrderType, "joinargs(Name)");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            Assert.Empty(Unbindable(gen));
            var precompiled = DifferentialHarness.RenderGenerated(gen, key, SampleOrder(), HostOptions());

            var compiled = new HeddleTemplate(content, new Runtime.CompileContext(HostOptions(), typeof(Order)));
            Assert.True(compiled.CompileResult.Success, compiled.CompileResult.ToString());
            Assert.Equal(compiled.Generate(SampleOrder()), precompiled);
        }
    }
}
