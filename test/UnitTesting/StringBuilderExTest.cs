using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Strings.Core;

namespace UnitTesting {
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

        private static FastString GenerateStringEx (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) _rnd.Next(1, 0x10000);
            return new FastString(result);
        }

        [TestInitialize]
        public void MyTestInitialize ()
        {
            _rnd = new Random(DateTime.Now.Millisecond);
        }

        [TestMethod]
        public void StringBuilderExConstructorTest ()
        {
            var target = new FastStringBuilder((FastString) null);
            Assert.AreEqual(target.Length, 0);
            FastString value = GenerateStringEx(50);
            target = new FastStringBuilder(value);
            Assert.AreEqual(target.Length, 50);
            Assert.AreEqual(target.ToFastString(), value);
        }

        [TestMethod]
        public void StringBuilderExConstructorTest1 ()
        {
            var target = new FastStringBuilder();
            Assert.AreEqual(target.Length, 0);
            Assert.AreEqual(target.ToFastString(), FastString.Empty);
        }

        [TestMethod]
        public void StringBuilderExConstructorTest2 ()
        {
            var target = new FastStringBuilder((string) null);
            Assert.AreEqual(target.Length, 0);
            string value = GenerateString(50);
            target = new FastStringBuilder(value);
            Assert.AreEqual(50, target.Length);
            Assert.AreEqual(value, target.ToString());
            Assert.AreEqual(new FastString(value), target.ToFastString());
        }

        [TestMethod]
        public void ToStringExTest ()
        {
            FastString expected = GenerateStringEx(10);
            var target = new FastStringBuilder(expected);
            FastString append1 = GenerateStringEx(20);
            FastString append2 = GenerateStringEx(20);
            target.Append(append1);
            target.Append(append2);
            expected += append1 + append2;
            FastString actual = target.ToFastString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(target.Length, 50);
        }

        [TestMethod]
        public void ClearTest ()
        {
            var target = new FastStringBuilder(GenerateStringEx(20));
            target.Append(GenerateStringEx(10));
            target.Append(GenerateStringEx(20));
            target.Clear();
            Assert.AreEqual(target.ToFastString(), FastString.Empty);
            Assert.AreEqual(target.Length, 0);
            target = new FastStringBuilder(GenerateStringEx(20));
            target.Append(GenerateStringEx(10));
            target.Append(GenerateStringEx(20));
            target.ToFastString();
            target.Clear();
            Assert.AreEqual(target.ToFastString(), FastString.Empty);
            Assert.AreEqual(target.Length, 0);
        }

        [TestMethod]
        public void AppendTest ()
        {
            FastString expected = GenerateStringEx(10);
            var target = new FastStringBuilder(expected);
            FastString append1 = GenerateStringEx(20);
            FastString append2 = GenerateStringEx(20);
            target.Append(append1);
            target.Append(append2);
            expected += append1 + append2;
            Assert.AreEqual(target.Length, 50);
            FastString actual = target.ToFastString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(target.Length, 50);
        }

        [TestMethod]
        public void AppendTest1 ()
        {
            string expected = GenerateString(10);
            var target = new FastStringBuilder(expected);
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
            var target = new FastStringBuilder(); // TODO: Initialize to an appropriate value
            string expected = string.Empty; // TODO: Initialize to an appropriate value
            string actual = target.ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void LengthTest ()
        {
            var target = new FastStringBuilder(GenerateStringEx(20));
            int actual = target.Length;
            Assert.AreEqual(actual, 20);
        }
    }
}