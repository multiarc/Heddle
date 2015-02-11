using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templater.Tests.TemplateTests {
    [TestClass]
    public class PartialTemplateTest {
        private PartialExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new PartialExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            var context = new DocumentContext
                (new TemplateOptions
                {
                    FileNamePostfix = ".ttl",
                    RootPath = @"g:\Work\Templater\test\Templater.Tests\TestTemplate",
                    TemplateName = "partial"
                });
            _target.InitializeInnerTemplate
                ("partial", typeof(TestListItem), null, context);
            context.Compile();
            var value = new TestListItem
            {
                Cost = 1024.25m,
                Locale = "en-us",
                Name = "xxx",
                Quantity = 1050
            };
            ExString expected = "<tr>\r\n    <td>$1,024.25</td>\r\n    <td>1050</td>\r\n    <td>xxx</td>\r\n</tr>";
            ExString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            expected = "";
            actual = _target.ProcessData(null, null).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestListItem

        private class TestListItem {
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