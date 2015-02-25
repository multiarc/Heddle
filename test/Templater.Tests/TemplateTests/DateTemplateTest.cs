using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings;

namespace Templater.Tests.TemplateTests {
    [TestClass]
    public class DateTemplateTest {
        private DateExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new DateExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitStart(null, typeof (DateTime), null, new CompileContext(new TemplateOptions()));
            DateTime value = DateTime.Now;
            ExString expected = value.ToString("d", CultureInfo.InvariantCulture);
            ExString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitStart("d", typeof (DateTime), null, new CompileContext(new TemplateOptions()));
            value = DateTime.Now;
            expected = value.ToString("d", CultureInfo.InvariantCulture);
            actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitStart("t", typeof (DateTime), null, new CompileContext(new TemplateOptions()));
            value = DateTime.Now;
            expected = value.ToString("t", CultureInfo.InvariantCulture);
            actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitStart("<%DateFormat%>", typeof (DateTime), typeof (TestData), new CompileContext(new TemplateOptions()));
            var testData = new TestData
            {
                DateFormat = "yyyy-mm-dd"
            };
            value = DateTime.Now;
            expected = value.ToString(testData.DateFormat, CultureInfo.InvariantCulture);
            actual = _target.ProcessData(value, testData).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestData

        private class TestData {
            public string DateFormat
            {
                get;
                set;
            }
        }

        #endregion
    }
}