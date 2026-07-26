using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Heddle.Tests.Data;
using Xunit;
using Heddle.TestCorpus;

namespace Heddle.Tests
{
    public class HeddleTemplateTests
    {
        public class OrderTest
        {
            public int Id { get; set; }
        }
        
        [Fact]
        public void WierdWhiteSpace()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            
            using var ttlTemplate = new HeddleTemplate();
            var results = ttlTemplate.TryCompilation(File.ReadAllText("TestTemplate/wierd-whitespace.heddle").Replace("\r\n", "\n"), new TemplateOptions
            {
                ExpressionMode = ExpressionMode.FullCSharp
            });
            
            Assert.True(results.Success, results.ToString());
        }

        /// <summary>
        /// Regression: nested parentheses in C# expressions must classify identically in named (<c>@x(@Foo(1))</c>)
        /// and unnamed (<c>@(@Foo(1))</c>) call forms.
        /// </summary>
        [Fact]
        public void NamedAndUnnamedCSharpCallsClassifyParenTokensEqually()
        {
            int CSharpTokenCount(string template)
            {
                var context = DocumentParser.Parse(template, new CompileContext(new TemplateOptions
                {
                    ProvideLanguageFeatures = true
                }), out _);
                Assert.Empty(context.Errors);
                return context.Tokens.Count(t => t.HeddleTokenType == HeddleTokenType.CSharpToken);
            }

            var named = CSharpTokenCount("@x(@Foo(1) + 2)tail");
            var unnamed = CSharpTokenCount("@(@Foo(1) + 2)tail");
            var namedNoNesting = CSharpTokenCount("@x(@Foo + 2)tail");

            Assert.Equal(unnamed, named);
            Assert.True(named > namedNoNesting, $"expected nested parens to add C# tokens: {named} vs {namedNoNesting}");
        }

