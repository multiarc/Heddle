using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Templates.Collections;
using Templates.Native;
using Templates.Strings.Core;

namespace Templates.Strings {
#if !NETSTANDARD1_5
    [Serializable]
#endif
    public sealed class ExStringBuilder {
        private static readonly Allocate AllocateString;

        private readonly SmartList<MutableString> _appendStrings = new SmartList<MutableString>();
        private int _appendlength;
        private string _data;

        public ExStringBuilder (ExString value)
        {
            _data = value ?? ExString.Empty;
        }

        public ExStringBuilder (string value)
        {
            _data = value ?? string.Empty;
        }

        public ExStringBuilder ()
        {
            _data = string.Empty;
        }

        static ExStringBuilder()
        {
            var method = typeof(string).GetMethod("FastAllocateString", BindingFlags.Static | BindingFlags.NonPublic);
            AllocateString = (Allocate)method.CreateDelegate(typeof(Allocate));
        }

        private int Capacity
        {
            set
            {
                if (_data.Length != value)
                {
                    string old = _data;
                    unsafe
                    {
                        int newLen = value;
                        int oldLen = _data.Length;
                        _data = AllocateString(newLen);
                        fixed (char* dest = _data)
                        {
                            fixed (char* src = old)
                            {
                                MemCpy(dest, src, oldLen);
                            }
                        }
                    }
                }
            }
        }

        public int Length => _data.Length + _appendlength;

        public void Clear ()
        {
            _appendlength = 0;
            _appendStrings.Clear();
            _data = ExString.Empty;
        }

        private void CommitAppend()
        {
            if (_appendlength != 0)
            {
                int seed = _data.Length;
                int newLength = _data.Length + _appendlength;
                for (int i = 0; i < _appendStrings.Length; i++)
                {
                    if (_data.Length < newLength)
                        Capacity = newLength;
                    int len = _appendStrings[i].Length;
                    unsafe
                    {
                        fixed (char* dest = _data)
                        {
                            if (_appendStrings[i].IsExString)
                            {
                                fixed (char* src = _appendStrings[i].ExStringValue.Data)
                                {
                                    MemCpy(dest + seed, src, len);
                                }
                            }
                            else
                            {
                                fixed (char* src = _appendStrings[i].StringValue)
                                {
                                    MemCpy(dest + seed, src, len);
                                }
                            }
                        }
                    }
                    seed += len;
                    _appendStrings[i] = string.Empty;
                }
                _appendStrings.Clear();
                _appendlength = 0;
            }
        }

        public void Append (string value)
        {
            if (!string.IsNullOrEmpty(value)) {
                _appendStrings.Add(value);
                _appendlength += value.Length;
            }
        }

        public static ExStringBuilder operator +(ExStringBuilder builder, string value)
        {
            builder.Append(value);
            return builder;
        }

        public static ExStringBuilder operator +(ExStringBuilder builder, ExString value)
        {
            builder.Append(value);
            return builder;
        }

        public static ExStringBuilder operator +(ExStringBuilder builder, ExStringBuilder value)
        {
            builder.Append(value.ToString());
            return builder;
        }

        public void Append(string format, params object[] args)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (!string.IsNullOrEmpty(format))
            {
                string formatted = string.Format(format, args);
                _appendStrings.Add(formatted);
                _appendlength += formatted.Length;
            }
        }

        public void Append (ExString value)
        {
            if (!ExString.IsNullOrEmpty(value)) {
                _appendStrings.Add(value);
                _appendlength += value.Length;
            }
        }

        public override string ToString ()
        {
            CommitAppend();
            return _data;
        }

        public ExString ToExString ()
        {
            CommitAppend();
            return _data;
        }

