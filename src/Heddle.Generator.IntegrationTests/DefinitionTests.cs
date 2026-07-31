using System;
using System.Collections.Generic;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Definition invocation: shared body class, call-site binding, and recursion guard coverage.
    /// Differential-gated byte-for-byte against the runtime backend.
    /// </summary>
    public class DefinitionTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";
        private const string GreetingType = "Heddle.Generator.IntegrationTests.Fixtures.GreetingModel";
        private const string TreeType = "Heddle.Generator.IntegrationTests.Fixtures.TreeNode";

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Products()
        {
            yield return new object[] { new Product { Name = "Widget", Manufacturer = new Manufacturer { Name = "Acme" } } };
            yield return new object[] { new Product { Name = "X", Manufacturer = new Manufacturer { Name = null } } };
            yield return new object[] { new Product { Name = "X", Manufacturer = null } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void TypedDefinitionByName(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<greet>{{Made by @(Name).}} :: Heddle.Generator.IntegrationTests.Fixtures.Manufacturer%@\n" +
                    "<footer>@greet(Manufacturer)</footer>\n";
            AssertParity("views/def-typed.heddle", t, typeof(Product), model);
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void DefinitionWithCallerContent(Product model)
        {
            // Caller-supplied content { … } is buffered and spliced back via @out().
            var t = "@model(){{" + ProductType + "}}@\\\n" +
                    "@%<box>{{[@out()]}} :: Heddle.Generator.IntegrationTests.Fixtures.Manufacturer%@\n" +
                    "@box(Manufacturer){{name=@(Name)}}\n";
            AssertParity("views/def-caller.heddle", t, typeof(Product), model);
        }

        public static IEnumerable<object[]> Greetings()
        {
            yield return new object[] { new GreetingModel { Payload = new UserPayload { User = new UserInfo { Name = "Ada" } } } };
            yield return new object[] { new GreetingModel { Payload = new UserPayload { User = null } } };
            yield return new object[] { new GreetingModel { Payload = null } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Greetings))]
        public void DynamicDefinition(GreetingModel model)
        {
            var t = "@%<greeting>{{Hello, @(User.Name)!}} :: dynamic%@\n@greeting(Payload)\n";
            AssertParity("views/greeting.heddle", t, typeof(GreetingModel), model);
        }

        public static IEnumerable<object[]> Trees()
        {
            yield return new object[] { new TreeNode { Label = "a", Next = new TreeNode { Label = "b", Next = new TreeNode { Label = "c" } } } };
            yield return new object[] { new TreeNode { Label = "solo" } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Trees))]
        public void SelfRecursiveDefinition(TreeNode model)
        {
            // Recursion guard bounds depth even though the generator terminates on the shared body class.
            var t = "@model(){{" + TreeType + "}}@\\\n" +
                    "@%<walk>{{@(Label)@if(Next){{-@walk(Next)}}}} :: " + TreeType + "%@\n" +
                    "@walk(this)\n";
            AssertParity("views/def-recursion.heddle", t, typeof(TreeNode), model);
        }

        /// <summary>
        /// A definition declaring <c>:: System.String</c> whose call site hands it a chain. The declaration is
        /// truthful — a chain call-parameter is rendered by the carrier it rides in, so the model really is text —
        /// and the body reads <c>Length</c> off it on both tiers. Handed the producer's own <c>int</c> instead, the
        /// generated body's cast to <c>string</c> threw <see cref="InvalidCastException"/> at render over a page the
        /// engine prints.
        /// </summary>
        [Fact]
        public void AChainValueReachesATypedDefinitionAsText()
        {
            const string t = "@model(){{" + ProductType + "}}@%\n" +
                             "<probe>{{[@(Length)]}} :: System.String\n%@\n" +
                             "@probe(len(Name))\n";
            var (precompiled, dyn) = DifferentialHarness.Render("views/def-chain-typed.heddle", t, typeof(Product),
                new Product { Name = "abcd" });
            Assert.Equal(dyn, precompiled);
            Assert.Equal("[1]\n", dyn);
        }

        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";
        private const string NestedType = "Heddle.Generator.IntegrationTests.Fixtures.Nested";

        private static string DeclaredModelDoc(string declared, string argument) =>
            "@model(){{" + CartType + "}}@%\n<frame>{{[@()]}} :: " + declared + "\n%@\n@frame(" + argument + ")\n";

        private static Cart CartModel() => new Cart { Name = "abc", Nested = new Nested() };

        /// <summary>
        /// A declared <c>:: T</c> is a type the call site's value has to satisfy, not only the type the body is
        /// compiled under. The engine compares them and refuses the template; the emitter took the declaration as
        /// the body's model and never looked at what was being passed, so the value was cast to <c>T</c> in
        /// generated code — rendering the wrong member for a reference type, and throwing
        /// <see cref="InvalidCastException"/> where the body read one.
        /// </summary>
        [Theory]
        [InlineData("System.String", "Nested", "Heddle.Generator.IntegrationTests.Fixtures.Nested",
            "System.String")]
        [InlineData("System.Int32", "Name", "System.String", "System.Int32")]
        [InlineData(CartType, "Nested", "Heddle.Generator.IntegrationTests.Fixtures.Nested",
            "Heddle.Generator.IntegrationTests.Fixtures.Cart")]
        public void ADeclaredDefinitionModelTheCallSiteValueDoesNotSatisfyDegrades(string declared, string argument,
            string valueTypeName, string declaredTypeName)
        {
            var template = DeclaredModelDoc(declared, argument);
            var key = "views/declared-model-" + declared.Replace('.', '-') + "-" + argument + ".heddle";
            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);

            var dynamicTemplate = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), typeof(Cart)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            var text = dynamicTemplate.CompileResult.ToString();
            Assert.Contains(HeddleDiagnosticIds.ReturnTypeMismatch, text, StringComparison.Ordinal);
            Assert.Contains("Return Type is " + valueTypeName + " but any of [" + declaredTypeName + "]", text,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// The call forms the engine does <b>not</b> compare, because its model accessor hands them no input type:
        /// a literal, <c>this</c>, and a chain, each against a declared model none of them satisfies. The engine
        /// compiles and renders all three, so mirroring its asymmetry is what keeps them on the precompiled tier —
        /// a rule that checked every call form would take them off it. The member-path rows that do satisfy their
        /// declaration are here too, so the refusal above is not a refusal of member paths.
        /// </summary>
        [Theory]
        [InlineData("System.String", "5", "[5]\n")]
        [InlineData("System.String", "this", "[Heddle.Generator.IntegrationTests.Fixtures.Cart]\n")]
        [InlineData("System.Int32", "len(Name)", "[3]\n")]
        [InlineData("System.String", "Name", "[abc]\n")]
        [InlineData(NestedType, "Nested", "[Heddle.Generator.IntegrationTests.Fixtures.Nested]\n")]
        [InlineData("System.Object", "Nested", "[Heddle.Generator.IntegrationTests.Fixtures.Nested]\n")]
        public void ACallFormTheEngineDoesNotCompareStillPrecompiles(string declared, string argument,
            string expected)
        {
            var template = DeclaredModelDoc(declared, argument);
            var key = "views/declared-model-ok-" + declared.Replace('.', '-') + "-" +
                      argument.Replace("(", "-").Replace(")", "") + ".heddle";
            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(Cart), CartModel());
            Assert.Equal(expected, dyn);
            Assert.Equal(dyn, precompiled);
        }

        private const string ArrayModelType = "Heddle.Generator.IntegrationTests.Fixtures.ArrayAcceptanceModel";

        private static string ArrayDeclaredModelDoc(string declared, string argument) =>
            "@model(){{" + ArrayModelType + "}}@%\n<frame>{{[@()]}} :: " + declared + "\n%@\n@frame(" +
            argument + ")\n";

        /// <summary>
        /// The <c>:: T</c> check runs over the whole type relation, so every element pair the runtime reduces to a
        /// common representative has to be a pair the check accepts too — including the direction where it is the
        /// declared interface's own type argument that reduces, the pointer-width pair, the 16-bit pair, and a
        /// lifted value the runtime compares by its underlying type. Each of these is a template the engine
        /// compiles and renders, so a check that refused it would cost the precompiled tier for nothing.
        /// </summary>
        [Theory]
        [InlineData("System.Collections.Generic.IList<System.UInt32>", "Ints", "[System.Int32[]]\n")]
        [InlineData("System.Collections.Generic.IEnumerable<System.UInt32>", "Ints", "[System.Int32[]]\n")]
        [InlineData("System.Collections.Generic.IReadOnlyList<System.UInt32>", "Ints", "[System.Int32[]]\n")]
        [InlineData("System.Collections.Generic.IList<System.DayOfWeek>", "Ints", "[System.Int32[]]\n")]
        [InlineData("System.Collections.Generic.IEnumerable<System.UInt32>", "Days", "[System.DayOfWeek[]]\n")]
        [InlineData("System.Collections.Generic.IList<System.DayOfWeek>", "UInts", "[System.UInt32[]]\n")]
        [InlineData("System.IntPtr[]", "NUInts", "[System.UIntPtr[]]\n")]
        [InlineData("System.UIntPtr[]", "NInts", "[System.IntPtr[]]\n")]
        [InlineData("System.Int16[]", "UShorts", "[System.UInt16[]]\n")]
        [InlineData("System.Int32", "Lifted", "[7]\n")]
        public void ADeclaredModelTheRuntimeAcceptsKeepsThePrecompiledTier(string declared, string argument,
            string expected)
        {
            var template = ArrayDeclaredModelDoc(declared, argument);
            var key = "views/declared-model-array-" + Sanitize(declared) + "-" + argument + ".heddle";
            DifferentialHarness.ExpectPrecompiled(DifferentialHarness.Generate(new[] { (key, template) }), key);

            var (precompiled, dyn) = DifferentialHarness.Render(key, template, typeof(ArrayAcceptanceModel),
                new ArrayAcceptanceModel());
            Assert.Equal(expected, dyn);
            Assert.Equal(dyn, precompiled);
        }

        /// <summary>The neighbour that says the rows above are a reduction and not a surrender: a width the runtime
        /// does not reduce is still refused, and by both tiers.</summary>
        [Fact]
        public void AnElementWidthTheRuntimeDoesNotReduceStaysRefused()
        {
            var template = ArrayDeclaredModelDoc("System.Int64[]", "Ints");
            const string key = "views/declared-model-array-widened.heddle";
            DifferentialHarness.ExpectDegrade(DifferentialHarness.Generate(new[] { (key, template) }), key);

            var dynamicTemplate = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions(), typeof(ArrayAcceptanceModel)));
            Assert.False(dynamicTemplate.CompileResult.Success);
            Assert.Contains(HeddleDiagnosticIds.ReturnTypeMismatch, dynamicTemplate.CompileResult.ToString(),
                StringComparison.Ordinal);
        }

        private static string Sanitize(string spelling)
        {
            var chars = spelling.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '-';
            return new string(chars);
        }
    }
}
