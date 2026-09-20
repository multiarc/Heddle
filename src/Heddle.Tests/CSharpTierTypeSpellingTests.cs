using System.Collections.Generic;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests.@event
{
    public class @class
    {
        public string Value { get; set; } = "kw";
    }
}

namespace Heddle.Tests
{
    public class SpellingShelf<T>
    {
        public T Top { get; set; }

        public class Slot<U>
        {
            public T Owner { get; set; }
            public U Item { get; set; }
        }
    }

    /// <summary>The C# tier declares its generated method's parameters with the model, chained and root types.
    /// Pins the regression where those were spelled by simple name and reached through an imported namespace:
    /// a nested type's simple name resolves nowhere (CS0246), and a generic argument's namespace was imported
    /// one level deep only. They are now spelled in full, so any type the template can be typed with compiles.</summary>
    public class CSharpTierTypeSpellingTests
    {
        public class Outer
        {
            public string Name { get; set; } = "outer";

            public class Inner
            {
                public string Value { get; set; } = "inner";
                public List<Inner> Siblings { get; set; } = new List<Inner>();
            }
        }

        private static string Render(string document, object model, ExType modelType = null)
        {
            HeddleTemplate.Configure(typeof(CSharpTierTypeSpellingTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp, OutputProfile = OutputProfile.Text };
            var template = new HeddleTemplate(document, new CompileContext(options, modelType ?? model.GetType()));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return template.Generate(model);
        }

        /// <summary>A namespace and a type named like C# keywords: legal in metadata and in any language that
        /// escapes them, and the C# tier imports the model's namespace with a <c>using</c> of its own.</summary>
        [Fact]
        public void ModelInANamespaceNamedLikeAKeyword() =>
            Assert.Equal("KW", Render("@(@model.Value.ToUpper())", new Heddle.Tests.@event.@class()));

        [Fact]
        public void NestedModelType() =>
            Assert.Equal("INNER", Render("@(@model.Value.ToUpper())", new Outer.Inner()));

        [Fact]
        public void GenericOfNestedModelType() =>
            Assert.Equal("2", Render("@(@model.Count + 1)", new List<Outer.Inner> { new Outer.Inner() }));

        [Fact]
        public void DictionaryOfListsOfNestedModelType() =>
            Assert.Equal("0", Render("@(@model.Count)", new Dictionary<string, List<Outer.Inner[]>>()));

        [Fact]
        public void NestedGenericInGenericModelType() =>
            Assert.Equal("a7", Render("@(@model.Owner + model.Item)",
                new SpellingShelf<string>.Slot<int> { Owner = "a", Item = 7 }));

        [Fact]
        public void JaggedArrayOfNestedModelType() =>
            Assert.Equal("1", Render("@(@model.Length)", new[] { new Outer.Inner[0, 0] }));

        [Fact]
        public void NestedTypeAsChainedAndRootType()
        {
            // The list body's model is the nested element type; root stays the outer holder; the chained
            // value of the C# call is the nested type flowing from the call to its right.
            var model = new Outer.Inner { Siblings = { new Outer.Inner { Value = "s" } } };
            Assert.Equal("s/inner|INNER",
                Render("@list(Siblings){{@(@model.Value + \"/\" + root.Value)}}|@(@chained.Value.ToUpper()):param(this)",
                    model));
        }

        [Fact]
        public void DefinitionTypedWithADottedNestedType() =>
            Assert.Equal("[inner]", Render(
                "@% <show>{{[@(@model.Value)]}} :: Heddle.Tests.CSharpTierTypeSpellingTests.Outer.Inner %@@show(this)",
                new Outer.Inner()));
    }
}
