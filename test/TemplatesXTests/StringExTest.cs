using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Templates.Strings;
using Xunit;

namespace TemplatesXTests {
    public class StringExTest {

        #region Additional test attributes

        private static readonly Random Rnd = new Random(DateTime.Now.Millisecond);

        private static string GenerateString (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) Rnd.Next(30, 0x100);
            return result;
        }

        private static ExString GenerateStringEx (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) Rnd.Next(30, 0x100);
            return new ExString(result);
        }

        #endregion

        [Fact]
        public void FastStringConstructorTest ()
        {
            string value = GenerateString(Rnd.Next(1, 100));
            var target = new ExString(value);
            Assert.Equal(value, target.ToString());
            value = string.Empty;
            target = new ExString(value);
            Assert.Equal(value, target.ToString());
        }

        [Fact]
        public void FastStringConstructorTest1 ()
        {
            var value = new StringBuilder();
            value.Append(GenerateString(Rnd.Next(1, 100)));
            value.Append(GenerateString(Rnd.Next(1, 100)));
            var target = new ExString(value);
            Assert.Equal(target.ToString(), value.ToString());
            value = new StringBuilder();
            target = new ExString(value);
            Assert.Equal(target.ToString(), value.ToString());
        }

        [Fact]
        public void FastStringConstructorTest2 ()
        {
            char[] value = GenerateString(Rnd.Next(100, 200)).ToCharArray();
            char[] someValue = GenerateString(Rnd.Next(1, 100)).ToCharArray();
            for (int i = 0; i < someValue.Length; i++)
                value[i] = someValue[i];
            int length = someValue.Length;
            var target = new ExString(value, length);
            Assert.Equal(target.Length, someValue.Length);
            Assert.Equal(target.ToString(), new string(someValue));
            someValue = new char[0];
            length = someValue.Length;
            target = new ExString(value, length);
            Assert.Equal(target.Length, someValue.Length);
            Assert.Equal(target.ToString(), new string(someValue));
        }

        [Fact]
        public void FastStringConstructorTest3 ()
        {
            var target = new ExString();
            Assert.True(target.Length == 0);
            Assert.Equal(target.ToString(), string.Empty);
        }

        [Fact]
        public void FastStringConstructorTest4 ()
        {
            char[] value = GenerateString(Rnd.Next(1, 100)).ToCharArray();
            var target = new ExString(value);
            Assert.Equal((char[]) target, value);
            Assert.Equal(target.ToString(), new string(value));
            value = new char[0];
            target = new ExString(value);
            Assert.Equal((char[]) target, value);
            Assert.Equal(target.ToString(), new string(value));
        }

        [Fact]
        public void AddTest ()
        {
            string one = GenerateString(Rnd.Next(1, 100));
            ExString two = GenerateStringEx(Rnd.Next(1, 100));
            var expected = new ExString(two.ToString() + one);
            ExString actual = ExString.Add(one, two);
            Assert.Equal(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(Rnd.Next(1, 100));
            actual = ExString.Add(one, two);
            expected = new ExString(two.ToString() + one);
            Assert.Equal(expected, actual);
            one = GenerateString(Rnd.Next(1, 100));
            two = ExString.Empty;
            actual = ExString.Add(one, two);
            expected = new ExString(two.ToString() + one);
            Assert.Equal(expected, actual);
            one = string.Empty;
            two = ExString.Empty;
            actual = ExString.Add(one, two);
            expected = new ExString(two.ToString() + one);
            Assert.Equal(expected, actual);
            two = ExString.Empty;
            actual = ExString.Add((string) null, two);
            expected = new ExString(null + two.ToString());
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AddTest1 ()
        {
            string one = GenerateString(Rnd.Next(1, 100));
            ExString two = GenerateStringEx(Rnd.Next(1, 100));
            var expected = new ExString(two.ToString() + one);
            ExString actual = ExString.Add(two, one);
            Assert.Equal(expected, actual);
            one = GenerateString(Rnd.Next(1, 100));
            two = ExString.Empty;
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one);
            Assert.Equal(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(Rnd.Next(1, 100));
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one);
            Assert.Equal(expected, actual);
            one = string.Empty;
            two = ExString.Empty;
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one);
            Assert.Equal(expected, actual);
            two = ExString.Empty;
            actual = ExString.Add(two, (string) null);
            expected = new ExString(two.ToString() + null);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void AddTest2 ()
        {
            ExString one = GenerateStringEx(Rnd.Next(1, 100));
            ExString two = GenerateStringEx(Rnd.Next(1, 100));
            var expected = new ExString(two.ToString() + one.ToString());
            ExString actual = ExString.Add(two, one);
            Assert.Equal(expected, actual);
            one = GenerateString(Rnd.Next(1, 100));
            two = ExString.Empty;
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one.ToString());
            Assert.Equal(expected, actual);
            one = string.Empty;
            two = GenerateStringEx(Rnd.Next(1, 100));
            actual = ExString.Add(two, one);
            expected = new ExString(two.ToString() + one.ToString());
            Assert.Equal(expected, actual);
            two = ExString.Empty;
            actual = ExString.Add(two, (string) null);
            expected = new ExString(two.ToString() + null);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConcatTest ()
        {
            const int count = 100;
            var strings = new ExString[count];
            for (int i = 0; i < count; i++)
                strings[i] = GenerateStringEx(Rnd.Next(0, 100));
            string expected = string.Concat(strings.Select(s => (string) s).ToArray());
            string actual = ExString.Concat(strings).ToString();
            Assert.Equal(expected, actual);
            strings = new ExString[count];
            for (int i = 0; i < count - 1; i++)
                strings[i] = GenerateStringEx(Rnd.Next(0, 100));
            strings[count - 1] = null;
            expected = string.Concat(strings.Select(s => (string) s).ToArray());
            actual = ExString.Concat(strings).ToString();
            Assert.Equal(expected, actual);
            Assert.Equal(string.Empty, ExString.Concat(null).ToString());
        }

        [Fact]
        public void ConcatTest1 ()
        {
            ExString one = GenerateStringEx(Rnd.Next(1, 100));
            string two = GenerateString(Rnd.Next(1, 100));
            string expected = one.ToString() + two;
            string actual = ExString.Concat(one, two).ToString();
            Assert.Equal(expected, actual);
            one = ExString.Empty;
            two = GenerateString(Rnd.Next(1, 100));
            expected = one.ToString() + two;
            actual = ExString.Concat(one, two);
            Assert.Equal(expected, actual);
            one = GenerateStringEx(Rnd.Next(1, 100));
            two = string.Empty;
            expected = one.ToString() + two;
            actual = ExString.Concat(one, two).ToString();
            Assert.Equal(expected, actual);
            one = ExString.Empty;
            two = string.Empty;
            expected = one.ToString() + two;
            actual = ExString.Concat(one, two).ToString();
            Assert.Equal(expected, actual);
            expected = (string) null + null;
            actual = ExString.Concat(one, (string) null).ToString();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConcatTest2 ()
        {
            ExString one = GenerateStringEx(Rnd.Next(1, 100));
            ExString two = GenerateStringEx(Rnd.Next(1, 100));
            ExString three = GenerateStringEx(Rnd.Next(1, 100));
            string expected = one.ToString() + two.ToString() + three.ToString();
            string actual = ExString.Concat(one, two, three);
            Assert.Equal(expected, actual);
            one = GenerateStringEx(Rnd.Next(1, 100));
            two = ExString.Empty;
            expected = one.ToString() + two.ToString() + null;
            actual = ExString.Concat(one, two, null).ToString();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConcatTest3 ()
        {
            ExString one = GenerateStringEx(Rnd.Next(1, 100));
            ExString two = GenerateStringEx(Rnd.Next(1, 100));
            string expected = one.ToString() + two.ToString();
            string actual = ExString.Concat(one, two);
            Assert.Equal(expected, actual);
            one = ExString.Empty;
            two = GenerateStringEx(Rnd.Next(1, 100));
            expected = one.ToString() + two.ToString();
            actual = ExString.Concat(one, two).ToString();
            Assert.Equal(expected, actual);
            one = GenerateStringEx(Rnd.Next(1, 100));
            two = ExString.Empty;
            expected = one.ToString() + two.ToString();
            actual = ExString.Concat(one, two).ToString();
            Assert.Equal(expected, actual);
            one = ExString.Empty;
            two = ExString.Empty;
            expected = one.ToString() + two.ToString();
            actual = ExString.Concat(one, two).ToString();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ConcatTest4 ()
        {
            const int count = 100;
            var strings = new List<ExString>();
            for (int i = 0; i < count; i++)
                strings.Add(GenerateStringEx(Rnd.Next(0, 100)));
            string expected = string.Concat(strings.Select(s => (string) s));
            string actual = ExString.Concat(strings);
            Assert.Equal(expected, actual);
            Assert.Equal(string.Empty, ExString.Concat((IEnumerable<ExString>) null).ToString());
        }

        [Fact]
        public void EqualsTest ()
        {
            string one = string.Empty;
            bool actual = ExString.Equals(one, null);
            Assert.Equal(false, actual);
            one = string.Empty;
            ExString another = ExString.Empty;
            actual = ExString.Equals(one, another);
            Assert.Equal(true, actual);
            one = GenerateString(Rnd.Next(1, 100));
            another = new ExString(one);
            actual = ExString.Equals(one, another);
            Assert.Equal(true, actual);
            actual = ExString.Equals((string) null, null);
            Assert.Equal(true, actual);
        }

        [Fact]
        public void EqualsTest1 ()
        {
            ExString one = GenerateStringEx(Rnd.Next(1, 100));
            var another = (ExString) one.Clone();
            bool actual = ExString.Equals(one, another);
            Assert.Equal(true, actual);
            one = ExString.Empty;
            another = ExString.Empty;
            actual = ExString.Equals(one, another);
            Assert.Equal(true, actual);
            actual = ExString.Equals((ExString) null, null);
            Assert.Equal(true, actual);
            another = ExString.Empty;
            actual = ExString.Equals((ExString) null, another);
            Assert.Equal(false, actual);
        }

        [Fact]
        public void IncrementTest ()
        {
            ExString value = GenerateStringEx(Rnd.Next(1, 100));
            string expected = value.ToString() + value.ToString();
            string actual = ExString.Duplicate(value).ToString();
            Assert.Equal(expected, actual);
            value = ExString.Empty;
            expected = value.ToString() + value.ToString();
            actual = ExString.Duplicate(value).ToString();
            Assert.Equal(expected, actual);
            Assert.Equal(ExString.Duplicate(null), ExString.Empty);
        }

        [Fact]
        public void IsNullOrEmptyTest ()
        {
            bool actual = ExString.IsNullOrEmpty(null);
            Assert.Equal(true, actual);
            ExString value = ExString.Empty;
            actual = ExString.IsNullOrEmpty(value);
            Assert.Equal(true, actual);
            value = GenerateStringEx(Rnd.Next(1, 100));
            actual = ExString.IsNullOrEmpty(value);
            Assert.Equal(false, actual);
            value = " ";
            actual = ExString.IsNullOrEmpty(value);
            Assert.Equal(false, actual);
        }

        [Fact]
        public void IsNullOrWhiteSpaceTest ()
        {
            bool actual = ExString.IsNullOrWhiteSpace(null);
            Assert.Equal(true, actual);
            ExString value = ExString.Empty;
            actual = ExString.IsNullOrWhiteSpace(value);
            Assert.Equal(true, actual);
            value = " \r\n\t \n\r\t \t\r\n \t\n\r \r\t\n ";
            actual = ExString.IsNullOrWhiteSpace(value);
            Assert.Equal(true, actual);
            value = GenerateStringEx(Rnd.Next(1, 100));
            actual = ExString.IsNullOrWhiteSpace(value);
            Assert.Equal(false, actual);
        }

        [Fact]
        public void ReplaceTest ()
        {
            const string source = "aabaabccaab ab b aacb aab";
            const string find = "aab";
            const string replace = "xyz";
            ExString target = source;
            string expected = source.Replace(find, replace);
            string actual = target.Replace(find, replace);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void EmptyTest ()
        {
            Assert.Equal(string.Empty, ExString.Empty.ToString());
        }

        [Fact]
        public void ConcatTest5 ()
        {
            ExString one = GenerateStringEx(Rnd.Next(100, 1000));
            char two = one[50];
            string expected = one + two;
            string actual = ExString.Concat(one, two);
            Assert.Equal(expected, actual);
        }
    }
}