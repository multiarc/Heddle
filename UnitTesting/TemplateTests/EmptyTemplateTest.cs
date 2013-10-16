using System;
using FastStrings.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Core.CompilerServices;
using Templates.Core.Data;
using Templates.Extensions;

namespace UnitTesting.TemplateTests {
    [TestClass]
    public class EmptyTemplateTest {
        private EmptyExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new EmptyExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate(null, typeof (object), null, new CompileContext(new TemplateOptions()));
            DateTime value = DateTime.Now;
// ReSharper disable SpecifyACultureInStringConversionExplicitly
            FastString expected = value.ToString();
// ReSharper restore SpecifyACultureInStringConversionExplicitly
            FastString actual = _target.ProcessData(value, null).ToString();
            Assert.AreEqual(expected, actual);
            const int value2 = 2;
// ReSharper disable SpecifyACultureInStringConversionExplicitly
            expected = value2.ToString();
// ReSharper restore SpecifyACultureInStringConversionExplicitly
            actual = _target.ProcessData(value2, null).ToString();
            Assert.AreEqual(expected, actual);
            const string value3 = "xxxxx";
            expected = value3;
            actual = _target.ProcessData(value3, null).ToString();
            Assert.AreEqual(expected, actual);
            var value4 = new ListExtension();
            expected = value4.ToString();
            actual = _target.ProcessData(value4, null).ToString();
            Assert.AreEqual(expected, actual);
        }
    }
}