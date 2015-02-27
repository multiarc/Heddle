using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NativeFunctions;
using Templates.Collections;

namespace Templates.Strings {
    [Serializable]
    public sealed partial class ExString {
        private static readonly ExString EmptyExString = new ExString(new char[0]);
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

        public ExString (char[] value, int length)
        {
            if (value == null) {
                if (length != 0)
                    throw new ArgumentNullException("value");
                _data = EmptyExString._data;
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

        public ExString ()
        {
            _data = EmptyExString._data;
            _length = _data.Length;
        }

        public ExString (char[] value)
        {
            _data = value ?? EmptyExString._data;
            _length = _data.Length;
        }

        private unsafe ExString (char* value, int length)
        {
            if (length < 0)
                throw new ArgumentException("length should not be less than zero");

            if (value == null) {
                if (length != 0)
                    throw new ArgumentNullException("value");
                _data = EmptyExString._data;
            } else {
                _data = new char[length];
                fixed (char* dest = _data) {
                    StringNativeHelper.MemCpy(dest, value, length);
                }
            }
            _length = _data.Length;
        }

        public ExString (string value)
        {
            if (string.IsNullOrEmpty(value)) {
                _data = EmptyExString._data;
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

        public ExString (StringBuilder value)
        {
            if (value == null)
                _data = EmptyExString._data;
            else {
                int len = value.Length;
                if (len == 0)
                    _data = EmptyExString._data;
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

        public static ExString Empty
        {
            get { return EmptyExString; }
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

        public ExString Trim ()
        {
            unsafe {
                fixed (char* src = _data) {
                    return FastTrim(src, _length);
                }
            }
        }

        private static unsafe ExString FastTrim (char* src, int len)
        {
            int startIndex = 0;
            for (int i = 0; i < len; i++) {
                if (!StringNativeHelper.IsWhiteSpace(src[i])) {
                    startIndex = i;
                    break;
                }
            }
            if (startIndex == len - 1)
                return EmptyExString;
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
            return new ExString(result);
        }

        private ExString BulkReplace (int[] foundIndexes, ExString toReplace, int findStringLen)
        {
            int count = foundIndexes.Length,
                chunkLength = toReplace.Length;
            if (count == 0)
                return new ExString(_data);

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

        private static unsafe void MoveData (int[] foundIndexes, ExString toReplace, int findStringLen, char* src, char* dest, int srcLen)
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

        public ExString Replace (ExString toFind, ExString toReplace)
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

        private int[] FindForReplace (ExString toFind)
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

        public static bool IsNullOrEmpty (ExString value)
        {
            return value == null || value._length == 0;
        }

        public static bool IsNullOrWhiteSpace (ExString value)
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

        public static implicit operator string (ExString value)
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

        public static explicit operator ExString (StringBuilder value)
        {
            return new ExString(value);
        }

        public static implicit operator ExString (string value)
        {
            return new ExString(value);
        }

        public static implicit operator char[] (ExString value)
        {
            if (value == null)
                return EmptyExString._data;
            return value._data;
        }

        public static implicit operator ExString (char[] value)
        {
            return new ExString(value);
        }

        public static implicit operator ExString (char value)
        {
            return new ExString
                (new[]
                {
                    value
                });
        }

        public static bool operator == (ExString compareTo, ExString compareWith)
        {
            return Equals(compareTo, compareWith);
        }

        public static bool operator != (ExString compareTo, ExString compareWith)
        {
            return !Equals(compareTo, compareWith);
        }

        public static bool operator == (string compareWith, ExString compareTo)
        {
            return Equals(compareTo, compareWith);
        }

        public static bool operator != (string compareWith, ExString compareTo)
        {
            return !Equals(compareTo, compareWith);
        }

        public static ExString Concat (ExString one, string two)
        {
            int lenOne = one == null ? 0 : one._length,
                lenTwo = two == null ? 0 : two.Length;

            //Do some optimiztions here, maximum two checks in any case

            if (lenOne == 0) {
                if (lenTwo == 0)
                    return EmptyExString;
                unsafe {
                    fixed (char* src = two) {
                        return new ExString(src, lenTwo);
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
            return new ExString(data);
        }

        public static ExString Concat (ExString one, char two)
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
            return new ExString(data);
        }

        public static ExString Concat (ExString one, ExString two)
        {
            int lenOne = one == null ? 0 : one._length,
                lenTwo = two == null ? 0 : two._length;

            //Do some optimiztions here, maximum two checks in any case

            if (lenOne == 0) {
                if (lenTwo == 0)
                    return EmptyExString;
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
            return new ExString(data);
        }

        public static ExString Concat (IEnumerable<ExString> strings)
        {
            if (strings != null) {
                var builder = new ExStringBuilder();
                foreach (ExString fastString in strings) {
                    if (fastString != null)
                        builder.Append(fastString);
                }
                return builder.ToExString();
            }
            return EmptyExString;
        }

        public static ExString Concat (params ExString[] exStrings)
        {
            if (exStrings != null) {
                int count = exStrings.Length;

                int totalLen = 0;
                for (int i = 0; i < count; i++)
                    totalLen += exStrings[i] == null ? 0 : exStrings[i].Length;
                if (totalLen == 0)
                    return EmptyExString;
                var data = new char[totalLen];
                unsafe {
                    fixed (char* dest = data) {
                        int seed = 0;
                        for (int i = 0; i < count; i++) {
                            int len = exStrings[i] == null ? 0 : exStrings[i].Length;
                            if (len > 0) {
                                fixed (char* src = (char[]) exStrings[i]) {
                                    StringNativeHelper.MemCpy(dest + seed, src, len);
                                    seed += len;
                                }
                            }
                        }
                    }
                }
                return new ExString(data);
            }
            return EmptyExString;
        }

        private static unsafe void ConcatNative (char* dest, char* one, char* two, int lenOne, int lenTwo)
        {
            StringNativeHelper.MemCpy(dest, one, lenOne);
            StringNativeHelper.MemCpy(dest + lenOne, two, lenTwo);
        }

        public static ExString Add (ExString one, ExString two)
        {
            return one + two;
        }

        public static ExString operator + (ExString one, ExString two)
        {
            return Concat(one, two);
        }

        public static ExString Add (ExString one, char two)
        {
            return one + two;
        }

        public static ExString operator + (ExString one, char two)
        {
            return Concat(one, two);
        }

        public static ExString Add (char one, ExString two)
        {
            return one + two;
        }

        public static ExString operator + (char one, ExString two)
        {
            return Concat(two, one);
        }

        public static ExString Add (ExString one, string two)
        {
            return one + two;
        }

        public static ExString operator + (ExString one, string two)
        {
            return Concat(one, two);
        }

        public static ExString Add (string one, ExString two)
        {
            return one + two;
        }

        public static ExString operator + (string one, ExString two)
        {
            return Concat(two, one);
        }

        public static ExString Increment (ExString value)
        {
            return ++value;
        }

        public static ExString operator ++ (ExString value)
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
            var fastString = obj as ExString;
            if (fastString != null)
                return Equals(fastString);
            return false;
        }

        public static bool Equals (ExString one, ExString another)
        {
            if (ReferenceEquals(one, another))
                return true;
            if ((object) one == null || (object) another == null)
                return false;
            return one.Equals(another);
        }

        //NULL native strings and NULL FastStrings are equals
        public static bool Equals (string one, ExString another)
        {
            if ((object) one == null && (object) another == null)
                return true;
            return (object) another != null && another.Equals(one);
        }
    }
}