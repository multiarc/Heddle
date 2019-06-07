using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Templates.Native;
using Templates.Strings.Core;

namespace Templates.Strings {
    public sealed class ExStringBuilder {
        private static readonly Allocate AllocateString;

        private readonly List<string> _appendStrings = new List<string>();
        private int _appendlength;
        private string _data;

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
            var fastAllocateMethod = typeof(string).GetMethod("FastAllocateString", BindingFlags.Static | BindingFlags.NonPublic);
            AllocateString = (Allocate) fastAllocateMethod.CreateDelegate(typeof(Allocate));
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
        }

        private void CommitAppend()
        {
            if (_appendlength != 0)
            {
                int seed = _data.Length;
                int newLength = _data.Length + _appendlength;
                foreach (var appendString in _appendStrings)
                {
                    if (_data.Length < newLength)
                        Capacity = newLength;
                    int len = appendString.Length;
                    unsafe
                    {
                        fixed (char* dest = _data)
                        {
                            fixed (char* src = appendString)
                            {
                                MemCpy(dest + seed, src, len);
                            }
                        }
                    }
                    seed += len;
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

        public override string ToString ()
        {
            CommitAppend();
            return _data;
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

        public static string BulkReplace (Replacement[] replacements, string source)
        {
            if (replacements == null)
                throw new ArgumentNullException(nameof(replacements));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (replacements.Length == 0)
                return source;

            //if (source.Length == 0)
            //    return string.Empty;

            int capacity = source.Length;
            int srcLen = capacity;
            unchecked {
                foreach (Replacement replacement in replacements)
                {
#if DEBUG
                    if (replacement.BlockPosition.Length < 0)
                        throw new ArgumentException();
                    if (replacement.BlockPosition.StartIndex < 0)
                        throw new ArgumentException();
#endif
                    capacity += (replacement.ReplacementValue?.Length ?? 0) - replacement.BlockPosition.Length;
                }

                if (capacity == 0)
                    return string.Empty;
                string result = AllocateString(capacity);
                unsafe {
                    fixed (char* dest = result) {
                        fixed (char* src = source)
                        {
                            MoveData(replacements, srcLen, dest, src);
                        }
                    }
                }
                return result;
            }
        }

        public static string BulkReplace(IList<Replacement> replacements, string source)
        {
            if (replacements == null)
                throw new ArgumentNullException(nameof(replacements));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (replacements.Count == 0)
                return source;

            //if (source.Length == 0)
            //    return string.Empty;

            int capacity = source.Length;
            int srcLen = capacity;
            unchecked
            {
                foreach (Replacement replacement in replacements)
                {
#if DEBUG
                    if (replacement.BlockPosition.Length < 0)
                        throw new ArgumentException();
                    if (replacement.BlockPosition.StartIndex < 0)
                        throw new ArgumentException();
#endif
                    capacity += (replacement.ReplacementValue?.Length ?? 0) - replacement.BlockPosition.Length;
                }

                if (capacity == 0)
                    return string.Empty;
                string result = AllocateString(capacity);
                unsafe
                {
                    fixed (char* dest = result)
                    {
                        fixed (char* src = source)
                        {
                            MoveData(replacements, srcLen, dest, src);
                        }
                    }
                }
                return result;
            }
        }

        public static string ConcatList(IEnumerable<string> list, int fullLength)
        {
            return ConcatListInternal(list, fullLength);
        }

        public static string ConcatList(IList<string> list)
        {
            var fullLength = 0;

            foreach (var item in list)
            {
                fullLength += item.Length;
            }

            return ConcatListInternal(list, fullLength);
        }

        private static unsafe string ConcatListInternal(IEnumerable<string> list, int fullLength)
        {
            int seed = 0;
            string result = AllocateString(fullLength);

            fixed (char* dest = result)
            {
                foreach (var item in list)
                {
                    var itemLength = item.Length;

                    fixed (char* src = item)
                    {
                        MemCpy(dest + seed, src, itemLength);
                    }

                    seed += itemLength;
                }
            }

            return result;
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

        private static unsafe void MoveData(IList<Replacement> replacements, int srcLen, char* dest, char* src)
        {
            int lastIndex = 0;
            int current = 0;
            foreach (Replacement replacement in replacements)
            {
                string replacementString = replacement.ReplacementValue ?? string.Empty;
                int chunkLength = replacementString.Length;
                fixed (char* middle = replacementString)
                {
                    MemCpy(dest + lastIndex, src + current, replacement.BlockPosition.StartIndex - current);
                    lastIndex += replacement.BlockPosition.StartIndex - current;

                    MemCpy(dest + lastIndex, middle, chunkLength);
                    current = replacement.BlockPosition.StartIndex + replacement.BlockPosition.Length;
                    lastIndex += chunkLength;
                }
            }
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

        public static int ApplyRemove(BlockPosition element, ref string source) {
            int removeStart = element.StartIndex;
            int removeLength = element.Length;
            source = Replace(removeStart, removeLength, string.Empty, source);
            return removeLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void MemCpy(char* dmem, char* smem, int charCount)
        {
            Buffer.MemoryCopy(smem, dmem, charCount*2, charCount*2);
        }

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