using System;
using NativeFunctions;

namespace Templates.Strings.Core {
    public sealed class MutableString: IEquatable<MutableString> {
        private static readonly MutableString EmptyString = new MutableString();
        public readonly bool IsExString;

        public readonly int Length;
        private readonly ExString _exValue;
        private readonly string _value;

        public MutableString (string value)
        {
            if (value != null) {
                _value = value;
                IsExString = false;
                Length = value.Length;
            } else {
                _exValue = ExString.Empty;
                IsExString = true;
                Length = 0;
            }
        }

        public MutableString (ExString value)
        {
            _exValue = value ?? ExString.Empty;
            IsExString = true;
            Length = _exValue.Length;
        }

        public MutableString ()
        {
            _exValue = ExString.Empty;
            IsExString = true;
            Length = 0;
        }

        public static MutableString Empty
        {
            get { return EmptyString; }
        }

        #region IEquatable<MutableString> Members

        public bool Equals (MutableString other)
        {
            if (IsExString)
                return _exValue.Equals((ExString) other);
            return _value.Equals(other);
        }

        #endregion

        public static implicit operator MutableString (ExString value)
        {
            if (ExString.IsNullOrEmpty(value))
                return EmptyString;
            return new MutableString(value);
        }

        public static implicit operator MutableString (string value)
        {
            if (string.IsNullOrEmpty(value))
                return EmptyString;
            return new MutableString(value);
        }

        public static implicit operator ExString (MutableString value)
        {
            if (ReferenceEquals(null, value))
                return null;

            if (value.IsExString)
                return value._exValue;
            return value._value;
        }

        public static implicit operator string (MutableString value)
        {
            if (ReferenceEquals(null, value))
                return null;

            if (value.IsExString)
                return value._exValue;
            return value._value;
        }

        public MutableString Trim ()
        {
            if (IsExString)
                return _exValue.Trim();
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
                    if (value.IsExString) {
                        fixed (char* data = (char[]) (ExString) value) {
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
                return one.IsExString ? one._exValue.Equals((ExString) other) : one._value.Equals(other);
            return other.IsExString ? other._exValue.Equals((ExString) null) : other._value.Equals(null);
        }

        public override bool Equals (object obj)
        {
            if (IsExString)
                return _exValue.Equals(obj);
            return _value.Equals(obj);
        }

        public override int GetHashCode ()
        {
            if (IsExString)
                return _exValue.GetHashCode();
            return _value.GetHashCode();
        }

        public override string ToString ()
        {
            if (IsExString) {
// ReSharper disable SpecifyACultureInStringConversionExplicitly
                return _exValue.ToString();
// ReSharper restore SpecifyACultureInStringConversionExplicitly
            }
            return _value;
        }
    }
}