using System;
using NativeFunctions;

namespace Templates.Strings.Core {
    public sealed class MutableString: IEquatable<MutableString> {
        private static readonly MutableString EmptyString = new MutableString();
        public readonly bool IsFastString;

        public readonly int Length;
        private readonly FastString _fastValue;
        private readonly string _value;

        public MutableString (string value)
        {
            if (value != null) {
                _value = value;
                IsFastString = false;
                Length = value.Length;
            } else {
                _fastValue = FastString.Empty;
                IsFastString = true;
                Length = 0;
            }
        }

        public MutableString (FastString value)
        {
            _fastValue = value ?? FastString.Empty;
            IsFastString = true;
            Length = _fastValue.Length;
        }

        public MutableString ()
        {
            _fastValue = FastString.Empty;
            IsFastString = true;
            Length = 0;
        }

        public static MutableString Empty
        {
            get { return EmptyString; }
        }

        #region IEquatable<MutableString> Members

        public bool Equals (MutableString other)
        {
            if (IsFastString)
                return _fastValue.Equals((FastString) other);
            return _value.Equals(other);
        }

        #endregion

        public static implicit operator MutableString (FastString value)
        {
            if (FastString.IsNullOrEmpty(value))
                return EmptyString;
            return new MutableString(value);
        }

        public static implicit operator MutableString (string value)
        {
            if (string.IsNullOrEmpty(value))
                return EmptyString;
            return new MutableString(value);
        }

        public static implicit operator FastString (MutableString value)
        {
            if (ReferenceEquals(null, value))
                return null;

            if (value.IsFastString)
                return value._fastValue;
            return value._value;
        }

        public static implicit operator string (MutableString value)
        {
            if (ReferenceEquals(null, value))
                return null;

            if (value.IsFastString)
                return value._fastValue;
            return value._value;
        }

        public MutableString Trim ()
        {
            if (IsFastString)
                return _fastValue.Trim();
            return _value.Trim();
        }

        public static bool IsNullOrEmpty (MutableString value)
        {
            return value == null || value.Length == 0;
        }

        public static bool IsNullOrWhiteSpace (MutableString value)
        {
            int len = value == null ? 0 : value.Length;
            if (len > 0) {
                unsafe {
                    if (value.IsFastString) {
                        fixed (char* data = (char[]) (FastString) value) {
                            for (int i = 0; i < len; i++) {
                                if (!StringNativeHelper.IsWhiteSpace(data[i]))
                                    return false;
                            }
                        }
                    } else {
                        fixed (char* data = (string) value) {
                            for (int i = 0; i < len; i++) {
                                if (!StringNativeHelper.IsWhiteSpace(data[i]))
                                    return false;
                            }
                        }
                    }
                }
            }
            return true;
        }

        public static bool operator == (MutableString one, MutableString other)
        {
            return Equals(one, other);
        }

        public static bool operator != (MutableString one, MutableString other)
        {
            return !Equals(one, other);
        }

        public static bool Equals (MutableString one, MutableString other)
        {
            if (ReferenceEquals(one, other))
                return true;

            if (!ReferenceEquals(null, one))
                return one.IsFastString ? one._fastValue.Equals((FastString) other) : one._value.Equals(other);
            return other.IsFastString ? other._fastValue.Equals((FastString) null) : other._value.Equals(null);
        }

        public override bool Equals (object obj)
        {
            if (IsFastString)
                return _fastValue.Equals(obj);
            return _value.Equals(obj);
        }

        public override int GetHashCode ()
        {
            if (IsFastString)
                return _fastValue.GetHashCode();
            return _value.GetHashCode();
        }

        public override string ToString ()
        {
            if (IsFastString) {
// ReSharper disable SpecifyACultureInStringConversionExplicitly
                return _fastValue.ToString();
// ReSharper restore SpecifyACultureInStringConversionExplicitly
            }
            return _value;
        }
    }
}