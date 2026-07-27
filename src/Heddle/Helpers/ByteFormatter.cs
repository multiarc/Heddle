using Heddle.Strings;

namespace Heddle.Helpers
{
    internal static class ByteFormatter
    {
        /// <summary>
        /// Two hex digits per byte, always. A single-digit format drops the leading zero of every byte below 0x10,
        /// which shortens the string and silently shifts every digit after it — the public keys built from this are
        /// then rejected by the compiler that reads them back.
        /// </summary>
        public static string ToHexString(this byte[] array)
        {
            ExStringBuilder builder = new ExStringBuilder();
            foreach (byte value in array)
            {
                builder.Append(value.ToString("X2"));
            }
            return builder.ToString();
        }
    }
}
