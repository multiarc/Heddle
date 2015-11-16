using Templates.Strings;

namespace Templates.Helpers
{
    internal static class ByteFormatter
    {
        public static string ToHexString(this byte[] array)
        {
            ExStringBuilder builder = new ExStringBuilder();
            foreach (byte value in array)
            {
                builder.Append(value.ToString("X"));
            }
            return builder.ToString();
        }
    }
}
