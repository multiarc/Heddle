using System;
using System.Runtime.InteropServices;
using Templates.Collections;
using Templates.Native;
using Templates.Strings.Core;

namespace Templates.Strings {
#if !DNXCORE50
    [Serializable]
#endif
    public sealed class ExStringBuilder {
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

        private int Capacity
        {
            set
            {
                if (_data.Length != value) {
                    string old = _data;
                    unsafe {
                        int newLen = value;
                        int oldLen = _data.Length;
#if DNXCORE50 || DNX451
                        _data = new string('\0', newLen);
#else
                        _data = NativeHelper.AllocateString(newLen);
#endif
                        fixed (char* dest = _data) {
                            fixed (char* src = old) {
                                NativeHelper.MemCpy(dest, src, oldLen);
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
                                    NativeHelper.MemCpy(dest + seed, src, len);
                                }
                            }
                            else
                            {
                                fixed (char* src = _appendStrings[i].StringValue)
                                {
                                    NativeHelper.MemCpy(dest + seed, src, len);
                                }
                            }
                        }
                    }
                    seed += len;
                    //_appendStrings[i] = string.Empty;
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

        public ExString BulkReplace (Replacement[] replacements)
        {
            if (_appendStrings.Length > 0)
                CommitAppend();
            if (replacements == null)
                throw new ArgumentNullException(nameof(replacements));

            int count = replacements.Length;

            if (count == 0)
                return _data;

            if (_data.Length == 0)
                return ExString.Empty;

            int capacity = _data.Length;
            int srcLen = capacity;
            unchecked {
                for (int i = 0; i < count; i++) {
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
                            MoveData(replacements, srcLen, capacity, dest, src);
                        }
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

            int count = replacements.Length;

            if (count == 0)
                return source;

            if (source.Length == 0)
                return string.Empty;

            int capacity = source.Length;
            int srcLen = capacity;
            unchecked {
                for (int i = 0; i < count; i++) {
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
#if DNXCORE50 || DNX451
                string result = new string('\0', capacity);
#else
                string result = NativeHelper.AllocateString(capacity);
#endif
                unsafe {
                    fixed (char* dest = result) {
                        fixed (char* src = source) {
                            MoveData(replacements, srcLen, capacity, dest, src);
                        }
                    }
                }
                return result;
            }
        }

        public static string BulkReplace (Replacement[] replacements, ExString source)
        {
            if (replacements == null)
                throw new ArgumentNullException(nameof(replacements));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int count = replacements.Length;

            if (count == 0)
                return source;

            if (source.Length == 0)
                return string.Empty;

            int capacity = source.Length;
            int srcLen = capacity;
            unchecked {
                for (int i = 0; i < count; i++) {
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
#if DNXCORE50 || DNX451
                string result = new string('\0', capacity);
#else
                string result = NativeHelper.AllocateString(capacity);
#endif

                unsafe {
                    fixed (char* dest = result) {
                        fixed (char* src = (char[]) source) {
                            MoveData(replacements, srcLen, capacity, dest, src);
                        }
                    }
                }
                return result;
            }
        }

        private static unsafe void MoveData (Replacement[] replacements, int srcLen, int capacity, char* dest, char* src)
        {
            int lastIndex = 0;
            int current = 0;
            int count = replacements.Length;
            for (int i = 0; i < count; i++) {
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
                    NativeHelper.MemCpy(dest + lastIndex, src + current, replacement.BlockPosition.StartIndex - current);
                    lastIndex += replacement.BlockPosition.StartIndex - current;

                    NativeHelper.MemCpy(dest + lastIndex, middle, chunkLength);
                    current = replacement.BlockPosition.StartIndex + replacement.BlockPosition.Length;
                    lastIndex += chunkLength;
                }
            }
#if DEBUG
            if (lastIndex + srcLen - current < 0 || lastIndex + srcLen - current > capacity || current > srcLen)
                throw new ArgumentException();
#endif
            NativeHelper.MemCpy(dest + lastIndex, src + current, srcLen - current);
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
#if DNXCORE50 || DNX451
                string destination = new string('\0', newLen);
#else
                string destination = NativeHelper.AllocateString(newLen);
#endif
                unsafe {
                    fixed (char* dest = destination) {
                        fixed (char* src = source) {
                            fixed (char* repl = replacement) {
                                if (start > 0)
                                    NativeHelper.MemCpy(dest, src, start);
                                NativeHelper.MemCpy(dest + start, repl, replacementLength);
                                NativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
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
                                    NativeHelper.MemCpy(dest, src, start);
                                NativeHelper.MemCpy(dest + start, repl, replacementLength);
                                NativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
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
                                    NativeHelper.MemCpy(dest, src, start);
                                NativeHelper.MemCpy(dest + start, repl, replacementLength);
                                NativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
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
                                    NativeHelper.MemCpy(dest, src, start);
                                NativeHelper.MemCpy(dest + start, repl, replacementLength);
                                NativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
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
                                    NativeHelper.MemCpy(dest, src, start);
                                NativeHelper.MemCpy(dest + start, repl, replacementLength);
                                NativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
                            }
                        }
                    }
                }
                return new ExString(destination);
            }
        }
    }
}