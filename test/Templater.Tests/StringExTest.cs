using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Strings;
using Templates.Strings.Core;

namespace Templater.Tests {
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

        private static ExString GenerateStringEx (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) _rnd.Next(30, 0x100);
            return new ExString(result);
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
            var target = new ExString(value);
            Assert.AreEqual(value, target.ToString());
            value = string.Empty;
            target = new ExString(value);
            Assert.AreEqual(value, target.ToString());
        }

        [TestMethod]
        public void FastStringConstructorTest1 ()
        {
            var value = new StringBuilder();
            value.Append(GenerateString(_rnd.Next(1, 100)));
            value.Append(GenerateString(_rnd.Next(1, 100)));
            var target = new ExString(value);
            Assert.AreEqual(target.ToString(), value.ToString());
            value = new StringBuilder();
            target = new ExString(value);
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
            var target = new ExString(value, length);
            Assert.AreEqual(target.Length, someValue.Length);
            Assert.AreEqual(target.ToString(), new string(someValue));
            someValue = new char[0];
            length = someValue.Length;
            target = new ExString(value, length);
            Assert.AreEqual(target.Length, someValue.Length);
            Assert.AreEqual(target.ToString(), new string(someValue));
        }

        [TestMethod]
        public void FastStringConstructorTest3 ()
        {
            var target = new ExString();
            Assert.IsTrue(target.Length == 0);
            Assert.AreEqual(target.ToString(), string.Empty);
        }

        [TestMethod]
        public void FastStringConstructorTest4 ()
        {
            char[] value = GenerateString(_rnd.Next(1, 100)).ToCharArray();
            var target = new ExString(value);
            Assert.AreEqual((char[]) target, value);
            Assert.AreEqual(target.ToString(), new string(value));
            value = new char[0];
            target = new ExString(value);
            Assert.AreEqual((char[]) target, value);
            Assert.AreEqual(target.ToString(), new string(value));
        }

        [TestMethod]
        public void AddTest ()
        {
            string one = GenerateString(_rnd.Next(1, 100));
            ExString two = GenerateStringEx(_rnd.Next(1, 100));
            var expected = new ExString(two.ToString() + one);
            ExString actual = ExString.Add(one, two);
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(_rnd.Next(1, 100));
            actual = ExString.Add(one, two);
            expected = new ExString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            one = GenerateString(_rnd.Next(1, 100));
            two = ExString.Empty;
            actual = ExString.Add(one, two);
            expected = new ExString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = ExString.Empty;
            actual = ExString.Add(one, two);
            expected = new ExString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            two = ExString.Empty;
            actual = ExString.Add((string) null, two);
            expected = new ExString(null + two.ToString());
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AddTest1 ()
        {
            string one = GenerateString(_rnd.Next(1, 100));
            ExString two = GenerateStringEx(_rnd.Next(1, 100));
            var expected = new ExString(two.ToString() + one);
            ExString actual = ExString.Add(two, one);
            Assert.AreEqual(expected, actual);
            one = GenerateString(_rnd.Next(1, 100));
            two = ExString.Empty;
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(_rnd.Next(1, 100));
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = ExString.Empty;
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one);
            Assert.AreEqual(expected, actual);
            two = ExString.Empty;
            actual = ExString.Add(two, (string) null);
            expected = new ExString(two.ToString() + null);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AddTest2 ()
        {
            ExString one = GenerateStringEx(_rnd.Next(1, 100));
            ExString two = GenerateStringEx(_rnd.Next(1, 100));
            var expected = new ExString(two.ToString() + one.ToString());
            ExString actual = ExString.Add(two, one);
            Assert.AreEqual(expected, actual);
            one = GenerateString(_rnd.Next(1, 100));
            two = ExString.Empty;
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one.ToString());
            Assert.AreEqual(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(_rnd.Next(1, 100));
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one.ToString());
            Assert.AreEqual(expected, actual);
            two = ExString.Empty;
            actual = ExString.Add(two, (string) null);
            expected = new ExString(two.ToString() + null);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ConcatTest ()
        {
            const int count = 100;
            var strings = new ExString[count];
            for (int i = 0; i < count; i++)
                strings[i] = GenerateStringEx(_rnd.Next(0, 100));
            string expected = string.Concat(strings.Select(s => (string) s).ToArray());
            string actual = ExString.Concat(strings).ToString();
            Assert.AreEqual(expected, actual);
            strings = new ExString[count];
            for (int i = 0; i < count - 1; i++)
                strings[i] = GenerateStringEx(_rnd.Next(0, 100));
            strings[count - 1] = null;
            expected = string.Concat(strings.Select(s => (string) s).ToArray());
            actual = ExString.Concat(strings).ToString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(string.Empty, ExString.Concat(null).ToString());
        }

        [TestMethod]
        public void ConcatTest1 ()
        {
            ExString one = GenerateStringEx(_rnd.Next(1, 100));
            string two = GenerateString(_rnd.Next(1, 100));
            string expected = one.ToString() + two;
            string actual = ExString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            one = ExString.Empty;
            two = GenerateString(_rnd.Next(1, 100));
            expected = one.ToString() + two;
            actual = ExString.Concat(one, two);
            Assert.AreEqual(expected, actual);
            one = GenerateStringEx(_rnd.Next(1, 100));
            two = string.Empty;
            expected = one.ToString() + two;
            actual = ExString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            one = ExString.Empty;
            two = string.Empty;
            expected = one.ToString() + two;
            actual = ExString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            expected = (string) null + null;
            actual = ExString.Concat(one, (string) null).ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ConcatTest2 ()
        {
            ExString one = GenerateStringEx(_rnd.Next(1, 100));
            ExString two = GenerateStringEx(_rnd.Next(1, 100));
            ExString three = GenerateStringEx(_rnd.Next(1, 100));
            string expected = one.ToString() + two.ToString() + three.ToString();
            string actual = ExString.Concat(one, two, three);
            Assert.AreEqual(expected, actual);
            one = GenerateStringEx(_rnd.Next(1, 100));
            two = ExString.Empty;
            expected = one.ToString() + two.ToString() + null;
            actual = ExString.Concat(one, two, null).ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ConcatTest3 ()
        {
            ExString one = GenerateStringEx(_rnd.Next(1, 100));
            ExString two = GenerateStringEx(_rnd.Next(1, 100));
            string expected = one.ToString() + two.ToString();
            string actual = ExString.Concat(one, two);
            Assert.AreEqual(expected, actual);
            one = ExString.Empty;
            two = GenerateStringEx(_rnd.Next(1, 100));
            expected = one.ToString() + two.ToString();
            actual = ExString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            one = GenerateStringEx(_rnd.Next(1, 100));
            two = ExString.Empty;
            expected = one.ToString() + two.ToString();
            actual = ExString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
            one = ExString.Empty;
            two = ExString.Empty;
            expected = one.ToString() + two.ToString();
            actual = ExString.Concat(one, two).ToString();
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ConcatTest4 ()
        {
            const int count = 100;
            var strings = new List<ExString>();
            for (int i = 0; i < count; i++)
                strings.Add(GenerateStringEx(_rnd.Next(0, 100)));
            string expected = string.Concat(strings.Select(s => (string) s));
            string actual = ExString.Concat(strings);
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(string.Empty, ExString.Concat((IEnumerable<ExString>) null).ToString());
        }

        [TestMethod]
        public void EqualsTest ()
        {
            string one = string.Empty;
            bool actual = ExString.Equals(one, null);
            Assert.AreEqual(false, actual);
            one = string.Empty;
            ExString another = ExString.Empty;
            actual = ExString.Equals(one, another);
            Assert.AreEqual(true, actual);
            one = GenerateString(_rnd.Next(1, 100));
            another = new ExString(one);
            actual = ExString.Equals(one, another);
            Assert.AreEqual(true, actual);
            actual = ExString.Equals((string) null, null);
            Assert.AreEqual(true, actual);
        }

        [TestMethod]
        public void EqualsTest1 ()
        {
            ExString one = GenerateStringEx(_rnd.Next(1, 100));
            var another = (ExString) one.Clone();
            bool actual = ExString.Equals(one, another);
            Assert.AreEqual(true, actual);
            one = ExString.Empty;
            another = ExString.Empty;
            actual = ExString.Equals(one, another);
            Assert.AreEqual(true, actual);
            actual = ExString.Equals((ExString) null, null);
            Assert.AreEqual(true, actual);
            another = ExString.Empty;
            actual = ExString.Equals((ExString) null, another);
            Assert.AreEqual(false, actual);
        }

        [TestMethod]
        public void IncrementTest ()
        {
            ExString value = GenerateStringEx(_rnd.Next(1, 100));
            string expected = value.ToString() + value.ToString();
            string actual = ExString.Increment(value).ToString();
            Assert.AreEqual(expected, actual);
            value = ExString.Empty;
            expected = value.ToString() + value.ToString();
            actual = ExString.Increment(value).ToString();
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(ExString.Increment(null), ExString.Empty);
        }

        [TestMethod]
        public void IsNullOrEmptyTest ()
        {
            bool actual = ExString.IsNullOrEmpty(null);
            Assert.AreEqual(true, actual);
            ExString value = ExString.Empty;
            actual = ExString.IsNullOrEmpty(value);
            Assert.AreEqual(true, actual);
            value = GenerateStringEx(_rnd.Next(1, 100));
            actual = ExString.IsNullOrEmpty(value);
            Assert.AreEqual(false, actual);
            value = " ";
            actual = ExString.IsNullOrEmpty(value);
            Assert.AreEqual(false, actual);
        }

        [TestMethod]
        public void IsNullOrWhiteSpaceTest ()
        {
            bool actual = ExString.IsNullOrWhiteSpace(null);
            Assert.AreEqual(true, actual);
            ExString value = ExString.Empty;
            actual = ExString.IsNullOrWhiteSpace(value);
            Assert.AreEqual(true, actual);
            value = " \r\n\t \n\r\t \t\r\n \t\n\r \r\t\n ";
            actual = ExString.IsNullOrWhiteSpace(value);
            Assert.AreEqual(true, actual);
            value = GenerateStringEx(_rnd.Next(1, 100));
            actual = ExString.IsNullOrWhiteSpace(value);
            Assert.AreEqual(false, actual);
        }

        [TestMethod]
        public void ReplaceTest ()
        {
            string source = "aabaabccaab ab b aacb aab";
            string find = "aab";
            string replace = "xyz";
            ExString target = source;
            string expected = source.Replace(find, replace);
            string actual = target.Replace(find, replace);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EmptyTest ()
        {
            Assert.AreEqual(string.Empty, ExString.Empty.ToString());
        }

        [TestMethod]
        public void ConcatTest5 ()
        {
            ExString one = GenerateStringEx(_rnd.Next(100, 1000));
            char two = one[50];
            string expected = one + two;
            string actual = ExString.Concat(one, two);
            Assert.AreEqual(expected, actual);
        }
    }
}