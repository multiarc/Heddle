using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Templates;
using Templates.Data;
using Templates.Runtime;
using Templates.Strings;
using TemplatesXTests.Data;
using Xunit;

namespace TemplatesXTests {
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
        public void GenerateTest() {
            var options = new TemplateOptions
            {
                FileNamePostfix = ".thtml",
                RootPath = @"..\..\TestTemplate",
                TemplateName = "template",
                AllowCSharp = true
            };
            var target = new TtlTemplate(new CompileContext(options));

            var products = new List<TestListItem>();
            products.Add
                (new TestListItem
                {
                    Cost = 1024m,
                    Locale = "en-us",
                    Name = "Name 1",
                    Quantity = 14509
                });
            products.Add
                (new TestListItem
                {
                    Cost = 90008880m,
                    Locale = "ru-ru",
                    Name = "Name 2",
                    Quantity = 1609
                });
            products.Add
                (new TestListItem
                {
                    Cost = 7800m,
                    Locale = "de-DE",
                    Name = "Name 3",
                    Quantity = 160921
                });
            products.Add
                (new TestListItem
                {
                    Cost = 70m,
                    Locale = "de-DE",
                    Name = "Name 4",
                    Quantity = 1609709
                });
            var data = new TestData
            {
                Products = products,
                Date = new DateTime(2012, 4, 2, 5, 14, 12),
                FuckingInt = 1059,
                IsShow = true,
                Guid = Guid.Parse("{3E55A9AF-0031-4C54-B836-527EAB26867B}"),
                Text = "SOME TEXT"
            };
            StreamReader reader = File.OpenText(@"..\..\TestTemplate\generated.html");
            var expected = reader.ReadToEnd();
            reader.Close();
            var actual = target.Generate(data);
            var writer = File.CreateText(@"..\..\TestTemplate\test.html");
            writer.Write(actual);
            writer.Close();
            Assert.Equal(expected, actual);
        }
    }
}