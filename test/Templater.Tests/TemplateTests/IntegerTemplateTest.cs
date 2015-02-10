using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templater.Tests.TemplateTests {
    [TestClass]
    public class IntegerTemplateTest {
        private IntegerExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new IntegerExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate("", typeof (int), null, new CompileContext(new TemplateOptions()));
            const int value = 160;
            ExString expected = value.ToString(CultureInfo.InvariantCulture);
            ExString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("<%IntFormat%>", typeof (int), typeof (TestData), new CompileContext(new TemplateOptions()));
            var testData = new TestData
            {
                IntFormat = "X"
            };
            expected = value.ToString(testData.IntFormat, CultureInfo.InvariantCulture);
            actual = _target.ProcessData(value, testData).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestData

        private class TestData {
            public string IntFormat
            {
                get;
                set;
            }
        }

        #endregion
    }
}