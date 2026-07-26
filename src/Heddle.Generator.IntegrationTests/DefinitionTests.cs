using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
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
    }
}
