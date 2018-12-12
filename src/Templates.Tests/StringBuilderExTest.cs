using System;
using Templates.Strings;
using Xunit;

namespace Templates.Tests {
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

            Assert.Equal(50, target.Length);
            string actual = target.ToString();

            Assert.Equal(expected, actual);
            Assert.Equal(50, target.Length);
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
            var target = new ExStringBuilder(GenerateString(20));
            int actual = target.Length;

            Assert.Equal(20, actual);
        }
    }
}