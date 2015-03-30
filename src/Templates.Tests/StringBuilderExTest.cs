using System;
using Templates.Strings;
using Xunit;

namespace TemplatesXTests {
    public class StringBuilderExTest
    {
        private static readonly Random Rnd = new Random(DateTime.Now.Millisecond);

        private static string GenerateString (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) Rnd.Next(1, 0x10000);
            return result;
        }

        private static ExString GenerateStringEx (int len)
        {
            string result = string.Empty;
            for (int i = 0; i < len; i++)
                result += (char) Rnd.Next(1, 0x10000);
            return new ExString(result);
        }

        [Fact]
        public void StringBuilderExConstructorTest ()
        {
            var target = new ExStringBuilder((ExString) null);
            Assert.Equal(target.Length, 0);

            ExString value = GenerateStringEx(50);
            target = new ExStringBuilder(value);

            Assert.Equal(target.Length, 50);
            Assert.Equal(target.ToExString(), value);
        }

        [Fact]
        public void StringBuilderExConstructorTest1 ()
        {
            var target = new ExStringBuilder();

            Assert.Equal(target.Length, 0);
            Assert.Equal(target.ToExString(), ExString.Empty);
        }

        [Fact]
        public void StringBuilderExConstructorTest2 ()
        {
            var target = new ExStringBuilder((string) null);
            Assert.Equal(target.Length, 0);

            string value = GenerateString(50);
            target = new ExStringBuilder(value);

            Assert.Equal(50, target.Length);
            Assert.Equal(value, target.ToString());
            Assert.Equal(new ExString(value), target.ToExString());
        }

        [Fact]
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

            Assert.Equal(expected, actual);
            Assert.Equal(target.Length, 50);
        }

        [Fact]
        public void ClearTest ()
        {
            var target = new ExStringBuilder(GenerateStringEx(20));
            target.Append(GenerateStringEx(10));
            target.Append(GenerateStringEx(20));
            target.Clear();
            Assert.Equal(target.ToExString(), ExString.Empty);
            Assert.Equal(target.Length, 0);
            target = new ExStringBuilder(GenerateStringEx(20));
            target.Append(GenerateStringEx(10));
            target.Append(GenerateStringEx(20));
            target.ToExString();
            target.Clear();

            Assert.Equal(target.ToExString(), ExString.Empty);
            Assert.Equal(target.Length, 0);
        }

        [Fact]
        public void AppendTest ()
        {
            ExString expected = GenerateStringEx(10);
            var target = new ExStringBuilder(expected);
            ExString append1 = GenerateStringEx(20);
            ExString append2 = GenerateStringEx(20);
            target.Append(append1);
            target.Append(append2);
            expected += append1 + append2;

            Assert.Equal(target.Length, 50);
            ExString actual = target.ToExString();

            Assert.Equal(expected, actual);
            Assert.Equal(target.Length, 50);
        }

        [Fact]
        public void AppendTest1 ()
        {
            string expected = GenerateString(10);
            var target = new ExStringBuilder(expected);
            string append1 = GenerateString(20);
            string append2 = GenerateString(20);
            target.Append(append1);
            target.Append(append2);
            expected += append1 + append2;

            Assert.Equal(target.Length, 50);
            string actual = target.ToString();

            Assert.Equal(expected, actual);
            Assert.Equal(target.Length, 50);
        }

        [Fact]
        public void ToStringTest ()
        {
            var target = new ExStringBuilder();
            string expected = string.Empty;
            string actual = target.ToString();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void LengthTest ()
        {
            var target = new ExStringBuilder(GenerateStringEx(20));
            int actual = target.Length;

            Assert.Equal(actual, 20);
        }
    }
}