        /// <summary>Digit separators, binary literals, and hex with separators must tokenize and compile.</summary>
        [Fact]
        public void ModernNumericLiteralsCompile()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var target = new HeddleTemplate("@(@ 1_000 + 0b1010 + 0xFF_FF )",
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp }));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            Assert.Equal("66545", target.Generate(null));
        }

        /// <summary>String interpolation with simple holes, parentheses, and nested literals must lex and render.</summary>
        [Fact]
        public void InterpolatedStringExpressions()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(
                "@(@$\"x{1 + 2}y z{(1 < 2 ? \"a\" : \"b\")} (p{(3 * 4)})\")",
                new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            var actual = target.Generate(null);
            Assert.Equal("x3y za (p12)", actual);
        }

        /// <summary>Brace escapes per C# spec (§12.8.3): '{{' and '}}' are literal braces, not hole delimiters.</summary>
        [Fact]
        public void InterpolatedStringBraceEscapes()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

            var regular = new HeddleTemplate("@(@$\"a{{b}}c{2 + 3}\")", new CompileContext(options));
            Assert.True(regular.CompileResult.Success, regular.CompileResult.ToString());
            Assert.Equal("a{b}c5", regular.Generate(null));

            var verbatim = new HeddleTemplate("@(@$@\"p{{q}}r{4 * 2}\")", new CompileContext(options));
            Assert.True(verbatim.CompileResult.Success, verbatim.CompileResult.ToString());
            Assert.Equal("p{q}r8", verbatim.Generate(null));
        }

        /// <summary>A C# expression call at end-of-input must parse, including nested parentheses as the final tokens.</summary>
        [Fact]
        public void CSharpCallAtEndOfInput()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

            var simple = new HeddleTemplate("@(@5)", new CompileContext(options));
            Assert.True(simple.CompileResult.Success, simple.CompileResult.ToString());
            Assert.Equal("5", simple.Generate(null));

            var nested = new HeddleTemplate("@(@(2 + 3) * 4)", new CompileContext(options));
            Assert.True(nested.CompileResult.Success, nested.CompileResult.ToString());
            Assert.Equal("20", nested.Generate(null));
        }

        /// <summary>Verbatim identifiers ('@' prefix) must lex and round-trip to Roslyn.</summary>
        [Fact]
        public void VerbatimIdentifierExpressions()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

            var plain = new HeddleTemplate("@(@@System.Int32.MaxValue)", new CompileContext(options));
            Assert.True(plain.CompileResult.Success, plain.CompileResult.ToString());
            Assert.Equal("2147483647", plain.Generate(null));

            var withCall = new HeddleTemplate("@(@@System.Math.Max(2, 3))", new CompileContext(options));
            Assert.True(withCall.CompileResult.Success, withCall.CompileResult.ToString());
            Assert.Equal("3", withCall.Generate(null));
        }

        /// <summary>Statement-bodied lambdas must compile despite nested return statements in the wrapper.</summary>
        [Fact]
        public void StatementLambdaExpressions()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

            var blockLambda = new HeddleTemplate(
                "@(@new System.Func<int>(() => { return 7; })())",
                new CompileContext(options));
            Assert.True(blockLambda.CompileResult.Success, blockLambda.CompileResult.ToString());
            Assert.Equal("7", blockLambda.Generate(null));

            var twoLambdas = new HeddleTemplate(
                "@(@new System.Func<int>(() => { if (true) return 2; return 0; })() + new System.Func<int>(() => { return 3; })())",
                new CompileContext(options));
            Assert.True(twoLambdas.CompileResult.Success, twoLambdas.CompileResult.ToString());
            Assert.Equal("5", twoLambdas.Generate(null));
        }

        /// <summary>Raw string literals (C# 11) and UTF-8 suffix must tokenize as single tokens.</summary>
        [Fact]
        public void RawStringAndUtf8Literals()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);

            void NoErrors(string template)
            {
                var ctx = DocumentParser.Parse(template,
                    new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
                Assert.Empty(ctx.Errors);
            }
            NoErrors("@(@\"abc\"u8.Length)");
            NoErrors("@(@\"\"\"(\"\"\".Length)");
            NoErrors("@(@\"\"\"\"a\"\"\"b\"\"\"\".Length)");
            NoErrors("@(@$\"\"\"x{1 + 2}y\"\"\")");
            NoErrors("@(@" + new string('"', 7) + "x" + new string('"', 7) + ".Length)");
            NoErrors("@(@" + new string('"', 20) + "x" + new string('"', 20) + ".Length)");
            NoErrors("@(@" + new string('"', 5) + "a" + new string('"', 4) + "b" + new string('"', 5) + ".Length)");

#if NET6_0_OR_GREATER
            // C# 11 evaluation requires Roslyn support available only on net6.0+.
            var options = new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };
            string Render(string template)
            {
                var t = new HeddleTemplate(template, new CompileContext(options));
                Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
                return t.Generate(null);
            }

            Assert.Equal("3", Render("@(@\"abc\"u8.Length)"));
            Assert.Equal("3", Render("@(@\"\"\"abc\"\"\".Length)"));
            Assert.Equal("3", Render("@(@\"\"\"a\"b\"\"\".Length)"));
            Assert.Equal("1", Render("@(@\"\"\"(\"\"\".Length)"));
            Assert.Equal("5", Render("@(@\"\"\"\"a\"\"\"b\"\"\"\".Length)"));
            Assert.Equal("x3y", Render("@(@$\"\"\"x{1 + 2}y\"\"\")"));
            Assert.Equal("3", Render("@(@\"\"\"\nabc\n\"\"\".Length)"));
            Assert.Equal("3", Render("@(@" + new string('"', 7) + "abc" + new string('"', 7) + ".Length)"));
            Assert.Equal("6", Render("@(@" + new string('"', 5) + "a" + new string('"', 4) + "b" + new string('"', 5) + ".Length)"));
