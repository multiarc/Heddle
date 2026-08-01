using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Generator.IntegrationTests.ListTyping;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests.ListTyping
{
    /// <summary>An element type generated code in another assembly may not write the name of. Reflection ignores
    /// the attribute entirely, so the engine iterates such a collection and renders its body.</summary>
    [System.Obsolete("retired", true)]
    public sealed class RetiredItem
    {
        public string Tag => "t";
    }

    /// <summary>Naming the retired element as a type argument needs an obsolete context of its own; warning-level
    /// is the weakest one that gives it, which also keeps this type nameable — so the element is the only thing out
    /// of reach, and the collection itself passes every gate ahead of it.</summary>
    [System.Obsolete("declaring a retired element type needs an obsolete context")]
    public sealed class RetiredItems : IEnumerable<RetiredItem>
    {
        private readonly List<RetiredItem> _items = new List<RetiredItem> { new RetiredItem(), new RetiredItem() };

        public IEnumerator<RetiredItem> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// What model an <c>@list</c> body runs under, and what that means for its <b>reads</b>.
    /// <para><c>ListExtension.InitStart</c> returns the collection's <c>IEnumerable&lt;T&gt;</c> argument, so the
    /// engine compiles the body once, in a scope of that type; only where the host reaches no generic form does it
    /// hand back <c>ExType.Dynamic</c>. The emitter computed the element type — every gate that types a call site
    /// inside the body already read it — and then emitted the body's own reads on the dynamic tier anyway, which
    /// cost it in both directions at once.</para>
    /// <list type="bullet">
    /// <item><description>A read the dynamic tier cannot express — a native expression over the element's own
    /// member, a function call taking one, a ternary — degraded a template the engine renders.</description></item>
    /// <item><description>A member the element type does <b>not</b> carry is an <c>HED0001</c> the engine raises
    /// when it compiles the template; bound dynamically it precompiled and threw <c>RuntimeBinderException</c> at
    /// render. Over an element type of <c>object</c> — a static type, not the absence of one — it precompiled and
    /// <b>rendered</b> a page the engine refuses outright.</description></item>
    /// </list>
    /// </summary>
    public class ListElementBodyTypingTests
    {
        private const string CatalogType = "Heddle.Generator.IntegrationTests.Fixtures.Catalog";
        private const string ObjItemsType = "Heddle.Generator.IntegrationTests.Fixtures.ObjItems";

        private static Catalog Sample() => new Catalog
        {
            Title = "T",
            Tags = new[] { "xy" },
            Products = new List<Product>
            {
                new Product { Name = "a", Manufacturer = new Manufacturer { Name = "m" } },
                new Product { Name = "b", Manufacturer = new Manufacturer { Name = "n" } }
            }
        };

        private static string Template(string modelType, string body) =>
            "@model(){{" + modelType + "}}@\\\n@list(" + body + "\n";

        /// <summary>
        /// The reads that used to cost the template its tier. Each reads the ELEMENT's own member through a
        /// construct the dynamic tier has no emission for, and each must now precompile and render the engine's
        /// bytes — byte-compared against the engine on the same model, because "it precompiled" is not the claim.
        /// </summary>
        [Theory]
        [InlineData("native-expression", "Products){{[@(Name + \"!\")]}}", "[a!][b!]\n")]
        [InlineData("function-call", "Products){{[@upper(Name)]}}", "[A][B]\n")]
        [InlineData("ternary", "Products){{[@(Name == \"a\" ? 1 : 2)]}}", "[1][2]\n")]
        [InlineData("hop", "Products){{[@(Manufacturer.Name)]}}", "[m][n]\n")]
        [InlineData("string-element-expression", "Tags){{[@(Length + 1)]}}", "[3]\n")]
        public void AReadOfTheElementsOwnMemberPrecompilesAndRendersTheEnginesBytes(string name, string body,
            string expected)
        {
            var key = "list-typing/reads-" + name + ".heddle";
            var content = Template(CatalogType, body);
            var (precompiled, dynamic) = DifferentialHarness.Render(key, content, typeof(Catalog), Sample());

            Assert.Equal(expected, dynamic);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>The shapes that already worked, kept beside the ones above so a rule is being asserted and not
        /// a blanket admission of <c>@list</c> bodies: a plain path, a body that reads nothing, <c>this</c>, and a
        /// nested <c>@list</c> over the element.</summary>
        [Theory]
        [InlineData("plain-path", "Products){{[@(Name)]}}", "[a][b]\n")]
        [InlineData("reads-nothing", "Products){{[x]}}", "[x][x]\n")]
        [InlineData("this", "Tags){{[@()]}}", "[xy]\n")]
        [InlineData("nested-list", "Products){{[@list(Name){{z}}]}}", "[z][z]\n")]
        public void TheShapesThatAlreadyWorkedStillDo(string name, string body, string expected)
        {
            var key = "list-typing/neighbour-" + name + ".heddle";
            var content = Template(CatalogType, body);
            var (precompiled, dynamic) = DifferentialHarness.Render(key, content, typeof(Catalog), Sample());

            Assert.Equal(expected, dynamic);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>
        /// The refusing half. A member the element type does not carry is <c>HED0001</c> on the engine before it
        /// renders a byte; the build now says the same thing with <c>HED7008</c> instead of pre-compiling a read
        /// that throws <c>RuntimeBinderException</c> the first time the page is served.
        /// </summary>
        [Theory]
        [InlineData("missing", "Products){{[@(Nope)]}}", "Product")]
        [InlineData("missing-through-a-hop", "Products){{[@(Manufacturer.Nope)]}}", "Manufacturer")]
        [InlineData("missing-in-a-condition", "Products){{[@if(Nope){{y}}]}}", "Product")]
        [InlineData("missing-on-a-string-element", "Tags){{[@(Nope)]}}", "string")]
        public void AMemberTheElementTypeLacksIsRefusedAtBuildTheWayTheEngineRefusesIt(string name, string body,
            string typeName)
        {
            var key = "list-typing/absent-" + name + ".heddle";
            var content = Template(CatalogType, body);
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            var reported = gen.Diagnostics
                .Where(d => d.Id == HeddleDiagnosticIds.BuildUnresolvableMember).ToArray();
            Assert.NotEmpty(reported);
            Assert.All(reported, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
            Assert.Contains(reported, d => d.GetMessage().Contains(typeName));
            DifferentialHarness.ExpectDegrade(gen, key);

            var compiled = new HeddleTemplate(content,
                new Runtime.CompileContext(new TemplateOptions(), typeof(Catalog)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains(compiled.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.PropertyNotFound);
        }

        /// <summary>
        /// <c>object</c> is a static type and not an absence of one, so the engine compiles the body against it and
        /// a member read is <c>HED0001</c>. This is the row that was <b>silent wrong output</b>: the generated tier
        /// precompiled and rendered <c>[1]</c> — <c>"s".Length</c>, resolved dynamically off the runtime type —
        /// where the engine refuses the template outright. It must not render.
        /// </summary>
        [Fact]
        public void AnObjectElementIsATypeToo()
        {
            const string key = "list-typing/object-element.heddle";
            var content = Template(ObjItemsType, "Items){{[@(Length)]}}");
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            DifferentialHarness.ExpectDegrade(gen, key);

            var compiled = new HeddleTemplate(content,
                new Runtime.CompileContext(new TemplateOptions(), typeof(ObjItems)));
            Assert.False(compiled.CompileResult.Success);
            Assert.Contains(compiled.CompileResult.ErrorList,
                e => e.DiagnosticId == HeddleDiagnosticIds.PropertyNotFound);
        }

        /// <summary>The neighbour that keeps the row above about the READ and not about the element type: the same
        /// <c>object</c>-element collection with a body that reads nothing still precompiles and renders the
        /// engine's bytes, because the engine compiles that body perfectly well.</summary>
        [Theory]
        [InlineData("reads-nothing", "Items){{[x]}}", "[x][x]\n")]
        [InlineData("this", "Items){{[@()]}}", "[s][7]\n")]
        public void AnObjectElementBodyThatDoesNotReadAMemberStillPrecompiles(string name, string body,
            string expected)
        {
            var key = "list-typing/object-ok-" + name + ".heddle";
            var content = Template(ObjItemsType, body);
            var model = new ObjItems { Items = new List<object> { "s", 7 } };
            var (precompiled, dynamic) = DifferentialHarness.Render(key, content, typeof(ObjItems), model);

            Assert.Equal(expected, dynamic);
            Assert.Equal(dynamic, precompiled);
        }

        /// <summary>
        /// Typing the body means <b>writing</b> the element type — <c>(T)scope.ModelData</c> — so it has to pass
        /// the same gate every other name the emitter spells passes. Here it does not: the element is
        /// <c>[Obsolete(error: true)]</c> and the cast would be CS0619 in the consumer's build, off a
        /// <c>.g.cs</c> nobody can edit, over a collection the engine iterates and renders perfectly well. The
        /// template degrades and says so, rather than stopping a build its author did not break.
        /// <para>Without this the guard would be an unmeasured one, which the record counts as a cost rather than a
        /// safety margin — so it is measured. The collection itself is nameable, which is what puts the refusal on
        /// the element and nothing else.</para>
        /// <para><b>The second row is the cost, recorded rather than narrowed.</b> Its body reads nothing, so no
        /// cast would have been written and the file would have compiled — and it degrades anyway, because the
        /// refusal is taken before the body is inspected. Narrowing it means building the body against no type and
        /// asking afterwards whether it consulted one, which is exactly the state the typing above exists to
        /// prevent. A cycle that narrows the rule reddens this row and has to say what it did.</para>
        /// </summary>
        /// <param name="body">The read that puts the element type into the file, and — in the second row — one that
        /// does not.</param>
        [Theory]
        [InlineData("reads-the-element", "[@(Tag)]", "[t][t]\n")]
        [InlineData("reads-nothing", "[x]", "[x][x]\n")]
        public void AnElementTypeGeneratedCodeMayNotNameDegradesRatherThanBreakingTheConsumersBuild(string name,
            string body, string expected)
        {
            var key = "list-typing/retired-element-" + name + ".heddle";
            var content = "@model(){{Heddle.Generator.IntegrationTests.ListTyping.RetiredItems}}@\\\n" +
                          "@list(this){{" + body + "}}\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });

            Assert.Contains(gen.Diagnostics, d => d.Id == HeddleDiagnosticIds.BuildInaccessibleModelSymbol);
            DifferentialHarness.ExpectDegrade(gen, key);

#pragma warning disable CS0618
            var model = new RetiredItems();
#pragma warning restore CS0618
            var compiled = new HeddleTemplate(content,
                new Runtime.CompileContext(new TemplateOptions(), typeof(RetiredItems)));
            Assert.True(compiled.CompileResult.Success, compiled.CompileResult.ToString());
            Assert.Equal(expected, compiled.Generate(model));
        }
    }
}
