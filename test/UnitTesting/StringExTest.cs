using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Strings.Core;

namespace UnitTesting {
    [TestClass]
    public class StringExTest {
        public TestContext TestContext
        {
            get;
            set;
        }

        #region Additional test attributes

        private static Random _rnd;

        private static string GenerateString (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) _rnd.Next(30, 0x100);
            return result;
        }

        private static FastString GenerateStringEx (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) _rnd.Next(30, 0x100);
            return new FastString(result);
        }

        [TestInitialize]
        public void MyTestInitialize ()
        {
            _rnd = new Random(DateTime.Now.Millisecond);
        }

        #endregion

        [TestMethod]
        public void FastStringConstructorTest ()
        {
            string value = GenerateString(_rnd.Next(1, 100));
            var target = new FastString(value);
            Assert.AreEqual(value, target.ToString());
            value = string.Empty;
            target = new FastString(value);
            Assert.AreEqual(value, target.ToString());
        }

        [TestMethod]
        public void FastStringConstructorTest1 ()
        {
            var value = new StringBuilder();
            value.Append(GenerateString(_rnd.Next(1, 100)));
            value.Append(GenerateString(_rnd.Next(1, 100)));
            var target = new FastString(value);
            Assert.AreEqual(target.ToString(), value.ToString());
            value = new StringBuilder();
            target = new FastString(value);
            Assert.AreEqual(target.ToString(), value.ToString());
        }

        [TestMethod]
        public void FastStringConstructorTest2 ()
        {
            char[] value = GenerateString(_rnd.Next(100, 200)).ToCharArray();
            char[] someValue = GenerateString(_rnd.Next(1, 100)).ToCharArray();
            for (int i = 0; i < someValue.Length; i++)
                value[i] = someValue[i];
            int length = someValue.Length;
            var target = new FastString(value, length);
            Assert.AreEqual(target.Length, someValue.Length);
            Assert.AreEqual(target.ToString(), new string(someValue));
            someValue = new char[0];
            length = someValue.Length;
            target = new FastString(value, length);
            Assert.AreEqual(target.Length, someValue.Length);
            Assert.AreEqual(target.ToString(), new string(someValue));
        }

        [TestMethod]
        public void FastStringConstructorTest3 ()
        {
            var target = new FastString();
            Assert.IsTrue(target.Length == 0);
            Assert.AreEqual(target.ToString(), string.Empty);
        }

        [TestMethod]
        public void FastStringConstructorTest4 ()
        {
            char[] value = GenerateString(_rnd.Next(1, 100)).ToCharArray();
            var target = new FastString(value);
            Assert.AreEqual((char[]) target, value);
            Assert.AreEqual(target.ToString(), new string(value));
            value = new char[0];
            target = new FastString(value);
            Assert.AreEqual((char[]) target, value);
            Assert.AreEqual(target.ToString(), new string(value));
        }

        [TestMethod]
        public void AddTest ()
        {
            string one = GenerateString(_rnd.Next(1, 100));
            FastString two = GenerateStringEx(_rnd.Next(1, 100));
            var expected = new FastString(two.ToString() + one);
            FastString actual = FastString.Add(one, two);
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(_rnd.Next(1, 100));
            actual = FastString.Add(one, two);
            expected = new FastString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            one = GenerateString(_rnd.Next(1, 100));
            two = FastString.Empty;
            actual = FastString.Add(one, two);
            expected = new FastString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = FastString.Empty;
            actual = FastString.Add(one, two);
            expected = new FastString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            two = FastString.Empty;
            actual = FastString.Add((string) null, two);
            expected = new FastString(null + two.ToString());
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AddTest1 ()
        {
            string one = GenerateString(_rnd.Next(1, 100));
            FastString two = GenerateStringEx(_rnd.Next(1, 100));
            var expected = new FastString(two.ToString() + one);
            FastString actual = FastString.Add(two, one);
            Assert.AreEqual(expected, actual);
            one = GenerateString(_rnd.Next(1, 100));
            two = FastString.Empty;
            actual = FastString.Add(two, one);
            expected = new FastString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(_rnd.Next(1, 100));
            actual = FastString.Add(two, one);
            expected = new FastString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = FastString.Empty;
            actual = FastString.Add(two, one);
            expected = new FastString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            two = FastString.Empty;
            actual = FastString.Add(two, (string) null);
            expected = new FastString(two.ToString() + null);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AddTest2 ()
        {
            FastString one = GenerateStringEx(_rnd.Next(1, 100));
            FastString two = GenerateStringEx(_rnd.Next(1, 100));
            var expected = new FastString(two.ToString() + one.ToString());
            FastString actual = FastString.Add(two, one);
            Assert.AreEqual(expected, actual);
            one = GenerateString(_rnd.Next(1, 100));
            two = FastString.Empty;
            actual = FastString.Add(two, one);
            expected = new FastString(two.ToString() + one.ToString());
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(_rnd.Next(1, 100));
            actual = FastString.Add(two, one);
            expected = new FastString(two.ToString() + one.ToString());
            Assert.AreEqual(expected, actual);
            two = FastString.Empty;
            actual = FastString.Add(two, (string) null);
            expected = new FastString(two.ToString() + null);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ConcatTest ()
        {
            const int count = 100;
            var strings = new FastString[count];
            for (int i = 0; i < count; i++)
                strings[i] = GenerateStringEx(_rnd.Next(0, 100));
            string expected = string.Concat(strings.Select(s => (string) s).ToArray());
            string actual = FastString.Concat(strings).ToString();
            Assert.AreEqual(expected, actual);
            strings = new FastString[count];
            for (int i = 0; i < count - 1; i++)
                strings[i] = GenerateStringEx(_rnd.Next(0, 100));
            strings[count - 1] = null;
            expected = string.Concat(strings.Select(s => (string) s).ToArray());
            actual = FastString.Concat(strings).ToString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(string.Empty, FastString.Concat(null).ToString());
        }

        [TestMethod]
        public void ConcatTest1 ()
        {
            FastString one = GenerateStringEx(_rnd.Next(1, 100));
            string two = GenerateString(_rnd.Next(1, 100));
            string expected = one.ToString() + two;
            string actual = FastString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            one = FastString.Empty;
            two = GenerateString(_rnd.Next(1, 100));
            expected = one.ToString() + two;
            actual = FastString.Concat(one, two);
            Assert.AreEqual(expected, actual);
            one = GenerateStringEx(_rnd.Next(1, 100));
            two = string.Empty;
            expected = one.ToString() + two;
            actual = FastString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            one = FastString.Empty;
            two = string.Empty;
            expected = one.ToString() + two;
            actual = FastString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            expected = (string) null + null;
            actual = FastString.Concat(one, (string) null).ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ConcatTest2 ()
        {
            FastString one = GenerateStringEx(_rnd.Next(1, 100));
            FastString two = GenerateStringEx(_rnd.Next(1, 100));
            FastString three = GenerateStringEx(_rnd.Next(1, 100));
            string expected = one.ToString() + two.ToString() + three.ToString();
            string actual = FastString.Concat(one, two, three);
            Assert.AreEqual(expected, actual);
            one = GenerateStringEx(_rnd.Next(1, 100));
            two = FastString.Empty;
            expected = one.ToString() + two.ToString() + null;
            actual = FastString.Concat(one, two, null).ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ConcatTest3 ()
        {
            FastString one = GenerateStringEx(_rnd.Next(1, 100));
            FastString two = GenerateStringEx(_rnd.Next(1, 100));
            string expected = one.ToString() + two.ToString();
            string actual = FastString.Concat(one, two);
            Assert.AreEqual(expected, actual);
            one = FastString.Empty;
            two = GenerateStringEx(_rnd.Next(1, 100));
            expected = one.ToString() + two.ToString();
            actual = FastString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            one = GenerateStringEx(_rnd.Next(1, 100));
            two = FastString.Empty;
            expected = one.ToString() + two.ToString();
            actual = FastString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            one = FastString.Empty;
            two = FastString.Empty;
            expected = one.ToString() + two.ToString();
            actual = FastString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ConcatTest4 ()
        {
            const int count = 100;
            var strings = new List<FastString>();
            for (int i = 0; i < count; i++)
                strings.Add(GenerateStringEx(_rnd.Next(0, 100)));
            string expected = string.Concat(strings.Select(s => (string) s));
            string actual = FastString.Concat(strings);
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(string.Empty, FastString.Concat((IEnumerable<FastString>) null).ToString());
        }

        [TestMethod]
        public void EqualsTest ()
        {
            string one = string.Empty;
            bool actual = FastString.Equals(one, null);
            Assert.AreEqual(false, actual);
            one = string.Empty;
            FastString another = FastString.Empty;
            actual = FastString.Equals(one, another);
            Assert.AreEqual(true, actual);
            one = GenerateString(_rnd.Next(1, 100));
            another = new FastString(one);
            actual = FastString.Equals(one, another);
            Assert.AreEqual(true, actual);
            actual = FastString.Equals((string) null, null);
            Assert.AreEqual(true, actual);
        }

        [TestMethod]
        public void EqualsTest1 ()
        {
            FastString one = GenerateStringEx(_rnd.Next(1, 100));
            var another = (FastString) one.Clone();
            bool actual = FastString.Equals(one, another);
            Assert.AreEqual(true, actual);
            one = FastString.Empty;
            another = FastString.Empty;
            actual = FastString.Equals(one, another);
            Assert.AreEqual(true, actual);
            actual = FastString.Equals((FastString) null, null);
            Assert.AreEqual(true, actual);
            another = FastString.Empty;
            actual = FastString.Equals((FastString) null, another);
            Assert.AreEqual(false, actual);
        }

        [TestMethod]
        public void IncrementTest ()
        {
            FastString value = GenerateStringEx(_rnd.Next(1, 100));
            string expected = value.ToString() + value.ToString();
            string actual = FastString.Increment(value).ToString();
            Assert.AreEqual(expected, actual);
            value = FastString.Empty;
            expected = value.ToString() + value.ToString();
            actual = FastString.Increment(value).ToString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(FastString.Increment(null), FastString.Empty);
        }

        [TestMethod]
        public void IsNullOrEmptyTest ()
        {
            bool actual = FastString.IsNullOrEmpty(null);
            Assert.AreEqual(true, actual);
            FastString value = FastString.Empty;
            actual = FastString.IsNullOrEmpty(value);
            Assert.AreEqual(true, actual);
            value = GenerateStringEx(_rnd.Next(1, 100));
            actual = FastString.IsNullOrEmpty(value);
            Assert.AreEqual(false, actual);
            value = " ";
            actual = FastString.IsNullOrEmpty(value);
            Assert.AreEqual(false, actual);
        }

        [TestMethod]
        public void IsNullOrWhiteSpaceTest ()
        {
            bool actual = FastString.IsNullOrWhiteSpace(null);
            Assert.AreEqual(true, actual);
            FastString value = FastString.Empty;
            actual = FastString.IsNullOrWhiteSpace(value);
            Assert.AreEqual(true, actual);
            value = " \r\n\t \n\r\t \t\r\n \t\n\r \r\t\n ";
            actual = FastString.IsNullOrWhiteSpace(value);
            Assert.AreEqual(true, actual);
            value = GenerateStringEx(_rnd.Next(1, 100));
            actual = FastString.IsNullOrWhiteSpace(value);
            Assert.AreEqual(false, actual);
        }

        [TestMethod]
        public void ReplaceTest ()
        {
            string source = "aabaabccaab ab b aacb aab";
            string find = "aab";
            string replace = "xyz";
            FastString target = source;
            string expected = source.Replace(find, replace);
            string actual = target.Replace(find, replace);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EmptyTest ()
        {
            Assert.AreEqual(string.Empty, FastString.Empty.ToString());
        }

        [TestMethod]
        public void ConcatTest5 ()
        {
            FastString one = GenerateStringEx(_rnd.Next(100, 1000));
            char two = one[50];
            string expected = one + two;
            string actual = FastString.Concat(one, two);
            Assert.AreEqual(expected, actual);
        }
    }
}