        public ExString BulkReplace (Replacement[] replacements, int takeLength)
        {
            if (_appendStrings.Length > 0)
                CommitAppend();
            if (replacements == null)
                throw new ArgumentNullException(nameof(replacements));

            if (takeLength == 0)
                return _data;

            if (_data.Length == 0)
                return ExString.Empty;

            int capacity = _data.Length;
            int srcLen = capacity;
            unchecked {
                for (int i = 0; i < takeLength; i++) {
                    Replacement replacement = replacements[i];
                    if (replacement.ReplacementValue == null)
                        replacement.ReplacementValue = string.Empty;
#if DEBUG
                    if (replacement.BlockPosition.Length < 0)
                        throw new ArgumentException();
                    if (replacement.BlockPosition.StartIndex < 0)
                        throw new ArgumentException();
#endif
                    capacity += replacement.ReplacementValue.Length - replacement.BlockPosition.Length;
#if DEBUG
                    if (capacity < 0)
                        throw new ArgumentException();
#endif
                }

                if (capacity == 0)
                    return ExString.Empty;

                var result = new char[capacity];

                unsafe {
                    fixed (char* dest = result) {
                        fixed (char* src = _data) {
                            MoveData(replacements, takeLength, srcLen, capacity, dest, src);
                        }
                    }
                }
                return result;
            }
        }

        internal static unsafe string BulkReplace(char** values, int* lengths, BlockPosition[] positions, string document)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (positions == null)
                throw new ArgumentNullException(nameof(positions));
            var count = positions.Length;
            if (count == 0)
                return document;

            if (document.Length == 0)
                return string.Empty;

