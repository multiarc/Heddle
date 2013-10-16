using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NativeFunctions;

namespace FastStrings.Core {
    [Serializable]
    public sealed partial class FastString {
        private static readonly FastString EmptyFastString = new FastString(new char[0]);
        private readonly char[] _data;
        private readonly int _length;
        //internal static readonly FastStringAllocator AllocateNewString;

        //private static string AllocateDefault(int length)
        //{
        //    return new string('\0', length);
        //}

        //static FastString()
        //{
        //    try {
        //        MethodInfo nativeMethod = typeof (string).GetMethod("FastAllocateString", BindingFlags.Static | BindingFlags.NonPublic);
        //        var dynamic = new DynamicMethod("FastAllocateString", typeof(string), new[]
        //                                          {
        //                                              typeof (int)
        //                                          }, typeof(FastString), true);
        //        ILGenerator il = dynamic.GetILGenerator();
        //        il.Emit(OpCodes.Ldarg_0);
        //        il.Emit(OpCodes.Call, nativeMethod);
        //        il.Emit(OpCodes.Ret);
        //        AllocateNewString = (FastStringAllocator)dynamic.CreateDelegate(typeof(FastStringAllocator));
        //    }
        //    catch(Exception e) {
        //        AllocateNewString = AllocateDefault;
        //        //TODO: Log error
        //    }
        //}

        public FastString (char[] value, int length)
        {
            if (value == null) {
                if (length != 0)
                    throw new ArgumentNullException("value");
                _data = EmptyFastString._data;
            } else {
                if (length < 0)
                    throw new ArgumentException("length should not be less than zero");
                if (length > value.Length)
                    length = value.Length;
                _data = new char[length];
                unsafe {
                    fixed (char* dest = _data) {
                        fixed (char* src = value) {
                            StringNativeHelper.MemCpy(dest, src, length);
                        }
                    }
                }
            }
            _length = _data.Length;
        }

        public FastString ()
        {
            _data = EmptyFastString._data;
            _length = _data.Length;
        }

        public FastString (char[] value)
        {
            _data = value ?? EmptyFastString._data;
            _length = _data.Length;
        }

        private unsafe FastString (char* value, int length)
        {
            if (length < 0)
                throw new ArgumentException("length should not be less than zero");

            if (value == null) {
                if (length != 0)
                    throw new ArgumentNullException("value");
                _data = EmptyFastString._data;
            } else {
                var len = (int) StringNativeHelper.StrLen(value);
                if (len < length)
                    length = len;
                _data = new char[length];
                fixed (char* dest = _data) {
                    StringNativeHelper.MemCpy(dest, value, length);
                }
            }
            _length = _data.Length;
        }

        public FastString (string value)
        {
            if (string.IsNullOrEmpty(value)) {
                _data = EmptyFastString._data;
                _length = 0;
            } else {
                _length = value.Length;
                _data = new char[value.Length];
                unsafe {
                    fixed (char* dest = _data) {
                        fixed (char* src = value) {
                            StringNativeHelper.MemCpy(dest, src, _length);
                        }
                    }
                }
            }
        }

        public FastString (StringBuilder value)
        {
            if (value == null)
                _data = EmptyFastString._data;
            else {
                int len = value.Length;
                if (len == 0)
                    _data = EmptyFastString._data;
                else {
                    _data = new char[len];
                    value.CopyTo(0, _data, 0, len);
                }
            }
            _length = _data.Length;
        }

        [IndexerName ("Chars")]
        public char this [int index]
        {
            get
            {
                if (index < 0 || index >= _length)
                    throw new ArgumentException();
                return _data[index];
            }
        }

        public int Length
        {
            get { return _length; }
        }

        public static FastString Empty
        {
            get { return EmptyFastString; }
        }

        public override string ToString ()
        {
            int len = _length;
            unsafe {
                fixed (char* data = _data) {
                    return new string(data, 0, len);
                }
            }
        }

        public FastString Trim ()
        {
            unsafe {
                fixed (char* src = _data) {
                    return FastTrim(src, _length);
                }
            }
        }

