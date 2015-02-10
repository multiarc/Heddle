using System;
using System.Collections;
using System.Globalization;
using NativeFunctions;

namespace Templates.Strings {
    public sealed partial class ExString: IComparable, ICloneable, IConvertible, IEnumerable, IEquatable<string>, IEquatable<ExString>,
                                            IEquatable<char[]> {
        #region ICloneable Members

        public object Clone ()
        {
            return new ExString(_data, _length);
        }

        #endregion

        #region IComparable Members

        public int CompareTo (object obj)
        {
            var fastString = obj as ExString;
            int lenOne = _length;
            if (fastString != null) {
                int lenTwo = fastString._length;
                unsafe {
                    fixed (char* two = (char[]) fastString) {
                        fixed (char* one = _data) {
                            return StringNativeHelper.Equals(one, two, lenOne, lenTwo);
                        }
                    }
                }
            }
            var str = obj as string;
            if (str != null) {
                int lenTwo = str.Length;
                unsafe {
                    fixed (char* two = str) {
                        fixed (char* one = _data) {
                            return StringNativeHelper.Equals(one, two, lenOne, lenTwo);
                        }
                    }
                }
            }
            var chr = obj as char[];
            if (chr != null) {
                int lenTwo = chr.Length;
                unsafe {
                    fixed (char* two = chr) {
                        fixed (char* one = _data) {
                            return StringNativeHelper.Equals(one, two, lenOne, lenTwo);
                        }
                    }
                }
            }
            if (obj == null && _length == 0)
                return 0;
            return 1;
        }

        #endregion

        #region IConvertible Members

        public TypeCode GetTypeCode ()
        {
            return TypeCode.Object;
        }

        // ReSharper disable SpecifyACultureInStringConversionExplicitly
        public bool ToBoolean (IFormatProvider provider)
        {
            return Convert.ToBoolean(ToString(), provider);
        }

        public char ToChar (IFormatProvider provider)
        {
            return Convert.ToChar(ToString(), provider);
        }

        [CLSCompliant(false)]
        public sbyte ToSByte (IFormatProvider provider)
        {
            return Convert.ToSByte(ToString(), provider);
        }

        public byte ToByte (IFormatProvider provider)
        {
            return Convert.ToByte(ToString(), provider);
        }

        public short ToInt16 (IFormatProvider provider)
        {
            return Convert.ToInt16(ToString(), provider);
        }

        [CLSCompliant(false)]
        public ushort ToUInt16 (IFormatProvider provider)
        {
            return Convert.ToUInt16(ToString(), provider);
        }

        public int ToInt32 (IFormatProvider provider)
        {
            return Convert.ToInt32(ToString(), provider);
        }

        [CLSCompliant(false)]
        public uint ToUInt32 (IFormatProvider provider)
        {
            return Convert.ToUInt32(ToString(), provider);
        }

        public long ToInt64 (IFormatProvider provider)
        {
            return Convert.ToInt64(ToString(), provider);
        }

        [CLSCompliant(false)]
        public ulong ToUInt64 (IFormatProvider provider)
        {
            return Convert.ToUInt64(ToString(), provider);
        }

        public float ToSingle (IFormatProvider provider)
        {
            return Convert.ToSingle(ToString(), provider);
        }

        public double ToDouble (IFormatProvider provider)
        {
            return Convert.ToDouble(ToString(), provider);
        }

        public decimal ToDecimal (IFormatProvider provider)
        {
            return Convert.ToDecimal(ToString(), provider);
        }

        public DateTime ToDateTime (IFormatProvider provider)
        {
            return Convert.ToDateTime(ToString(), provider);
        }

        public string ToString (IFormatProvider provider)
        {
            return Convert.ToString(ToString(), provider);
        }

        public object ToType (Type conversionType, IFormatProvider provider)
        {
            if (conversionType == null)
                throw new ArgumentNullException("conversionType");
            if (provider == null)
                throw new ArgumentNullException("provider");

            if (conversionType == typeof (string))
                return ToString(provider);
            if (conversionType == typeof (ExString))
                return this;
            if (conversionType == typeof (DateTime))
                return ToDateTime(provider);
            if (conversionType == typeof (decimal))
                return ToDecimal(provider);
            if (conversionType == typeof (double))
                return ToDouble(provider);
            if (conversionType == typeof (float))
                return ToSingle(provider);
            if (conversionType == typeof (ulong))
                return ToUInt64(provider);
            if (conversionType == typeof (long))
                return ToInt64(provider);
            if (conversionType == typeof (uint))
                return ToUInt32(provider);
            if (conversionType == typeof (int))
                return ToInt32(provider);
            if (conversionType == typeof (ushort))
                return ToUInt16(provider);
            if (conversionType == typeof (short))
                return ToInt16(provider);
            if (conversionType == typeof (byte))
                return ToByte(provider);
            if (conversionType == typeof (sbyte))
                return ToSByte(provider);
            if (conversionType == typeof (char))
                return ToChar(provider);
            if (conversionType == typeof (bool))
                return ToBoolean(provider);
            if (conversionType == typeof (object))
                return this;
            if (conversionType.IsEnum)
                return Enum.Parse(conversionType, this);
            throw new InvalidCastException(string.Format(CultureInfo.InvariantCulture, "Cannot convert [{0}] to [{1}]", GetType(), conversionType));
        }

        #endregion

        // ReSharper restore SpecifyACultureInStringConversionExplicitly

        #region IEnumerable Members

        public IEnumerator GetEnumerator ()
        {
            for (int i = 0; i < _length; i++)
                yield return _data[i];
        }

        #endregion

        #region IEquatable<char[]> Members

        public bool Equals (char[] other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(_data, other))
                return true;
            int lenTwo = other.Length;
            int lenOne = _length;
            unsafe {
                fixed (char* two = other) {
                    fixed (char* one = _data) {
                        return StringNativeHelper.Equals(one, two, lenOne, lenTwo) == 0;
                    }
                }
            }
        }

        #endregion

        #region IEquatable<FastString> Members

        public bool Equals (ExString other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            int lenTwo = other.Length;
            int lenOne = _length;
            unsafe {
                fixed (char* two = other._data) {
                    fixed (char* one = _data) {
                        return StringNativeHelper.Equals(one, two, lenOne, lenTwo) == 0;
                    }
                }
            }
        }

        #endregion

        #region IEquatable<string> Members

        public bool Equals (string other)
        {
            if (ReferenceEquals(null, other))
                return false;

            int lenTwo = other.Length;
            int lenOne = _length;
            unsafe {
                fixed (char* two = other) {
                    fixed (char* one = _data) {
                        return StringNativeHelper.Equals(one, two, lenOne, lenTwo) == 0;
                    }
                }
            }
        }

        #endregion
                                            }
}