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
    /// Nested CLR types are addressed from templates with C#-style dots (<c>Outer.Nested</c>), never the CLR
    /// <c>+</c> spelling (which the lexer rejects as HED0003). These tests pin the dotted spelling for the two
    /// template-facing type positions: a definition's <c>:: model</c> clause and a slot's <c>out::</c> clause.
    /// Mirrors the shapes in <c>Heddle.Performance.PropsRenderBenchmarks</c> at small scale.
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
            // The stored canonical keys are CLR metadata names; the '+' spelling is unreachable from
            // templates (lexer rejects '+') but must keep resolving through the helper API.
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
            // Type.GetType fails on the dotted spelling; the helper retries by replacing the
            // rightmost '.' with '+' until the CLR metadata name is found.
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
            // Canonical metadata names: '+' between every nesting level, dots only in the namespace prefix.
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
            // Progressive rightmost-dot replacement walks
            //   ...NestedTypeResolutionTests.Outer.Middle+Inner   (miss)
            //   ...NestedTypeResolutionTests.Outer+Middle+Inner   (miss)
            //   ...NestedTypeResolutionTests+Outer+Middle+Inner   (hit)
            // Every intermediate candidate keeps all '+' to the right of the conversion point,
            // so no candidate ever places a dot after a '+'.
            Assert.Same(typeof(Outer.Middle.Inner),
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests.Outer.Middle.Inner, Heddle.Tests"));
        }

        [Fact]
        public void MixedSpellingWithDotAfterPlusDoesNotResolve()
        {
            HeddleTemplate.Configure(typeof(NestedTypeResolutionTests).GetTypeInfo().Assembly);
            // Invariant: nested types can only contain nested types, so a metadata name never has a
            // dot after a '+'. The mixed spelling is registered nowhere and must fail to resolve.
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("Heddle.Tests.NestedTypeResolutionTests+Outer.Middle"));
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("NestedTypeResolutionTests+Outer.Middle", "Heddle.Tests"));
        }
    }
}