#endif
        }

        /// <summary>Per C# spec (§6.4.3): Unicode identifiers are valid, and '+' is not absorbed into identifiers.</summary>
        [Fact]
        public void IdentifierAndOperatorCoverage()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);

            void NoErrors(string template)
            {
                var ctx = DocumentParser.Parse(template,
                    new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
                Assert.Empty(ctx.Errors);
            }
            NoErrors("@(@café.ToString())");
            NoErrors("@(@Δλ + naïve)");

            // '+' between identifiers must be a separate operator, not part of an identifier.
            var t = new HeddleTemplate("@(@\"x\".Length+1)",
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp }));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("2", t.Generate(null));

            var nameofT = new HeddleTemplate("@(@nameof(System.String))",
                new CompileContext(new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp }));
            Assert.True(nameofT.CompileResult.Success, nameofT.CompileResult.ToString());
            Assert.Equal("String", nameofT.Generate(null));
        }

        /// <summary>The C# 13 '\e' escape must tokenize without error.</summary>
        [Fact]
        public void EscapeSequenceLexes()
        {
            var ctx = DocumentParser.Parse("@(@\"\\e\".Length)X",
                new CompileContext(new TemplateOptions { ProvideLanguageFeatures = true }), out _);
            Assert.Empty(ctx.Errors);
        }

        [Fact]
        public void SubjectDynamicTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate("@%" + Environment.NewLine + "<default> -> (Model)" + Environment.NewLine +
                                         "{{ Order #@(Id)! }} :: dynamic" + Environment.NewLine + "%@",
                new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            dynamic model = new ExpandoObject();
            model.Model = new OrderTest
            {
                Id = 100
            };
            var actual = target.Generate(model);
            var expected = " Order #100! ";
            Assert.Equal(expected, actual);
        }
        
        [Fact]
        public void RawDocumentTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("raw")
            {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplate",
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            var actual = target.Generate(null);
            using (var writer = File.CreateText(TestCorpusIndex.WrittenArtifactPath("test-raw-document.html")))
            {
                writer.Write(actual);
            }
            string expected;
            using (StreamReader reader = File.OpenText(@"TestTemplate/generated-raw-document.html"))
            {
                expected = reader.ReadToEnd();
            }
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void EmptyOptimizedDocumentTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("optimized-document")
            {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplate",
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            var actual = target.Generate(null);
            using (var writer = File.CreateText(TestCorpusIndex.WrittenArtifactPath("test-optimized-document.html")))
            {
                writer.Write(actual);
            }
            string expected;
            using (StreamReader reader = File.OpenText(@"TestTemplate/generated-optimized-document.html"))
            {
                expected = reader.ReadToEnd();
            }
            Assert.Equal(expected, actual);
        }
        
        [Fact]
        public void TupleDocumentTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("tuple_array")
            {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplate",
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            var actual = target.Generate(new[]
            {
                new NameValuePair
                {
                    Name = "Name_Test",
                    Value = "Value_Test"
                },
                new NameValuePair
                {
                    Name = "Name_Test2",
                    Value = "Value_Test2"
                }
            });
            using (var writer = File.CreateText(TestCorpusIndex.WrittenArtifactPath("test-tuple_array.html")))
            {
                writer.Write(actual);
            }
            string expected;
            using (StreamReader reader = File.OpenText(@"TestTemplate/generated-tuple_array.html"))
            {
                expected = reader.ReadToEnd();
            }
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void EmptyOverrideTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("empty-override")
            {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplate",
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            List<Category> testList = new List<Category>
            {
                new Category
                {
                    ComplexObject = new ComplexObject
                    {
                        Data = new TestDataStructure
                        {
                            Text = "TEST1"
                        }
                    },
                    Name = "test1",
                    SubCategories = new List<Category>
                    {
                        new Category
                        {
                            ComplexObject = new ComplexObject
                            {
                                Data = new TestDataStructure
                                {
                                    Text = "TEST2"
                                }
                            },
                            Name = "test2",
                            SubCategories = new List<Category>
                            {
                                new Category
                                {
                                    Name = "test3"
                                },
                                new Category
                                {
                                    Name = "test7"
                                }
                            }
                        }
                    }
                },
                new Category
                {
                    Name = "test4",
                    SubCategories = new List<Category>
                    {
                        new Category
                        {
                            ComplexObject = new ComplexObject
                            {
                                Data = new TestDataStructure()
                            },
                            Name = "test5",
                            SubCategories = new List<Category>
                            {
                                new Category
                                {
                                    ComplexObject = new ComplexObject(),
                                    Name = "test6"
                                },
                                new Category
                                {
                                    Name = "test8"
                                },
                            }
                        }
                    }
                }
            };
            dynamic model = new ExpandoObject();
            model.Model = testList;
            var actual = target.Generate(model);
            using (var writer = File.CreateText(TestCorpusIndex.WrittenArtifactPath("test-empty-override.html")))
            {
                writer.Write(actual);
            }
            string expected;
            using (StreamReader reader = File.OpenText(@"TestTemplate/generated-empty-override.html"))
            {
                expected = reader.ReadToEnd();
            }
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void RecursionGenerateTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("recursion")
            {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplate",
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            List<Category> testList = new List<Category>
            {
                new Category
                {
                    ComplexObject = new ComplexObject
                    {
                        Data = new TestDataStructure
                        {
                            Text = "TEST1"
                        }
                    },
                    Name = "test1",
                    SubCategories = new List<Category>
                    {
                        new Category
                        {
                            ComplexObject = new ComplexObject
                            {
                                Data = new TestDataStructure
                                {
                                    Text = "TEST2"
                                }
                            },
                            Name = "test2",
                            SubCategories = new List<Category>
                            {
                                new Category
                                {
                                    Name = "test3"
                                },
                                new Category
                                {
                                    Name = "test7"
                                }
                            }
                        }
                    }
                },
                new Category
                {
                    Name = "test4",
                    SubCategories = new List<Category>
                    {
                        new Category
                        {
                            ComplexObject = new ComplexObject
                            {
                                Data = new TestDataStructure()
                            },
                            Name = "test5",
                            SubCategories = new List<Category>
                            {
                                new Category
                                {
                                    ComplexObject = new ComplexObject(),
                                    Name = "test6"
                                },
                                new Category
                                {
                                    Name = "test8"
                                },
                            }
                        }
                    }
                }
            };
            var actual = target.Generate(testList);
            using (var writer = File.CreateText(TestCorpusIndex.WrittenArtifactPath("test-recursion.html")))
            {
                writer.Write(actual);
            }
            string expected;
            using (StreamReader reader = File.OpenText(@"TestTemplate/generated-recursion.html"))
            {
                expected = reader.ReadToEnd();
            }
            Assert.Equal(expected, actual);
        }
        
        [Fact()]
        public void DynamicRecursionGenerateTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("dynamic-recursion")
            {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplate",
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            List<DynamicCategory> testList = new List<DynamicCategory>
            {
                new DynamicCategory
                {
                    ComplexObject = new DynamicComplexObject
                    {
                        Data = new DynamicTestDataStructure
                        {
                            Text = "TEST1"
                        }
                    },
                    Name = "test1",
                    SubCategories = new List<dynamic>
                    {
                        new DynamicCategory
                        {
                            ComplexObject = new DynamicComplexObject
                            {
                                Data = new DynamicTestDataStructure
                                {
                                    Text = "TEST2"
                                }
                            },
                            Name = "test2",
                            SubCategories = new List<dynamic>
                            {
                                new DynamicCategory
                                {
                                    Name = "test3"
                                },
                                new DynamicCategory
                                {
                                    Name = "test7"
                                }
                            }
                        }
                    }
                },
                new DynamicCategory
                {
                    Name = "test4",
                    SubCategories = new List<dynamic>
                    {
                        new DynamicCategory
                        {
                            ComplexObject = new DynamicComplexObject
                            {
                                Data = new DynamicTestDataStructure()
                            },
                            Name = "test5",
                            SubCategories = new List<dynamic>
                            {
                                new DynamicCategory
                                {
                                    ComplexObject = new DynamicComplexObject(),
                                    Name = "test6"
                                },
                                new DynamicCategory
                                {
                                    Name = "test8"
                                },
                            }
                        }
                    }
                }
            };
            var actual = target.Generate(testList);
            using (var writer = File.CreateText(TestCorpusIndex.WrittenArtifactPath("test-dynamic-recursion.html")))
            {
                writer.Write(actual);
            }
            string expected;
            using (StreamReader reader = File.OpenText(@"TestTemplate/generated-recursion.html"))
            {
                expected = reader.ReadToEnd();
            }
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void VcGenerateTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("vc-test")
            {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplate",
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            // vc-test deliberately combines '<default> -> ()' with two '@default()' by-name calls to pin override
            // layering across three renders — exactly two HED4002 double-render warnings, both naming 'default',
            // and nothing else. The warning does not alter output (the golden below proves it).
            var doubleRenderWarnings = target.Context.CompileWarnings
                .Where(w => w.DiagnosticId == HeddleDiagnosticIds.DefinitionRendersTwice)
                .ToList();
            Assert.Equal(2, doubleRenderWarnings.Count);
            Assert.All(doubleRenderWarnings, w => Assert.StartsWith("Definition 'default' (declared at ", w.Error));
            Assert.Equal(2, doubleRenderWarnings.Select(w => w.Position.StartIndex).Distinct().Count());
            string expected;
            using (StreamReader reader = File.OpenText(@"TestTemplate/generated-vc.html"))
            {
                expected = reader.ReadToEnd();
            }
            var actual = target.Generate(null);
            using (var writer = File.CreateText(TestCorpusIndex.WrittenArtifactPath("test-vc.html")))
            {
                writer.Write(actual);
            }
            Assert.Equal(expected, actual);
        }

        [Fact()]
        public void GenerateTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("template")
            {
                FileNamePostfix = ".heddle",
                RootPath = @"TestTemplate",
                ExpressionMode = ExpressionMode.FullCSharp
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());

            var products = new List<TestListItem>
            {
                new TestListItem
                {
                    Cost = 1024m,
                    Locale = "en-us",
                    Name = "Name 1",
                    Quantity = 14509
                },
                new TestListItem
                {
                    Cost = 90008880m,
                    Locale = "ru-ru",
                    Name = "Name 2",
                    Quantity = 1609
                },
                new TestListItem
                {
                    Cost = 7800m,
                    Locale = "de-DE",
                    Name = "Name 3",
                    Quantity = 160921
                },
                new TestListItem
                {
                    Cost = 70m,
                    Locale = "de-DE",
                    Name = "Name 4",
                    Quantity = 1609709
                },
                new TestListItem
                {
                    Cost = 7000m,
                    Locale = "de-DE",
                    Name = "Name 4",
                    Quantity = 20
                }
            };
            var data = new TestData
            {
                Products = products,
                Date = new DateTime(2012, 4, 2, 5, 14, 12),
                FuckingInt = 1059,
                IsShow = true,
                Guid = Guid.Parse("{3E55A9AF-0031-4C54-B836-527EAB26867B}"),
                Text = "SOME TEXT"
            };
            StreamReader reader;
            if (Type.GetType("Mono.Runtime") != null)
            {
                reader = File.OpenText(@"TestTemplate/generated_mono.html");
            }
            else
            {
                reader = File.OpenText(@"TestTemplate/generated.html");
            }

            var expected = reader.ReadToEnd();
            reader.Dispose();
            var actual = target.Generate(data);
            var writer = File.CreateText(TestCorpusIndex.WrittenArtifactPath("test.html"));
            writer.Write(actual);
            writer.Dispose();
            Assert.Equal(expected, actual);
        }
    }
}