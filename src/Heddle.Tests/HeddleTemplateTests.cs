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
                AllowCSharp = true
            });
            
            Assert.True(results.Success, results.ToString());
        }

        /// <summary>
        /// Regression for the named-call C# token branch: a C# expression containing nested parentheses lexes the
        /// inner ')' as an OUT_PARAMEND token inside the expression. Both the named (e.g. <c>@x(@Foo(1))</c>) and the
        /// unnamed (<c>@(@Foo(1))</c>) call forms must classify those tokens identically. The named branch previously
        /// only iterated CSHARP_TOKEN() and dropped the inner OUT_PARAMEND, producing fewer highlighting tokens than
        /// the unnamed branch.
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

            // The two call forms share the same C# expression, so they must yield the same C# token set.
            Assert.Equal(unnamed, named);
            // The nested '(' '1' ')' (including the inner OUT_PARAMEND) must contribute extra C# tokens.
            Assert.True(named > namedNoNesting, $"expected nested parens to add C# tokens: {named} vs {namedNoNesting}");
        }

        /// <summary>
        /// Tier 1 lexer additions: digit separators (1_000), binary literals (0b1010) and hex with separators
        /// (0xFF_FF) must tokenize without a lexer error and compile/evaluate through Roslyn.
        /// </summary>
        [Fact]
        public void ModernNumericLiteralsCompile()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            // Trailing text after ')' is required: csharp_expression greedily consumes OUT_PARAMEND tokens, so a
            // bare '@(...)' at end-of-input is ambiguous (matches the existing template/test convention).
            var target = new HeddleTemplate("@(@ 1_000 + 0b1010 + 0xFF_FF )!",
                new CompileContext(new TemplateOptions { AllowCSharp = true }));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            // 1000 + 0b1010 (10) + 0xFFFF (65535) = 66545
            Assert.Equal("66545!", target.Generate(null));
        }

        /// <summary>
        /// Tier 2: string interpolation. A simple hole, a hole containing parentheses, and a hole containing a
        /// nested string literal must all lex (no error) and render correctly. The nested-string case is the one
        /// the lexer-mode approach handles that a naive opaque scan would not.
        /// </summary>
        [Fact]
        public void InterpolatedStringExpressions()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                AllowCSharp = true
            };
            // Self-contained C# interpolation: a simple hole ({1 + 2}), a hole containing parentheses and a
            // nested string literal ({(1 < 2 ? "a" : "b")}), and a hole wrapped in parentheses with parens
            // inside ((p{(3 * 4)})). The nested-string case is the one the lexer-mode handling makes work.
            var target = new HeddleTemplate(
                "@(@$\"x{1 + 2}y z{(1 < 2 ? \"a\" : \"b\")} (p{(3 * 4)})\")|END",
                new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            var actual = target.Generate(null);
            Assert.Equal("x3y za (p12)|END", actual);
        }

        [Fact]
        public void SubjectDynamicTest()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                AllowCSharp = true
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
                AllowCSharp = true
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            var actual = target.Generate(null);
            using (var writer = File.CreateText(@"TestTemplate/test-raw-document.html"))
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
                AllowCSharp = true
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            var actual = target.Generate(null);
            using (var writer = File.CreateText(@"TestTemplate/test-optimized-document.html"))
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
                AllowCSharp = true
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
            using (var writer = File.CreateText(@"TestTemplate/test-tuple_array.html"))
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
                AllowCSharp = true
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
            using (var writer = File.CreateText(@"TestTemplate/test-empty-override.html"))
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
                AllowCSharp = true
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
            using (var writer = File.CreateText(@"TestTemplate/test-recursion.html"))
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
                AllowCSharp = true
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
            using (var writer = File.CreateText(@"TestTemplate/test-dynamic-recursion.html"))
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
                AllowCSharp = true
            };
            var target = new HeddleTemplate(new CompileContext(options));
            Assert.True(target.CompileResult.Success, target.CompileResult.ToString());
            string expected;
            using (StreamReader reader = File.OpenText(@"TestTemplate/generated-vc.html"))
            {
                expected = reader.ReadToEnd();
            }
            var actual = target.Generate(null);
            using (var writer = File.CreateText(@"TestTemplate/test-vc.html"))
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
                AllowCSharp = true
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
            var writer = File.CreateText(@"TestTemplate/test.html");
            writer.Write(actual);
            writer.Dispose();
            Assert.Equal(expected, actual);
        }
    }
}