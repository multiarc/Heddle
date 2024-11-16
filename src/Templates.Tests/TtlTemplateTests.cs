using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Reflection;
using Templates.Data;
using Templates.Runtime;
using Templates.Tests.Data;
using Xunit;

namespace Templates.Tests
{
    public class TtlTemplateTests
    {
        public class OrderTest
        {
            public int Id { get; set; }
        }
        
        [Fact]
        public void WierdWhiteSpace()
        {
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            
            using var ttlTemplate = new TtlTemplate();
            var results = ttlTemplate.TryCompilation(File.ReadAllText("TestTemplate/wierd-whitespace.thtml").Replace("\r\n", "\n"), new TemplateOptions
            {
                AllowCSharp = true,
                ForceRemoveWhitespace = true
            });
            
            Assert.True(results.Success, results.ToString());
        }

        [Fact]
        public void SubjectDynamicTest()
        {
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions
            {
                AllowCSharp = true
            };
            var target = new TtlTemplate("@%" + Environment.NewLine + "<default> -> (Model)" + Environment.NewLine +
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
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("raw")
            {
                FileNamePostfix = ".thtml",
                RootPath = @"TestTemplate",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));
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
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("optimized-document")
            {
                FileNamePostfix = ".thtml",
                RootPath = @"TestTemplate",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));
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
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("tuple_array")
            {
                FileNamePostfix = ".thtml",
                RootPath = @"TestTemplate",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));
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
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("empty-override")
            {
                FileNamePostfix = ".thtml",
                RootPath = @"TestTemplate",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));
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
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("recursion")
            {
                FileNamePostfix = ".thtml",
                RootPath = @"TestTemplate",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));
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
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("dynamic-recursion")
            {
                FileNamePostfix = ".thtml",
                RootPath = @"TestTemplate",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));
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
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("vc-test")
            {
                FileNamePostfix = ".thtml",
                RootPath = @"TestTemplate",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));
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
            TtlTemplate.Configure(typeof(TtlTemplateTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions("template")
            {
                FileNamePostfix = ".thtml",
                RootPath = @"TestTemplate",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));
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