        private static unsafe FastString FastTrim (char* src, int len)
        {
            int startIndex = 0;
            for (int i = 0; i < len; i++) {
                if (!StringNativeHelper.IsWhiteSpace(src[i])) {
                    startIndex = i;
                    break;
                }
            }
            if (startIndex == len - 1)
                return EmptyFastString;
            int endIndex = len - 1;
            for (int i = len - 1; i >= 0; i--) {
                if (StringNativeHelper.IsWhiteSpace(src[i]))
                    endIndex = i - 1;
                else
                    break;
            }
            int newLength = endIndex - startIndex + 1;
            var result = new char[newLength];
            fixed (char* dest = result) {
                StringNativeHelper.MemCpy(dest, src + startIndex, newLength);
            }
            return new FastString(result);
        }

        private FastString BulkReplace (int[] foundIndexes, FastString toReplace, int findStringLen)
        {
            int count = foundIndexes.Length,
                chunkLength = toReplace.Length;
            if (count == 0)
                return new FastString(_data);

            int capacity = _length + count * chunkLength - count * findStringLen;

            var result = new char[capacity];
            unsafe {
                fixed (char* dest = result) {
                    fixed (char* src = _data) {
                        MoveData(foundIndexes, toReplace, findStringLen, src, dest, _length);
                    }
                }
            }
            return result;
        }

        private static unsafe void MoveData (int[] foundIndexes, FastString toReplace, int findStringLen, char* src, char* dest, int srcLen)
        {
            int count = foundIndexes.Length,
                chunkLength = toReplace.Length;
            int current = 0,
                lastIndex = 0;
            fixed (int* indexes = foundIndexes) {
                fixed (char* replacement = (char[]) toReplace) {
                    lastIndex = MoveChunk(findStringLen, src, dest, replacement, indexes, count, lastIndex, chunkLength, ref current);
                }
            }
            StringNativeHelper.MemCpy(dest + lastIndex, src + current, srcLen - current);
        }

        private static unsafe int MoveChunk
            (int findStringLen, char* src, char* dest, char* replacement, int* indexes, int count, int lastIndex, int chunkLength, ref int current)
        {
            for (int i = 0; i < count; i++) {
                StringNativeHelper.MemCpy(dest + lastIndex, src + current, indexes[i] - current);
                lastIndex += indexes[i] - current;
                StringNativeHelper.MemCpy(dest + lastIndex, replacement, chunkLength);
                current = indexes[i] + findStringLen;
                lastIndex += chunkLength;
            }
            return lastIndex;
        }

        public FastString Replace (FastString toFind, FastString toReplace)
        {
            if (toFind == null)
                throw new ArgumentNullException("toFind");

            if (toReplace == null)
                throw new ArgumentNullException("toReplace");

            return BulkReplace(FindForReplace(toFind), toReplace, toFind.Length);
        }

        //private static unsafe int StartsWith (char* data, char* find, int* needleTable, int dataLen, int findLen)
        //{
        //    if (dataLen >= findLen) {
        //        int found = 0;
        //        int currentIndex = findLen - 1;
        //        int counter = currentIndex;

        //        while (counter >= 0 && currentIndex < dataLen) {
        //            counter = findLen - 1;
        //            found = currentIndex;
        //            while (counter >= 0 && data[found] == find[counter]) {
        //                found--;
        //                counter--;
        //            }
        //            currentIndex += needleTable[(byte) data[currentIndex]];
        //        }
        //        found++;
        //        if (found <= dataLen - findLen)
        //            return found;
        //    }
        //    return -1;
        //}

        private int[] FindForReplace (FastString toFind)
        {
            var replacements = new SmartList<int>();

            int dataLen = _length,
                findLen = toFind.Length;

            if (findLen == 0)
                return replacements;
            unsafe {
                fixed (char* data = _data) {
                    fixed (char* find = (char[]) toFind) {
                        FindReplacesNative(find, findLen, replacements, data, dataLen);
                    }
                }
            }
            return replacements;
        }

