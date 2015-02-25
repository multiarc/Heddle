using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings;

namespace Templater.Tests.TemplateTests {
    [TestClass]
    public class InnerTest {
        private InnerExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new InnerExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate("<%Str%>=<%Num%>", typeof (TestData), null, new CompileContext(new TemplateOptions()));
            var value = new TestData
            {
                Num = 10,
                Str = "TEST DATA"
            };
            ExString expected = "TEST DATA=10";
            ExString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            expected = "=";
            actual = _target.ProcessData(null, null).ToString();
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