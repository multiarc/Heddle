using System;
using System.Collections.Generic;
using System.IO;
using Templates.Data;
using Templates.Runtime;
using Templates.Tests.Data;
using Xunit;

namespace Templates.Tests {
    public class TtlTemplateTests {
        [Fact()]
        public void TtlTemplateTest() {
            
        }

        [Fact()]
        public void TtlTemplateTest1() {
            
        }

        [Fact()]
        public void TtlTemplateTest2() {
            
        }

        [Fact()]
        public void DisposeTest() {
            
        }

        [Fact()]
        public void RecursionGenerateTest()
        {
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
        public void VcGenerateTest()
        {
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
        public void GenerateTest() {
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
            StreamReader reader = File.OpenText(@"TestTemplate/generated.html");
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