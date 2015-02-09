using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings.Core;

namespace Templater.Tests.TemplateTests {
    [TestClass]
    public class MoneyTest {
        private MoneyExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new MoneyExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate("", typeof (decimal), null, new CompileContext(new TemplateOptions()));
            const decimal value = 98.25m;
            ExString expected = value.ToString("c", CultureInfo.InvariantCulture);
            ExString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("en-us", typeof (decimal), null, new CompileContext(new TemplateOptions()));
            expected = value.ToString("c", new CultureInfo("en-us"));
            actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("<%Locale%>", typeof (decimal), typeof (TestData), new CompileContext(new TemplateOptions()));
            var testData = new TestData
            {
                Locale = "ru-ru"
            };
            expected = value.ToString("c", new CultureInfo(testData.Locale));
            actual = _target.ProcessData(value, testData).ToString();
            Assert.AreEqual(expected, actual);
            const int value2 = 98;
            _target.InitializeInnerTemplate("ru-ru", typeof (int), null, new CompileContext(new TemplateOptions()));
            expected = ((decimal) value2).ToString("c", new CultureInfo("ru-ru"));
            actual = _target.ProcessData(value2, testData).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestData

        private class TestData {
            public string Locale
            {
                get;
                set;
            }
        }

        #endregion
    }
}