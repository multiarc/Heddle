using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Nested CLR types resolve from templates with C#-style dots (<c>Outer.Nested</c>), never the CLR <c>+</c> spelling.
    /// Pins the dotted spelling for definition <c>:: model</c> clauses and slot <c>out::</c> clauses.
    /// </summary>
    public class NestedTypeResolutionTests
    {
        public class Article
        {
            public string Title { get; set; }
            public string Summary { get; set; }
        }

        public class Option
        {
            public int Id { get; set; }
            public string Label { get; set; }
        }

        public class Menu
        {
            public IEnumerable<Option> Options { get; set; }
        }

        public class Outer
        {
            public class Middle
            {
                public string Name { get; set; }

                public class Inner
                {
                    public string Value { get; set; }
                }
            }
        }

        private static HeddleTemplate Compile(string document, ExType modelType)
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(document, new CompileContext(new TemplateOptions(), modelType));
        }

        [Fact]
        public void DefinitionModelClauseResolvesDottedNestedType()
        {
            var t = Compile(
                "@% <card>{{<h2>@(Title)</h2><p>@(Summary)</p>}} :: Heddle.Tests.NestedTypeResolutionTests.Article %@\n@card(this)",
                typeof(Article));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("<h2>Hello</h2><p>World</p>",
                t.Generate(new Article { Title = "Hello", Summary = "World" }).Trim());
        }

        [Fact]
        public void SlotOutClauseResolvesDottedNestedType()
        {
            var t = Compile(
                "@% <picker(out:: Heddle.Tests.NestedTypeResolutionTests.Option)>{{<ul>@list(Options){{<li>@out(this)</li>}}</ul>}} :: Heddle.Tests.NestedTypeResolutionTests.Menu %@\n@picker(this){{<a>@(Id):@(Label)</a>}}",
                typeof(Menu));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("<ul><li><a>1:A</a></li><li><a>2:B</a></li></ul>",
                t.Generate(new Menu
                {
                    Options = new List<Option>
                    {
                        new Option { Id = 1, Label = "A" },
                        new Option { Id = 2, Label = "B" }
                    }
                }).Trim());
        }

        [Fact]
        public void MetadataPlusSpellingStillResolvesAtHelperLevel()
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            // CLR metadata uses '+' spelling; templates cannot emit it (lexer rejects '+') but must still resolve via helper.
            Assert.Same(typeof(Article),
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests+Article"));
            Assert.Same(typeof(Article),
                ReflectionHelper.ResolveType("NestedTypeResolutionTests+Article"));
        }

        [Fact]
        public void DottedSpellingResolvesAtHelperLevel()
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            Assert.Same(typeof(Article),
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests.Article"));
            Assert.Same(typeof(Option),
                ReflectionHelper.ResolveType("NestedTypeResolutionTests.Option", "Heddle.Tests"));
        }

        [Fact]
        public void AssemblyQualifiedDottedNestedNameResolvesViaGetTypeFallback()
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            // Type.GetType fails on dotted spelling; helper retries by replacing rightmost '.' with '+' until CLR metadata name is found.
            Assert.Same(typeof(Article),
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests.Article, Heddle.Tests"));
        }

        [Fact]
        public void MultiLevelDottedSpellingResolvesAtHelperLevel()
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            Assert.Same(typeof(Outer.Middle.Inner),
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests.Outer.Middle.Inner"));
            Assert.Same(typeof(Outer.Middle.Inner),
                ReflectionHelper.ResolveType("NestedTypeResolutionTests.Outer.Middle.Inner", "Heddle.Tests"));
        }

        [Fact]
        public void MultiLevelMetadataPlusSpellingResolvesAtHelperLevel()
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            // CLR metadata names: '+' between every nesting level, dots only in the namespace prefix.
            Assert.Same(typeof(Outer.Middle.Inner),
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests+Outer+Middle+Inner"));
            Assert.Same(typeof(Outer.Middle.Inner),
                ReflectionHelper.ResolveType("NestedTypeResolutionTests+Outer+Middle+Inner"));
        }

        [Fact]
        public void DefinitionModelClauseResolvesMultiLevelDottedNestedType()
        {
            var t = Compile(
                "@% <chip>{{<b>@(Value)</b>}} :: Heddle.Tests.NestedTypeResolutionTests.Outer.Middle.Inner %@\n@chip(this)",
                typeof(Outer.Middle.Inner));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("<b>deep</b>",
                t.Generate(new Outer.Middle.Inner { Value = "deep" }).Trim());
        }

        [Fact]
        public void AssemblyQualifiedMultiLevelDottedNameResolvesViaGetTypeFallback()
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            // Progressive rightmost-dot replacement until CLR metadata name is found.
            Assert.Same(typeof(Outer.Middle.Inner),
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests.Outer.Middle.Inner, Heddle.Tests"));
        }

        [Fact]
        public void MixedSpellingWithDotAfterPlusDoesNotResolve()
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            // Nested types contain only nested types, so metadata names never have a dot after '+'; mixed spellings are unreachable.
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests+Outer.Middle"));
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("NestedTypeResolutionTests+Outer.Middle", "Heddle.Tests"));
        }
    }
}
