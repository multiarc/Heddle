using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templater.Tests.TemplateTests {
    [TestClass]
    public class ListTemplateTest {
        private Random _rnd;
        private ListExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _rnd = new Random(DateTime.Now.Millisecond);
            _target = new ListExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate("<%Str%>=<%Num%>", typeof (List<TestListItem>), null, new DocumentContext(new TemplateOptions()));
            var value = new List<TestListItem>();
            for (int i = 0; i < 100; i++) {
                value.Add
                    (new TestListItem
                    {
                        Num = _rnd.Next(0, 100),
                        Str = "TEST DATA " + i.ToString(CultureInfo.InvariantCulture)
                    });
            }
            ExString expected = value.Aggregate<TestListItem, ExString>
                ("", (current, item) => current + string.Format("{0}={1}", item.Str, item.Num));
            ExString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            expected = "";
            actual = _target.ProcessData(null, null).ToString();
            Assert.AreEqual(expected, actual);
            expected = "";
            actual = _target.ProcessData(new List<TestListItem>(), null).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestListItem

        private class TestListItem {
            public string Str
            {
                get;
                set;
            }

            public int Num
            {
                get;
                set;
            }
        }

        #endregion
    }
}