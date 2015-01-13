using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Core.Data;
using Templates.Extensions;
using Templates.Runtime;
using Templates.Strings.Core;

namespace UnitTesting.TemplateTests {
    [TestClass]
    public class SystemTemplateTest {
        private SystemExtension _target;

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize]
        public void Init ()
        {
            _target = new SystemExtension();
        }

        [TestMethod]
        public void ProcessDataTest ()
        {
            _target.InitializeInnerTemplate("", null, null, new CompileContext(new TemplateOptions()));
            FastString expected = "";
            FastString actual = _target.ProcessData(null, null).ToString();
            Assert.AreEqual(expected, actual);
            _target.InitializeInnerTemplate(@"[]/\", null, null, new CompileContext(new TemplateOptions()));
            expected = "{}%><%";
            actual = _target.ProcessData(null, null).ToString();
            Assert.AreEqual(expected, actual);
        }
    }
}