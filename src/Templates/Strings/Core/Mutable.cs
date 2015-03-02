using System.Runtime.CompilerServices;

namespace Templates.Strings.Core {
    public static class Mutable {
        public static MutableString ToMutableString (this object value)
        {
            if (value == null)
                return ExString.Empty;
            var mutableString = value as MutableString;
            if (mutableString != null)
                return mutableString;
            var fastString = value as ExString;
            if (fastString != null)
                return fastString.ToMutableString();
            var s = value as string;
            if (s != null)
                return s.ToMutableString();
            return value.ToString();
        }

        public static MutableString ToMutableString (this string value)
        {
            if (string.IsNullOrEmpty(value))
                return MutableString.Empty;
            return new MutableString(value);
        }

        public static MutableString ToMutableString (this ExString value)
        {
            if (ExString.IsNullOrEmpty(value))
                return MutableString.Empty;
            return new MutableString(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty (this MutableString value)
        {
            return MutableString.IsNullOrEmpty(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrWhiteSpace (this MutableString value)
        {
            return MutableString.IsNullOrWhiteSpace(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty (this ExString value)
        {
            return ExString.IsNullOrEmpty(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrWhiteSpace (this ExString value)
        {
            return ExString.IsNullOrWhiteSpace(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty (this string value)
        {
            return string.IsNullOrEmpty(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrWhiteSpace (this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }
    }
}