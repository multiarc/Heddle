using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings.Core;

namespace Templater.Tests.TemplateTests {
    [TestClass]
    public class StringTemplateTest {
        private StringExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new StringExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate("", typeof (string), null, new CompileContext(new TemplateOptions()));
            string value = "fdsfasdfdasf";
            ExString expected = value;
            ExString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            value = "";
            expected = value;
            actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            expected = ExString.Empty;
            actual = _target.ProcessData(null, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("<%Str%>=<%Num%>", typeof (string), typeof (TestData), new CompileContext(new TemplateOptions()));
            var testData = new TestData
            {
                Str = "TEST DATA",
                Num = 160
            };
            expected = "TEST DATA=160";
            actual = _target.ProcessData(null, testData).ToString();
            Assert.AreEqual(expected, actual);
            expected = "";
            actual = _target.ProcessData("", testData).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestData

        private class TestData {
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