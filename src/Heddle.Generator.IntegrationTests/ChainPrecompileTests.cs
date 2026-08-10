using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// A chain is a straight sequence of direct calls threading one value, and the precompiled tier emits it as
    /// exactly that: <c>TemplateChain.RenderData</c>, statement for statement. The chained channel seeds the running
    /// value, every item is handed <c>scope.Chain(running)</c> — so an item's own call parameter is read off the
    /// chained scope and not the ambient one — and the leftmost item renders where the rest process.
    /// <para>Every case here is tier-pinned: <c>DifferentialHarness.Render</c> declares the precompiled expectation
    /// before it renders, so a silent build-time degrade cannot pass as byte parity (fallback is byte-identical by
    /// design, which is exactly why an unpinned chain test would prove nothing).</para>
    /// </summary>
    public class ChainPrecompileTests
    {
        private const string Model = "@model(){{System.String}}@\\\n";

        private static DifferentialHarness.GenResult Generated(string key, string template)
        {
            var gen = DifferentialHarness.Generate(new[] { (key, template) });
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                "Unexpected generator error: " + string.Join("; ", gen.Diagnostics.Select(d => d.ToString())));
            DifferentialHarness.ExpectPrecompiled(gen, key);
            return gen;
        }

        /// <summary>Two, three and four items through the unnamed carrier, a custom extension and a registered
        /// function — every route <c>BuildCall</c> takes, reached as a chain item rather than as a lone call. The
        /// definition routes are <see cref="ChainedDefinitionTests"/>, which owns those two templates.</summary>
        [Theory]
        // The leftmost item is the unnamed carrier, which is the profile-resolved one.
        [InlineData("<x>@():string(this)</x>", "<x>Hi</x>\n")]
        // A custom extension as the rendering item, and as the producer.
        [InlineData("<x>@yell():string(this)</x>", "<x>HI!</x>\n")]
        [InlineData("<x>@raw():yell(this)</x>", "<x>Hi</x>\n")]
        // A registered function as the producer.
        [InlineData("<x>@raw():upper(this)</x>", "<x>Hi</x>\n")]
        // Four items, mixing all three tiers of call in one chain.
        [InlineData("<x>@yell():raw():string(this)</x>", "<x>HI!</x>\n")]
        public void AnOutputChainPrecompilesAndRendersTheEnginesBytes(string body, string expected)
        {
            var key = "views/chain-" + body.GetHashCode().ToString("x8") + ".heddle";
            var template = Model + body + "\n";
            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), "Hi");
            Assert.Equal(dyn, pre);
            Assert.Equal(expected, pre);
        }

        /// <summary>A chain in call-parameter position — <c>@a(b():c())</c>, the grammar's third <c>call</c>
        /// alternative. The engine compiles it with <c>CompileParameterChain</c> and reads it back through
        /// <c>ChainedParameter</c>, which is <c>TemplateChain.ProcessData</c>: every item processes and the leftmost
        /// result is the value.</summary>
        [Theory]
        [InlineData("<x>@raw(yell(this):string(this))</x>", "<x>HI!</x>\n")]
        [InlineData("<x>@raw(upper(this):string(this))</x>", "<x>HI</x>\n")]
        [InlineData("@% <box>{{[@out()]}} %@\n<x>@raw(box():string(this))</x>", "<x>[Hi]</x>\n")]
        public void AChainInCallParameterPositionPrecompiles(string body, string expected)
        {
            var key = "views/chainparam-" + body.GetHashCode().ToString("x8") + ".heddle";
            var template = Model + body + "\n";
            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), "Hi");
            Assert.Equal(dyn, pre);
            Assert.Equal(expected, pre);
        }

        /// <summary>The body belongs to the chain's <b>leftmost</b> item — <c>ParseContext</c> attaches the
        /// outblock's single subtemplate to <c>Chain.First()</c> — so <c>@a():b(){{…}}</c> is a bodied leftmost
        /// item and no other item of a chain can carry a body at all.</summary>
        [Fact]
        public void AChainWhoseLeftmostItemHasABodyPrecompiles()
        {
            const string key = "views/chain-bodied.heddle";
            var template = Model + "<x>@bellow():string(this){{loud}}</x>\n";
            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), null);
            Assert.Equal(dyn, pre);
            // @bellow steps back to its body when the value is null, so the body is what renders.
            Assert.Equal("<x>LOUD</x>\n", pre);
        }

        /// <summary>Inside a <c>@list</c> body the chained channel already carries the iteration index, so a chain
        /// there has to seed its running value from that channel rather than from nothing — which is what
        /// <c>result = scope.ChainedData</c> is for, on both tiers.</summary>
        [Theory]
        [InlineData("@list(Tags){{[@raw():string(this)]}}", "[a][b]\n")]
        [InlineData("@list(Tags){{[@raw(string(this):string(this))]}}", "[a][b]\n")]
        [InlineData("@list(Products){{[@raw(Name):yell(Name)]}}", "[p1][p2]\n")]
        public void AChainInsideAListBodyKeepsTheIterationChannel(string body, string expected)
        {
            var key = "views/chainlist-" + body.GetHashCode().ToString("x8") + ".heddle";
            var template = "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Catalog}}@\\\n" + body + "\n";
            var model = new Fixtures.Catalog
            {
                Title = "t",
                Tags = new[] { "a", "b" },
                Products = new List<Fixtures.Product>
                {
                    new Fixtures.Product { Name = "p1" }, new Fixtures.Product { Name = "p2" }
                }
            };
            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(Fixtures.Catalog), model);
            Assert.Equal(dyn, pre);
            Assert.Equal(expected, pre);
        }

        /// <summary>A chain in a body whose model type the build cannot name, because the extension hosting it
        /// decides that type in its own hook. Unknown typing is not a refusal: the body is emitted type-agnostically
        /// and the chain inside it precompiles with it.</summary>
        [Fact]
        public void AChainInABodyTypedByAnUnreadHookPrecompilesTypeAgnostically()
        {
            const string key = "views/chain-agnostic.heddle";
            var template = Model + "<x>@bellow(this){{@raw():string(this)}}</x>\n";
            var gen = Generated(key, template);
            Assert.Contains("PrecompiledRuntime.Init(", Assert.Single(gen.TemplateSources).Value);

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), "Hi");
            Assert.Equal(dyn, pre);
            Assert.Equal("<x>HI</x>\n", pre);
        }

        /// <summary>
        /// The chained type an item's hook is handed is the type the item to its <b>right</b> actually returned —
        /// the engine's <c>returnTypeChainedPrevious</c>, threaded right to left. The build never predicts it: the
        /// producer's own <c>InitStart</c> runs first (its field initializer precedes the consumer's) and the
        /// consumer's site reads the answer off it.
        /// <para><c>@chainedtype</c> renders what it was handed, so the claim is a rendered byte rather than an
        /// assertion about a field: <c>@string</c> returns <c>System.String</c> from its hook, and that is what the
        /// item to its left is initialized with. Threaded as <c>dynamic</c> instead — the type a build that
        /// predicted nothing would have to write — this reads <c>dynamic</c> and the two tiers part company.</para>
        /// </summary>
        [Fact]
        public void AChainedTypeIsTheProducersOwnHookAnswer()
        {
            const string key = "views/chain-typed.heddle";
            var template = Model + "<a>@chainedtype():string(this)</a>\n";
            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), "Hi");
            Assert.Equal(dyn, pre);
            Assert.Equal("<a>String</a>\n", pre);
        }

        /// <summary>A chain item whose extension declares <c>[PrecompileUnsupported]</c> costs <b>that item</b> and
        /// not the chain: the rest of the chain is emitted, and the disowned item renders by compiling its own call
        /// text. That text has to be the item's own — handed the whole chain the substitute would run the producers
        /// a second time — so the byte comparison is what proves the reconstruction.</summary>
        [Theory]
        [InlineData("<x>@raw():scanner(this)</x>", "<x>Hi</x>\n")]
        [InlineData("<x>@scanner():raw(this)</x>", "<x>scanned:Hi</x>\n")]
        public void ADisownedChainItemCostsThatItemAndNotTheChain(string body, string expected)
        {
            var key = "views/chainsite-" + body.GetHashCode().ToString("x8") + ".heddle";
            var template = Model + body + "\n";
            var gen = Generated(key, template);
            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("PrecompiledRuntime.SiteFallback(", source);
            Assert.Contains("SourceText = \"@scanner(", source);

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), "Hi");
            Assert.Equal(dyn, pre);
            Assert.Equal(expected, pre);
        }

        /// <summary>A chain whose composed result is nothing is removed from the document, exactly as a lone
        /// zero-output directive is: the rule reads the chain's leftmost item, which is the one whose
        /// <c>InitStart</c> return is the whole chain's, so it needed no chain-specific arm on either tier.</summary>
        [Fact]
        public void AChainComposingToNothingIsRemovedFromTheDocument()
        {
            const string key = "views/chain-directive.heddle";
            var template = Model + "<x>@using():string(this){{System.Text}}</x>\n";
            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), "Hi");
            Assert.Equal(dyn, pre);
            Assert.Equal("<x></x>\n", pre);
        }

        /// <summary>The two structural facts a chain writes onto a call site: a non-leading item is a chained
        /// consumer (what arms <c>@out</c>'s composed-projection guard), and an item with a producer to its right
        /// is what makes a bodiless definition invocation take its content from the chained channel
        /// (<c>MarkChainConsumer</c>).</summary>
        [Fact]
        public void AChainWritesItsConsumerAndProducerFactsOntoTheSite()
        {
            const string key = "views/chain-facts.heddle";
            var template = Model + "@% <box>{{[@out()]}} %@\n@box():string(this)\n";
            var gen = Generated(key, template);
            var source = Assert.Single(gen.TemplateSources).Value;
            Assert.Contains("IsChainedConsumer = true", source);
            Assert.Contains("HasProducerToRight = true", source);
            Assert.Contains("ChainProducer = S", source);
        }

        /// <summary>A body is its own document: a call inside the body of a chained item is not itself an item of
        /// that chain. Left carried, the two definition bodies' <c>@out()</c> calls here would each be written as a
        /// chained consumer — which is what arms <c>@out</c>'s composed-projection guard, a render-time throw for an
        /// <c>@out</c> that composes nothing — and would take their chained type from the enclosing chain's
        /// producer. Counted rather than merely present, because what is wrong is a surplus.</summary>
        [Fact]
        public void ACallInsideAChainedItemsBodyIsNotItselfAChainItem()
        {
            const string key = "views/chain-nested.heddle";
            var template = Model + "@% <heading>{{<h2>@out()</h2>}} <emphasis>{{<em>@out()</em>}} %@\n" +
                           "@heading():emphasis():string(this)\n";
            var gen = Generated(key, template);
            var source = Assert.Single(gen.TemplateSources).Value;

            // Exactly the two non-leading items of the chain — @emphasis and @string — and neither @out().
            Assert.Equal(2, Occurrences(source, "IsChainedConsumer = true"));

            var (pre, dyn) = DifferentialHarness.Render(key, template, typeof(string), "Hi");
            Assert.Equal(dyn, pre);
            Assert.Equal("<h2><em>Hi</em></h2>\n", pre);
        }

        private static int Occurrences(string haystack, string needle)
        {
            int count = 0;
            for (int i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
                count++;
            return count;
        }
    }
}
