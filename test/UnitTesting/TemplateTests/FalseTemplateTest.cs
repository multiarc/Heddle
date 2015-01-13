using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings.Core;

namespace UnitTesting.TemplateTests {
    [TestClass]
    public class FalseTemplateTest {
        private FalseExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new FalseExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate("<tag></tag>", typeof (bool), null, new CompileContext(new TemplateOptions()));
            ExString expected = "<tag></tag>";
            ExString actual = _target.ProcessData(false, null).ToString();
            Assert.AreEqual(expected, actual);
            expected = "";
            actual = _target.ProcessData(true, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("<%Str%>=<%Num%>", typeof (bool), typeof (TestType), new CompileContext(new TemplateOptions()));
            var testData = new TestType
            {
                Num = 160,
                Str = "TEST DATA"
            };
            expected = "TEST DATA=160";
            actual = _target.ProcessData(false, testData).ToString();
            Assert.AreEqual(expected, actual);
            expected = "";
            actual = _target.ProcessData(false, null).ToString();
            Assert.AreEqual(expected, actual);
            actual = _target.ProcessData(true, testData).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestType

        private class TestType {
            public int Num
            {
                get;
                set;
            }

            public string Str
            {
                get;
                set;
            }
        }

        #endregion
    }
}