            int capacity = document.Length;
            int srcLen = capacity;
            unchecked
            {
                for (int i = 0; i < count; i++)
                {
                    BlockPosition block = positions[i];
#if DEBUG
                    if (block.Length < 0)
                        throw new ArgumentException();
                    if (block.StartIndex < 0)
                        throw new ArgumentException();
#endif
                    capacity += (values[i] == null ? 0 : lengths[i]) - block.Length;
#if DEBUG
                    if (capacity < 0)
                        throw new ArgumentException();
#endif
                }

                if (capacity == 0)
                    return string.Empty;
                string result = AllocateString(capacity);
                fixed (char* dest = result)
                {
                    fixed (char* src = document)
                    {
                        MoveData(values, lengths, positions, srcLen, capacity, dest, src);
                    }
                }
                return result;
            }
        }

        public static string BulkReplace (Replacement[] replacements, int takeLength, string source)
        {
            if (replacements == null)
                throw new ArgumentNullException(nameof(replacements));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (takeLength == 0)
                return source;

            //if (source.Length == 0)
            //    return string.Empty;

            int capacity = source.Length;
            int srcLen = capacity;
            unchecked {
                for (int i = 0; i < takeLength; i++) {
                    Replacement replacement = replacements[i];
#if DEBUG
                    if (replacement.BlockPosition.Length < 0)
                        throw new ArgumentException();
                    if (replacement.BlockPosition.StartIndex < 0)
                        throw new ArgumentException();
#endif
                    capacity += (replacement.ReplacementValue?.Length ?? 0) - replacement.BlockPosition.Length;
#if DEBUG
                    if (capacity < 0)
                        throw new ArgumentException();
#endif
                }

                if (capacity == 0)
                    return string.Empty;
                string result = AllocateString(capacity);
                unsafe {
                    fixed (char* dest = result) {
                        fixed (char* src = source) {
                            MoveData(replacements, takeLength, srcLen, capacity, dest, src);
                        }
                    }
                }
                return result;
            }
        }

        public static string BulkReplace (Replacement[] replacements, int takeLength, ExString source)
        {
            if (replacements == null)
                throw new ArgumentNullException(nameof(replacements));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (takeLength == 0)
                return source;

            //if (source.Length == 0)
            //    return string.Empty;

            int capacity = source.Length;
            int srcLen = capacity;
            unchecked {
                for (int i = 0; i < takeLength; i++) {
                    Replacement replacement = replacements[i];
#if DEBUG
                    if (replacement.BlockPosition.Length < 0)
                        throw new ArgumentException();
                    if (replacement.BlockPosition.StartIndex < 0)
                        throw new ArgumentException();
#endif
                    capacity += (replacement.ReplacementValue != null ? replacement.ReplacementValue.Length : 0) - replacement.BlockPosition.Length;
#if DEBUG
                    if (capacity < 0)
                        throw new ArgumentException();
#endif
                }

                if (capacity == 0)
                    return string.Empty;
                string result = AllocateString(capacity);
                unsafe {
                    fixed (char* dest = result) {
                        fixed (char* src = (char[]) source) {
                            MoveData(replacements, takeLength, srcLen, capacity, dest, src);
                        }
                    }
                }
                return result;
            }
        }

        private static unsafe void MoveData(char** values, int* lengths, BlockPosition[] positions, int srcLen, int capacity, char* dest, char* src)
        {
            int lastIndex = 0;
            int current = 0;
            var count = positions.Length;
            for (int i = 0; i < count; i++)
            {
                int chunkLength = lengths[i];
                BlockPosition block = positions[i];
#if DEBUG
                if (lastIndex + block.StartIndex - current + chunkLength < 0
                    || lastIndex + block.StartIndex - current + chunkLength > capacity || block.StartIndex > srcLen
                    || current > srcLen)
                    throw new ArgumentException();
#endif
                char* middle = values[i];
                MemCpy(dest + lastIndex, src + current, block.StartIndex - current);
                lastIndex += block.StartIndex - current;
                if (middle != null)
                {
                    MemCpy(dest + lastIndex, middle, chunkLength);
                    current = block.StartIndex + block.Length;
                    lastIndex += chunkLength;
                }
                else
                {
                    current = block.StartIndex + block.Length;
#if DEBUG
                    if (chunkLength > 0)
                        throw new ArgumentException();

#endif
                }
            }
#if DEBUG
            if (lastIndex + srcLen - current < 0 || lastIndex + srcLen - current > capacity || current > srcLen)
                throw new ArgumentException();
#endif
            MemCpy(dest + lastIndex, src + current, srcLen - current);
        }

        private static unsafe void MoveData (Replacement[] replacements, int takeLength, int srcLen, int capacity, char* dest, char* src)
        {
            int lastIndex = 0;
            int current = 0;
            for (int i = 0; i < takeLength; i++) {
                Replacement replacement = replacements[i];
                string replacementString = replacement.ReplacementValue ?? string.Empty;
                int chunkLength = replacementString.Length;
#if DEBUG
                if (lastIndex + replacement.BlockPosition.StartIndex - current + chunkLength < 0
                    || lastIndex + replacement.BlockPosition.StartIndex - current + chunkLength > capacity || replacement.BlockPosition.StartIndex > srcLen
                    || current > srcLen)
                    throw new ArgumentException();
#endif
                fixed (char* middle = replacementString) {
                    MemCpy(dest + lastIndex, src + current, replacement.BlockPosition.StartIndex - current);
                    lastIndex += replacement.BlockPosition.StartIndex - current;

                    MemCpy(dest + lastIndex, middle, chunkLength);
                    current = replacement.BlockPosition.StartIndex + replacement.BlockPosition.Length;
                    lastIndex += chunkLength;
                }
            }
#if DEBUG
            if (lastIndex + srcLen - current < 0 || lastIndex + srcLen - current > capacity || current > srcLen)
                throw new ArgumentException();
#endif
            MemCpy(dest + lastIndex, src + current, srcLen - current);
        }

        public static string Replace (int start, int length, string replacement, string source)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int sourceLen = source.Length;
            int replacementLength = replacement.Length;
            unchecked {
#if DEBUG
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
#endif
                int newLen = sourceLen - length + replacement.Length;
                string destination = AllocateString(newLen);
                unsafe {
                    fixed (char* dest = destination) {
                        fixed (char* src = source) {
                            fixed (char* repl = replacement) {
                                if (start > 0)
                                    MemCpy(dest, src, start);
                                MemCpy(dest + start, repl, replacementLength);
                                MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
                            }
                        }
                    }
                }
                return destination;
            }
        }

        public static ExString Replace (int start, int length, string replacement, ExString source)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int sourceLen = source.Length;
            int replacementLength = replacement.Length;
            unchecked {
#if DEBUG
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
#endif
                int newLen = sourceLen - length + replacement.Length;
                var destination = new char[newLen];
                unsafe {
                    fixed (char* dest = destination) {
                        fixed (char* src = (char[]) source) {
                            fixed (char* repl = replacement) {
                                if (start > 0)
                                    MemCpy(dest, src, start);
                                MemCpy(dest + start, repl, replacementLength);
                                MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
                            }
                        }
                    }
                }
                return new ExString(destination);
            }
        }

        public static ExString Replace (int start, int length, ExString replacement, ExString source)
        {
            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int sourceLen = source.Length;
            int replacementLength = replacement.Length;
            unchecked {
#if DEBUG
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
#endif
                int newLen = sourceLen - length + replacement.Length;
                var destination = new char[newLen];
                unsafe {
                    fixed (char* dest = destination) {
                        fixed (char* src = (char[]) source) {
                            fixed (char* repl = (char[]) replacement) {
                                if (start > 0)
                                    MemCpy(dest, src, start);
                                MemCpy(dest + start, repl, replacementLength);
                                MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
                            }
                        }
                    }
                }
                return new ExString(destination);
            }
        }

        public ExString Replace (int start, int length, string replacement)
        {
            if (_appendStrings.Length > 0)
                CommitAppend();

            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));

            int sourceLen = _data.Length;
            int replacementLength = replacement.Length;
            unchecked {
#if DEBUG
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
#endif
                int newLen = sourceLen - length + replacement.Length;
                var destination = new char[newLen];
                unsafe {
                    fixed (char* dest = destination) {
                        fixed (char* src = _data) {
                            fixed (char* repl = replacement) {
                                if (start > 0)
                                    MemCpy(dest, src, start);
                                MemCpy(dest + start, repl, replacementLength);
                                MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
                            }
                        }
                    }
                }
                return new ExString(destination);
            }
        }

        public static int ApplyRemove(BlockPosition element, ref string source) {
            int removeStart = element.StartIndex;
            int removeLength = element.Length;
            source = Replace(removeStart, removeLength, string.Empty, source);
            return removeLength;
        }

        public ExString Replace (int start, int length, ExString replacement)
        {
            if (_appendStrings.Length > 0)
                CommitAppend();

            if (replacement == null)
                throw new ArgumentNullException(nameof(replacement));

            int sourceLen = _data.Length;
            int replacementLength = replacement.Length;
            unchecked {
#if DEBUG
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
#endif
                int newLen = sourceLen - length + replacement.Length;
                var destination = new char[newLen];
                unsafe {
                    fixed (char* dest = destination) {
                        fixed (char* src = _data) {
                            fixed (char* repl = (char[]) replacement) {
                                if (start > 0)
                                    MemCpy(dest, src, start);
                                MemCpy(dest + start, repl, replacementLength);
                                MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
                            }
                        }
                    }
                }
                return new ExString(destination);
            }
        }

