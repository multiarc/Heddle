using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;
using Templates.Runtime;
using Templates.Extensions;
using Templates.Strings.Core;

namespace UnitTesting.TemplateTests {
    [TestClass]
    public class GuidTemplateTest {
        private GuidExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new GuidExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate("X", typeof (Guid), null, new CompileContext(new TemplateOptions()));
            Guid value = Guid.NewGuid();
            ExString expected = value.ToString("X");
            ExString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("<%GuidFormat%>", typeof (Guid), typeof (TestData), new CompileContext(new TemplateOptions()));
            value = Guid.NewGuid();
            var testData = new TestData
            {
                GuidFormat = "X"
            };
            expected = value.ToString(testData.GuidFormat);
            actual = _target.ProcessData(value, testData).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate("", typeof (Guid), null, new CompileContext(new TemplateOptions()));
            value = Guid.NewGuid();
            expected = value.ToString();
            actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
        }

        #region Nested type: TestData

        private class TestData {
            public string GuidFormat
            {
                get;
                set;
            }
        }

        #endregion
    }
}