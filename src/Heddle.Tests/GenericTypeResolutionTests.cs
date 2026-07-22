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
    /// Constructed generic type names resolve through a small C#-grammar parser: dotted segments of
    /// <c>ident</c> or <c>ident&lt;argList&gt;</c> with optional trailing <c>[]</c>, arguments split on
    /// top-level commas and resolved recursively. Pins multi-parameter generics, nested generics,
    /// tuples, arrays of/in generics, and generic OUTER types in the C# spelling
    /// (<c>Ns.GenOuter&lt;int&gt;.Inner</c>). Storage keys stay CLR metadata names.
    /// </summary>
    public class GenericTypeResolutionTests
    {
        public class GenOuter<T>
        {
            public class Inner
            {
                public T Value { get; set; }
            }

            public class InnerGen<TU>
            {
                public T First { get; set; }
                public TU Second { get; set; }
            }
        }

        public class Plain
        {
            public string Name { get; set; }
        }

        public class Row
        {
            public string Cell { get; set; }
        }

        private static void Configure()
        {
            HeddleTemplate.Configure(typeof(GenericTypeResolutionTests).GetTypeInfo().Assembly);
        }

        [Fact]
        public void MultiParameterGenericResolves()
        {
            Configure();
            Assert.Same(typeof(Dictionary<string, int>),
                ReflectionHelper.ResolveType("System.Collections.Generic.Dictionary<string, int>"));
            Assert.Same(typeof(Dictionary<string, int>),
                ReflectionHelper.ResolveType("Dictionary<string, int>", "System.Collections.Generic"));
        }

        [Fact]
        public void NestedGenericArgumentResolves()
        {
            Configure();
            Assert.Same(typeof(List<List<int>>),
                ReflectionHelper.ResolveType("List<List<int>>", "System.Collections.Generic"));
            Assert.Same(typeof(Dictionary<string, List<int>>),
                ReflectionHelper.ResolveType("Dictionary<string, List<int>>", "System.Collections.Generic"));
        }

        [Fact]
        public void MultiElementTupleResolves()
        {
            Configure();
            Assert.Same(typeof(ValueTuple<int, string>),
                ReflectionHelper.ResolveType("(int, string)"));
        }

        [Fact]
        public void ArraysOfAndInGenericsResolve()
        {
            Configure();
            Assert.Same(typeof(List<int>[]),
                ReflectionHelper.ResolveType("List<int>[]", "System.Collections.Generic"));
            Assert.Same(typeof(List<int[]>),
                ReflectionHelper.ResolveType("List<int[]>", "System.Collections.Generic"));
            Assert.Same(typeof(int[][]),
                ReflectionHelper.ResolveType("int[][]"));
        }

        [Fact]
        public void GenericOuterWithNonGenericNestedResolves()
        {
            Configure();
            Assert.Same(typeof(GenOuter<int>.Inner),
                ReflectionHelper.ResolveType("Heddle.Tests.GenericTypeResolutionTests.GenOuter<int>.Inner"));
            Assert.Same(typeof(GenOuter<int>.Inner),
                ReflectionHelper.ResolveType("GenericTypeResolutionTests.GenOuter<int>.Inner", "Heddle.Tests"));
        }

        [Fact]
        public void GenericOuterWithGenericNestedResolves()
        {
            Configure();
            // CLR nested types inherit outer generic parameters: InnerGen`1 has 2 generic
            // arguments total, closed left-to-right as [int, string].
            Assert.Same(typeof(GenOuter<int>.InnerGen<string>),
                ReflectionHelper.ResolveType("Heddle.Tests.GenericTypeResolutionTests.GenOuter<int>.InnerGen<string>"));
        }

        [Fact]
        public void DottedNestedNameAsGenericArgumentResolves()
        {
            Configure();
            Assert.Same(typeof(List<Plain>),
                ReflectionHelper.ResolveType("List<Heddle.Tests.GenericTypeResolutionTests.Plain>",
                    "System.Collections.Generic"));
        }

        [Fact]
        public void GenericArityMismatchThrows()
        {
            Configure();
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("Heddle.Tests.GenericTypeResolutionTests.GenOuter<int, int>.Inner"));
        }

        [Fact]
        public void UnbalancedAngleBracketsThrow()
        {
            Configure();
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("List<int", "System.Collections.Generic"));
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("List<List<int>", "System.Collections.Generic"));
        }

        [Fact]
        public void EmptyGenericArgumentThrows()
        {
            Configure();
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("List<>", "System.Collections.Generic"));
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("Dictionary<int,>", "System.Collections.Generic"));
        }

        [Fact]
        public void WhitespaceInsideArgumentListIsTolerated()
        {
            Configure();
            Assert.Same(typeof(Dictionary<string, int>),
                ReflectionHelper.ResolveType("Dictionary< string , int >", "System.Collections.Generic"));
            Assert.Same(typeof(Dictionary<string, int>),
                ReflectionHelper.ResolveType("Dictionary<string,int>", "System.Collections.Generic"));
        }

        [Fact]
        public void DeepJaggedArraysResolve()
        {
            Configure();
            Assert.Same(typeof(int[][][]), ReflectionHelper.ResolveType("int[][][]"));
            // Whitespace between suffix pairs is tolerated (each pair is trimmed as it is peeled).
            Assert.Same(typeof(int[][]), ReflectionHelper.ResolveType("int[] []"));
        }

        [Fact]
        public void StrayClosingBracketIsNotAnArraySpelling()
        {
            Configure();
            // Ends with ']' but not with an "[]" pair — falls through to simple resolution, which fails.
            Assert.Throws<InvalidOperationException>(() =>
                ReflectionHelper.ResolveType("Heddle.Tests.GenericTypeResolutionTests.Plain]"));
        }

        [Fact]
        public void ArrayOfGenericOuterNestedTypeResolves()
        {
            Configure();
            Assert.Same(typeof(GenOuter<int>.Inner[]),
                ReflectionHelper.ResolveType("Heddle.Tests.GenericTypeResolutionTests.GenOuter<int>.Inner[]"));
        }

        [Fact]
        public void NestedTupleResolves()
        {
            Configure();
            Assert.Same(typeof(((int, string), int)),
                ReflectionHelper.ResolveType("((int, string), int)"));
            Assert.Same(typeof(ValueTuple<int, Plain>),
                ReflectionHelper.ResolveType("(int, Heddle.Tests.GenericTypeResolutionTests.Plain)"));
        }

        [Fact]
        public void ComplexCompositionResolves()
        {
            Configure();
            Assert.Same(typeof(GenOuter<List<int[]>>.InnerGen<(int, string)>),
                ReflectionHelper.ResolveType(
                    "Heddle.Tests.GenericTypeResolutionTests.GenOuter<List<int[]>>.InnerGen<(int, string)>",
                    "System.Collections.Generic"));
        }

        [Fact]
        public void DefinitionModelClauseResolvesGenericModelType()
        {
            HeddleTemplate.Configure(typeof(GenericTypeResolutionTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(
                "@% <rows>{{<ul>@list(this){{<li>@(Cell)</li>}}</ul>}} :: System.Collections.Generic.List<Heddle.Tests.GenericTypeResolutionTests.Row> %@\n@rows(this)",
                new CompileContext(new TemplateOptions(), typeof(List<Row>)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("<ul><li>a</li><li>b</li></ul>",
                t.Generate(new List<Row> { new Row { Cell = "a" }, new Row { Cell = "b" } }).Trim());
        }
    }
}
