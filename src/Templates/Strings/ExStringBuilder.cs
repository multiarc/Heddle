using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Templates.Native;
using Templates.Strings.Core;

namespace Templates.Strings {
    public sealed class ExStringBuilder {
        private static readonly Allocate AllocateString;

        private readonly List<string> _appendStrings = new List<string>();
        private int _appendLength;
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
                            var destSpan = new Span<char>(dest, oldLen);
                            
                            fixed (char* src = old)
                            {
                                var srcSpan = new Span<char>(src, oldLen);
                                srcSpan.CopyTo(destSpan);
                            }
                        }
                    }
                }
            }
        }

        public int Length => _data.Length + _appendLength;

        public void Clear ()
        {
            _appendLength = 0;
            _appendStrings.Clear();
        }

        private void CommitAppend()
        {
            if (_appendLength != 0)
            {
                int seed = _data.Length;
                int newLength = _data.Length + _appendLength;
                if (_data.Length < newLength)
                    Capacity = newLength;

                unsafe
                {
                    fixed (char* dest = _data)
                    {
                        var destSpan = new Span<char>(dest, _data.Length);
                        foreach (var appendString in _appendStrings)
                        {
                            int len = appendString.Length;

                            fixed (char* src = appendString)
                            {
                                var srcSpan = new Span<char>(src, len);
                                srcSpan.CopyTo(destSpan.Slice(seed));
                            }

                            seed += len;
                        }
                    }
                }

                _appendStrings.Clear();
                _appendLength = 0;
            }
        }

        public void Append (string value)
        {
            if (!string.IsNullOrEmpty(value)) {
                _appendStrings.Add(value);
                _appendLength += value.Length;
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
                _appendLength += formatted.Length;
            }
        }

        public override string ToString ()
        {
            CommitAppend();
            return _data;
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
                            var destSpan = new Span<char>(dest, capacity);
                            var srcSpan = new Span<char>(src, srcLen);
                            MoveData(replacements, srcLen, destSpan, srcSpan);
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
                        var destSpan = new Span<char>(dest, capacity);
                        fixed (char* src = source)
                        {
                            var srcSpan = new Span<char>(src, srcLen);
                            MoveData(replacements, srcLen, destSpan, srcSpan);
                        }
                    }
                }

                return result;
            }
        }

        public static string Concat(IList<string> list, int fullLength)
        {
            var array = list;

            var result = AllocateString(fullLength);
            unsafe
            {
                fixed (char* buffer = result)
                {
                    var span = new Span<char>(buffer, fullLength);

                    var seed = 0;
                    for (var i = 0; i < list.Count; i++)
                    {
                        var str = array[i];
                        fixed (char* value = str)
                        {
                            var spanValue = new Span<char>(value, str.Length);
                            spanValue.CopyTo(span.Slice(seed));
                            seed += str.Length;
                        }
                    }

                    return result;
                }
            }
        }

        public static string Concat(string[] array, int fullLength)
        {
            return Concat(array, array.Length, fullLength);
        }

        public static string Concat(string[] array, int takeCount, int fullLength)
        {
            var result = AllocateString(fullLength);
            unsafe
            {
                fixed (char* buffer = result)
                {
                    var span = new Span<char>(buffer, fullLength);

                    var seed = 0;
                    for (var i = 0; i < takeCount; i++)
                    {
                        var src = array[i];
                        src.AsSpan().CopyTo(span.Slice(seed));
                        seed += src.Length;
                    }

                    return result;
                }
            }
        }

        private static unsafe void MoveData(IList<Replacement> replacements, int srcLen, in Span<char> dest, in Span<char> src)
        {
            int lastIndex = 0;
            int current = 0;
            foreach (Replacement replacement in replacements)
            {
                string replacementString = replacement.ReplacementValue ?? string.Empty;
                int chunkLength = replacementString.Length;
                fixed (char* middle = replacementString)
                {
                    var middleSpan = new Span<char>(middle, chunkLength);
                    var len = replacement.BlockPosition.StartIndex - current;
                    src.Slice(current, len).CopyTo(dest.Slice(lastIndex));
                    lastIndex += replacement.BlockPosition.StartIndex - current;

                    middleSpan.CopyTo(dest.Slice(lastIndex));
                    current = replacement.BlockPosition.StartIndex + replacement.BlockPosition.Length;
                    lastIndex += chunkLength;
                }
            }

            src.Slice(current, srcLen - current).CopyTo(dest.Slice(lastIndex));
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
                                var destSpan = new Span<char>(dest, newLen);
                                var srcSpan = new Span<char>(src, sourceLen);
                                var replSpan = new Span<char>(repl, replacementLength);
                                if (start > 0)
                                    srcSpan.Slice(0, start).CopyTo(destSpan);
                                replSpan.CopyTo(destSpan.Slice(start));
                                srcSpan.Slice(start + length, sourceLen - start - length).CopyTo(destSpan.Slice(start + replacementLength));
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