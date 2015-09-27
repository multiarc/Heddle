using System;

namespace Templates.Strings.Core {
    public struct MutableString: IEquatable<MutableString> {
        private static readonly MutableString EmptyString = new MutableString();
        private readonly bool _isExString;

        private readonly int _length;
        private readonly ExString _exValue;
        private readonly string _value;

        public MutableString (string value)
        {
            if (value != null) {
                _value = value;
                _isExString = false;
                _length = value.Length;
                _exValue = null;
            } else {
                _exValue = ExString.Empty;
                _value = null;
                _isExString = true;
                _length = 0;
            }
        }

        public MutableString (ExString value)
        {
            _exValue = value ?? ExString.Empty;
            _value = null;
            _isExString = true;
            _length = _exValue.Length;
        }

        public static MutableString Empty => EmptyString;

        public int Length => _length;

        public bool IsExString => _isExString;

        public ExString ExStringValue => _exValue;

        public string StringValue => _value;

        #region IEquatable<MutableString> Members

        public bool Equals (MutableString other)
        {
            if (_isExString)
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
            if (value._isExString)
                return value._exValue;
            return value._value;
        }

        public static implicit operator string (MutableString value)
        {
            if (value._isExString)
                return value._exValue;
            return value._value;
        }

        public MutableString Trim ()
        {
            if (_isExString)
                return _exValue.Trim();
            return _value.Trim();
        }

        public static bool IsNullOrEmpty (MutableString value)
        {
            return value._length == 0;
        }

        public static bool IsNullOrWhiteSpace (MutableString value)
        {
            int len = value._length;
            if (len > 0)
            {
                if (value._isExString)
                {
                    return ExString.IsNullOrWhiteSpace(value._exValue);
                }
                return string.IsNullOrWhiteSpace(value._value);
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

        public static bool Equals(MutableString one, MutableString other)
        {
            if (one._isExString)
            {
                if (other._isExString)
                {
                    return one._exValue.Equals(other._exValue);
                }
                return one._exValue.Equals(other._value);
            }
            if (other._isExString)
            {
                return other._exValue.Equals(one._value);
            }
            return one._value.Equals(other._value);
        }

        public override bool Equals (object obj)
        {
            if (obj == null)
                return false;
            if (_isExString)
                return _exValue.Equals(obj);
            return _value.Equals(obj);
        }

        public override int GetHashCode ()
        {
            if (_isExString)
                return _exValue.GetHashCode();
            return _value.GetHashCode();
        }

        public override string ToString ()
        {
            if (_isExString) {
                return _exValue.ToString();
            }
            return _value;
        }

        public ExString ToExString()
        {
            if (_isExString) {
                return _exValue;
            }
            return new ExString(_value);
        }
    }
}