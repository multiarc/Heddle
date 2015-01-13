using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Runtime;
using Templates.Core.Data;
using Templates.Extensions;
using Templates.Strings.Core;

namespace UnitTesting.TemplateTests {
    [TestClass]
    public class TimeTest {
        private TimeExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new TimeExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate(null, typeof (DateTime), null, new CompileContext(new TemplateOptions()));
            DateTime value = DateTime.Now;
            FastString expected = value.ToString("t", CultureInfo.InvariantCulture);
            FastString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("d", typeof (DateTime), null, new CompileContext(new TemplateOptions()));
            value = DateTime.Now;
            expected = value.ToString("d", CultureInfo.InvariantCulture);
            actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("t", typeof (DateTime), null, new CompileContext(new TemplateOptions()));
            value = DateTime.Now;
            expected = value.ToString("t", CultureInfo.InvariantCulture);
            actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("<%TimeFormat%>", typeof (DateTime), typeof (TestData), new CompileContext(new TemplateOptions()));
            var testData = new TestData
            {
                TimeFormat = "yyyy-mm-dd"
            };
            value = DateTime.Now;
            expected = value.ToString(testData.TimeFormat, CultureInfo.InvariantCulture);
            actual = _target.ProcessData(value, testData).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestData

        private class TestData {
            public string TimeFormat
            {
                get;
                set;
            }
        }

        #endregion
    }
}