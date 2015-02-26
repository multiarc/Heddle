using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates;
using Templates.Data;
using Templates.Runtime;
using Templates.Strings;

namespace Templater.Tests {
    [TestClass]
    public class TemplateTest {
        private TtlTemplate _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            var options = new TemplateOptions
            {
                FileNamePostfix = ".thtml",
                RootPath = @"..\..\TestTemplate",
                TemplateName = "template"
            };
            _target = new TtlTemplate(new CompileContext(options));
        }

        [TestMethod]
        public void GenerateStringTest ()
        {
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
            ExString expected = reader.ReadToEnd();
            reader.Close();
            ExString actual = _target.Generate(data);
            //var writer = File.CreateText(@"d:\Tmp\generated.html");
            //writer.Write((string)actual);
            //writer.Close();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestData

        private class TestData: TestDataStructure {
            public override List<TestListItem> Products
            {
                get { return ProductsCollection.Where(p => p.Cost > 1000).ToList(); }
                set { ProductsCollection = value; }
            }
        }

        #endregion

        #region Nested type: TestDataStructure

        private class TestDataStructure {
            protected List<TestListItem> ProductsCollection;

            public virtual List<TestListItem> Products
            {
                get { return ProductsCollection; }
                set { ProductsCollection = value; }
            }

            public DateTime Date
            {
                get;
                set;
            }

            public bool IsShow
            {
                get;
                set;
            }

            public string Text
            {
                get;
                set;
            }

            public int FuckingInt
            {
                get;
                set;
            }

            public Guid Guid
            {
                get;
                set;
            }
        }

        #endregion

        #region Nested type: TestListItem

        public class TestListItem {
            public decimal Cost
            {
                get;
                set;
            }

            public string Name
            {
                get;
                set;
            }

            public int Quantity
            {
                get;
                set;
            }

            public string Locale
            {
                get;
                set;
            }
        }

        #endregion
    }
}