#if !NETSTANDARD1_5
        internal static unsafe void MemCpy(char* dmem, char* smem, int len)
        {
            //len *= 2;
            //if (len >= 16)
            //{
            //    do
            //    {
            //        ((long*)dmem)[0] = ((long*)smem)[0]; 
            //        ((long*)dmem)[1] = ((long*)smem)[1];
            //        dmem += 8;
            //        smem += 8;
            //    } while ((len -= 16) >= 16);
            //}
            //if (len > 0)
            //{
            //    if ((len & 8) != 0)
            //    {
            //        ((long*) dmem)[0] = ((long*) smem)[0];
            //        dmem += 4;
            //        smem += 4;
            //    }
            //    if ((len & 4) != 0)
            //    {
            //        ((int*) dmem)[0] = ((int*) smem)[0];
            //        dmem += 2;
            //        smem += 2;
            //    }
            //    if ((len & 2) != 0)
            //    {
            //        *dmem = *smem;
            //    }
            //}

            if (len > 0)
            {
                if ((((int)dmem | (int)smem) & 1) == 0)
                {
                    if (((int)dmem & 2) != 0)
                    {
                        dmem[0] = smem[0];
                        dmem += 1;
                        smem += 1;
                        len -= 1;
                    }
                    if ((((int)dmem & 4) != 0) && (len >= 2))
                    {
                        {
                            ((uint*)dmem)[0] = ((uint*)smem)[0];
                        }
                        dmem += 2;
                        smem += 2;
                        len -= 2;
                    }
                    while (len >= 16)
                    {
                        ((ulong*)dmem)[0] = ((ulong*)smem)[0];
                        ((ulong*)dmem)[1] = ((ulong*)smem)[1];
                        ((ulong*)dmem)[2] = ((ulong*)smem)[2];
                        ((ulong*)dmem)[3] = ((ulong*)smem)[3];
                        dmem += 16;
                        smem += 16;
                        len -= 16;
                    }
                    if ((len & 8) != 0)
                    {
                        ((ulong*)dmem)[0] = ((ulong*)smem)[0];
                        ((ulong*)dmem)[1] = ((ulong*)smem)[1];
                        dmem += 8;
                        smem += 8;
                    }
                    if ((len & 4) != 0)
                    {
                        ((ulong*)dmem)[0] = ((ulong*)smem)[0];
                        dmem += 4;
                        smem += 4;
                    }
                    if ((len & 2) != 0)
                    {
                        ((uint*)dmem)[0] = ((uint*)smem)[0];
                        dmem += 2;
                        smem += 2;
                    }
                    if ((len & 1) != 0)
                    {
                        dmem[0] = smem[0];
                    }
                }
                else
                {
                    // This is rare case where at least one of the pointers is only byte aligned. 
                    do
                    {
                        ((byte*)dmem)[0] = ((byte*)smem)[0];
                        ((byte*)dmem)[1] = ((byte*)smem)[1];
                        len -= 1;
                        dmem += 1;
                        smem += 1;
                    } while (len > 0);
                }
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void MemCpy(char* dmem, char* smem, int charCount)
        {
            Buffer.MemoryCopy(smem, dmem, charCount*2, charCount*2);
        }
#endif

        internal static unsafe int Equals(char* one, char* two, int lenOne, int lenTwo)
        {
            if (one == two)
            {
                return 0;
            }
            char* a = one;
            char* b = two;
            int length = lenOne <= lenTwo ? lenOne : lenTwo;
            if (lenOne != lenTwo)
                return lenOne - lenTwo;
            while (length > 0 && *a == *b)
            {
                a++;
                b++;
                length--;
            }

            if (length > 0)
            {
                return *a - *b;
            }
            return 0;
        }


        internal static unsafe int StartsWith(char* data, char* find, int* needleTable, int dataLen, int findLen)
        {
            if (dataLen >= findLen)
            {
                int found = 0;
                int currentIndex = findLen - 1;
                int counter = currentIndex;

                while (counter >= 0 && currentIndex < dataLen)
                {
                    counter = findLen - 1;
                    found = currentIndex;
                    while (counter >= 0 && data[found] == find[counter])
                    {
                        found--;
                        counter--;
                    }
                    currentIndex += needleTable[(sbyte)data[currentIndex]];
                }
                found++;
                if (found <= dataLen - findLen)
                    return found;
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\x00a0' || c == '\x0085' || c >= '\x0009' && c <= '\x000d';
        }

    }
}