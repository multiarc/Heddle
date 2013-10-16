namespace FastStrings.Core {
    public static class Mutable {
        public static MutableString ToMutableString (this object value)
        {
            if (value == null)
                return FastString.Empty;
            var mutableString = value as MutableString;
            if (mutableString != null)
                return mutableString;
            var fastString = value as FastString;
            if (fastString != null)
                return fastString.ToMutableString();
            var s = value as string;
            if (s != null)
                return s.ToMutableString();
            return MutableString.Empty;
        }

        public static MutableString ToMutableString (this string value)
        {
            if (string.IsNullOrEmpty(value))
                return MutableString.Empty;
            return new MutableString(value);
        }

        public static MutableString ToMutableString (this FastString value)
        {
            if (FastString.IsNullOrEmpty(value))
                return MutableString.Empty;
            return new MutableString(value);
        }

        public static bool IsNullOrEmpty (this MutableString value)
        {
            return MutableString.IsNullOrEmpty(value);
        }

        public static bool IsNullOrWhiteSpace (this MutableString value)
        {
            return MutableString.IsNullOrWhiteSpace(value);
        }

        public static bool IsNullOrEmpty (this FastString value)
        {
            return FastString.IsNullOrEmpty(value);
        }

        public static bool IsNullOrWhiteSpace (this FastString value)
        {
            return FastString.IsNullOrWhiteSpace(value);
        }

        public static bool IsNullOrEmpty (this string value)
        {
            return string.IsNullOrEmpty(value);
        }

        public static bool IsNullOrWhiteSpace (this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }
    }
}