        private static unsafe void FindReplacesNative (char* find, int findLen, SmartList<int> replacements, char* data, int dataLen)
        {
            int i = 0;
            if (findLen == 1) {
                while (i < dataLen) {
                    if (data[i] == find[0])
                        replacements.Add(i);
                    i++;
                }
            } else {
                int* needleTable = stackalloc int[256];
                for (int j = 0; j < 256; j++)
                    needleTable[j] = findLen;

                for (int j = 1; j < findLen; j++)
                    needleTable[(byte) find[j - 1]] = findLen - j;
                int found = StringNativeHelper.StartsWith(data + i, find, needleTable, dataLen - i, findLen);
                while (found != -1) {
                    i += found;
                    replacements.Add(i);
                    i += findLen - 1;
                    found = StringNativeHelper.StartsWith(data + i, find, needleTable, dataLen - i, findLen);
                }
            }
        }

        public static bool IsNullOrEmpty (FastString value)
        {
            return value == null || value._length == 0;
        }

        public static bool IsNullOrWhiteSpace (FastString value)
        {
            int len = value == null ? 0 : value.Length;
            if (len > 0) {
                unsafe {
                    fixed (char* data = value._data) {
                        for (int i = 0; i < len; i++) {
                            if (!StringNativeHelper.IsWhiteSpace(data[i]))
                                return false;
                        }
                    }
                }
            }
            return true;
        }

        public static implicit operator string (FastString value)
        {
            if (value == null)
                return string.Empty;
            int len = value._length;
            unsafe {
                fixed (char* data = value._data) {
                    return new string(data, 0, len);
                }
            }
        }

        public static explicit operator FastString (StringBuilder value)
        {
            return new FastString(value);
        }

        public static implicit operator FastString (string value)
        {
            return new FastString(value);
        }

        public static implicit operator char[] (FastString value)
        {
            if (value == null)
                return EmptyFastString._data;
            return value._data;
        }

        public static implicit operator FastString (char[] value)
        {
            return new FastString(value);
        }

        public static implicit operator FastString (char value)
        {
            return new FastString
                (new[]
                {
                    value
                });
        }

        public static bool operator == (FastString compareTo, FastString compareWith)
        {
            return Equals(compareTo, compareWith);
        }

        public static bool operator != (FastString compareTo, FastString compareWith)
        {
            return !Equals(compareTo, compareWith);
        }

        public static bool operator == (string compareWith, FastString compareTo)
        {
            return Equals(compareTo, compareWith);
        }

        public static bool operator != (string compareWith, FastString compareTo)
        {
            return !Equals(compareTo, compareWith);
        }

        public static FastString Concat (FastString one, string two)
        {
            int lenOne = one == null ? 0 : one._length,
                lenTwo = two == null ? 0 : two.Length;

            //Do some optimiztions here, maximum two checks in any case

            if (lenOne == 0) {
                if (lenTwo == 0)
                    return EmptyFastString;
                unsafe {
                    fixed (char* src = two) {
                        return new FastString(src, lenTwo);
                    }
                }
            }
            if (lenTwo == 0)
                return one;

            var data = new char[lenOne + lenTwo];
            unsafe {
                fixed (char* dest = data) {
                    fixed (char* srcTwo = two) {
                        fixed (char* srcOne = one._data) {
                            ConcatNative(dest, srcOne, srcTwo, lenOne, lenTwo);
                        }
                    }
                }
            }
            return new FastString(data);
        }

        public static FastString Concat (FastString one, char two)
        {
            int lenOne = one == null ? 0 : one._length;
            if (lenOne == 0)
                return two;
            var data = new char[lenOne + 1];
            unsafe {
                fixed (char* dest = data) {
                    fixed (char* src = one._data) {
                        StringNativeHelper.MemCpy(dest, src, lenOne);
                        dest[lenOne] = two;
                    }
                }
            }
            return new FastString(data);
        }

