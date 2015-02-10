using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templater.Tests {
    [TestClass]
    public class StringBuilderExTest {
        private static Random _rnd;

        public TestContext TestContext
        {
            get;
            set;
        }

        private static string GenerateString (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) _rnd.Next(1, 0x10000);
            return result;
        }

        private static ExString GenerateStringEx (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) _rnd.Next(1, 0x10000);
            return new ExString(result);
        }

        [TestInitialize]
        public void MyTestInitialize ()
        {
            _rnd = new Random(DateTime.Now.Millisecond);
        }

        [TestMethod]
        public void StringBuilderExConstructorTest ()
        {
            var target = new ExStringBuilder((ExString) null);
            Assert.AreEqual(target.Length, 0);
            ExString value = GenerateStringEx(50);
            target = new ExStringBuilder(value);
            Assert.AreEqual(target.Length, 50);
            Assert.AreEqual(target.ToExString(), value);
        }

        [TestMethod]
        public void StringBuilderExConstructorTest1 ()
        {
            var target = new ExStringBuilder();
            Assert.AreEqual(target.Length, 0);
            Assert.AreEqual(target.ToExString(), ExString.Empty);
        }

        [TestMethod]
        public void StringBuilderExConstructorTest2 ()
        {
            var target = new ExStringBuilder((string) null);
            Assert.AreEqual(target.Length, 0);
            string value = GenerateString(50);
            target = new ExStringBuilder(value);
            Assert.AreEqual(50, target.Length);
            Assert.AreEqual(value, target.ToString());
            Assert.AreEqual(new ExString(value), target.ToExString());
        }

        [TestMethod]
        public void ToStringExTest ()
        {
            ExString expected = GenerateStringEx(10);
            var target = new ExStringBuilder(expected);
            ExString append1 = GenerateStringEx(20);
            ExString append2 = GenerateStringEx(20);
            target.Append(append1);
            target.Append(append2);
            expected += append1 + append2;
            ExString actual = target.ToExString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(target.Length, 50);
        }

        [TestMethod]
        public void ClearTest ()
        {
            var target = new ExStringBuilder(GenerateStringEx(20));
            target.Append(GenerateStringEx(10));
            target.Append(GenerateStringEx(20));
            target.Clear();
            Assert.AreEqual(target.ToExString(), ExString.Empty);
            Assert.AreEqual(target.Length, 0);
            target = new ExStringBuilder(GenerateStringEx(20));
            target.Append(GenerateStringEx(10));
            target.Append(GenerateStringEx(20));
            target.ToExString();
            target.Clear();
            Assert.AreEqual(target.ToExString(), ExString.Empty);
            Assert.AreEqual(target.Length, 0);
        }

        [TestMethod]
        public void AppendTest ()
        {
            ExString expected = GenerateStringEx(10);
            var target = new ExStringBuilder(expected);
            ExString append1 = GenerateStringEx(20);
            ExString append2 = GenerateStringEx(20);
            target.Append(append1);
            target.Append(append2);
            expected += append1 + append2;
            Assert.AreEqual(target.Length, 50);
            ExString actual = target.ToExString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(target.Length, 50);
        }

        [TestMethod]
        public void AppendTest1 ()
        {
            string expected = GenerateString(10);
            var target = new ExStringBuilder(expected);
            string append1 = GenerateString(20);
            string append2 = GenerateString(20);
            target.Append(append1);
            target.Append(append2);
            expected += append1 + append2;
            Assert.AreEqual(target.Length, 50);
            string actual = target.ToString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(target.Length, 50);
        }

        [TestMethod]
        public void ToStringTest ()
        {
            var target = new ExStringBuilder(); // TODO: Initialize to an appropriate value
            string expected = string.Empty; // TODO: Initialize to an appropriate value
            string actual = target.ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void LengthTest ()
        {
            var target = new ExStringBuilder(GenerateStringEx(20));
            int actual = target.Length;
            Assert.AreEqual(actual, 20);
        }
    }
}