        public static FastString Concat (FastString one, FastString two)
        {
            int lenOne = one == null ? 0 : one._length,
                lenTwo = two == null ? 0 : two._length;

            //Do some optimiztions here, maximum two checks in any case

            if (lenOne == 0) {
                if (lenTwo == 0)
                    return EmptyFastString;
                return two;
            }
            if (lenTwo == 0)
                return one;

            var data = new char[lenOne + lenTwo];
            unsafe {
                fixed (char* dest = data) {
                    fixed (char* srcOne = one._data) {
                        fixed (char* srcTwo = two._data) {
                            ConcatNative(dest, srcOne, srcTwo, lenOne, lenTwo);
                        }
                    }
                }
            }
            return new FastString(data);
        }

        public static FastString Concat (IEnumerable<FastString> strings)
        {
            if (strings != null) {
                var builder = new FastStringBuilder();
                foreach (FastString fastString in strings) {
                    if (fastString != null)
                        builder.Append(fastString);
                }
                return builder.ToFastString();
            }
            return EmptyFastString;
        }

        public static FastString Concat (params FastString[] fastStrings)
        {
            if (fastStrings != null) {
                int count = fastStrings.Length;

                int totalLen = 0;
                for (int i = 0; i < count; i++)
                    totalLen += fastStrings[i] == null ? 0 : fastStrings[i].Length;
                if (totalLen == 0)
                    return EmptyFastString;
                var data = new char[totalLen];
                unsafe {
                    fixed (char* dest = data) {
                        int seed = 0;
                        for (int i = 0; i < count; i++) {
                            int len = fastStrings[i] == null ? 0 : fastStrings[i].Length;
                            if (len > 0) {
                                fixed (char* src = (char[]) fastStrings[i]) {
                                    StringNativeHelper.MemCpy(dest + seed, src, len);
                                    seed += len;
                                }
                            }
                        }
                    }
                }
                return new FastString(data);
            }
            return EmptyFastString;
        }

        private static unsafe void ConcatNative (char* dest, char* one, char* two, int lenOne, int lenTwo)
        {
            StringNativeHelper.MemCpy(dest, one, lenOne);
            StringNativeHelper.MemCpy(dest + lenOne, two, lenTwo);
        }

        public static FastString Add (FastString one, FastString two)
        {
            return one + two;
        }

        public static FastString operator + (FastString one, FastString two)
        {
            return Concat(one, two);
        }

        public static FastString Add (FastString one, char two)
        {
            return one + two;
        }

        public static FastString operator + (FastString one, char two)
        {
            return Concat(one, two);
        }

        public static FastString Add (char one, FastString two)
        {
            return one + two;
        }

        public static FastString operator + (char one, FastString two)
        {
            return Concat(two, one);
        }

        public static FastString Add (FastString one, string two)
        {
            return one + two;
        }

        public static FastString operator + (FastString one, string two)
        {
            return Concat(one, two);
        }

        public static FastString Add (string one, FastString two)
        {
            return one + two;
        }

        public static FastString operator + (string one, FastString two)
        {
            return Concat(two, one);
        }

        public static FastString Increment (FastString value)
        {
            return ++value;
        }

        public static FastString operator ++ (FastString value)
        {
            return Concat(value, value);
        }

        public override int GetHashCode ()
        {
            int len = _length;
            int hash = 3571 << 16 + 3557;
            if (len > 0) {
                unsafe {
                    fixed (char* data = _data) {
                        while (len >= 1) {
                            hash ^= data[len] * 3541 + data[len - 1] * 3559 * 3547;
                            len--;
                        }
                        hash ^= data[len] * 3539;
                    }
                }
            }
            return hash;
        }

        public override bool Equals (object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;

            var str = obj as string;
            if (str != null)
                return Equals(str);
            var chr = obj as char[];
            if (chr != null)
                return Equals(chr);
            var fastString = obj as FastString;
            if (fastString != null)
                return Equals(fastString);
            return false;
        }

        public static bool Equals (FastString one, FastString another)
        {
            if (ReferenceEquals(one, another))
                return true;
            if ((object) one == null || (object) another == null)
                return false;
            return one.Equals(another);
        }

        //NULL native strings and NULL FastStrings are equals
        public static bool Equals (string one, FastString another)
        {
            if ((object) one == null && (object) another == null)
                return true;
            return (object) another != null && another.Equals(one);
        }
    }
}