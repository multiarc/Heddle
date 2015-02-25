using System;
using NativeFunctions;
using Templates.Collections;
using Templates.Strings.Core;

namespace Templates.Strings {
    [Serializable]
    public sealed class ExStringBuilder {
        private readonly SmartList<ExString> _appendFastStrings = new SmartList<ExString>();
        private readonly SmartList<string> _appendStrings = new SmartList<string>();
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
                        _data = StringNativeHelper.AllocateString(newLen);
                        fixed (char* dest = _data) {
                            fixed (char* src = old) {
                                StringNativeHelper.MemCpy(dest, src, oldLen);
                            }
                        }
                    }
                }
            }
        }

        public int Length
        {
            get { return _data.Length + _appendlength; }
        }

        public void Clear ()
        {
            _appendlength = 0;
            _appendStrings.Clear();
            _appendFastStrings.Clear();
            _data = ExString.Empty;
        }

        private void CommitAppend ()
        {
            if (_appendlength != 0) {
                int seed = _data.Length;
                int newLength = _data.Length + _appendlength;
                for (int i = 0; i < _appendStrings.Length; i++) {
                    if (_data.Length < newLength)
                        Capacity = newLength;
                    int len = _appendStrings[i].Length;
                    unsafe {
                        fixed (char* dest = _data) {
                            fixed (char* src = _appendStrings[i]) {
                                StringNativeHelper.MemCpy(dest + seed, src, len);
                            }
                        }
                    }
                    seed += len;
                    _appendStrings[i] = null;
                }
                _appendStrings.Clear();
                for (int i = 0; i < _appendFastStrings.Length; i++) {
                    if (_data.Length < newLength)
                        Capacity = newLength;
                    int len = _appendFastStrings[i].Length;
                    unsafe {
                        fixed (char* dest = _data) {
                            fixed (char* src = (char[]) _appendFastStrings[i]) {
                                StringNativeHelper.MemCpy(dest + seed, src, len);
                            }
                        }
                    }
                    seed += len;
                    _appendFastStrings[i] = null;
                }
                _appendFastStrings.Clear();
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

        public void Append(string format, params object[] args) {
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
                _appendFastStrings.Add(value);
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
                throw new ArgumentNullException("replacements");

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
                throw new ArgumentNullException("replacements");
            if (source == null)
                throw new ArgumentNullException("source");

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

                string result = StringNativeHelper.AllocateString(capacity);

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
                throw new ArgumentNullException("replacements");
            if (source == null)
                throw new ArgumentNullException("source");

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

                string result = StringNativeHelper.AllocateString(capacity);

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
                    StringNativeHelper.MemCpy(dest + lastIndex, src + current, replacement.BlockPosition.StartIndex - current);
                    lastIndex += replacement.BlockPosition.StartIndex - current;

                    StringNativeHelper.MemCpy(dest + lastIndex, middle, chunkLength);
                    current = replacement.BlockPosition.StartIndex + replacement.BlockPosition.Length;
                    lastIndex += chunkLength;
                }
            }
#if DEBUG
            if (lastIndex + srcLen - current < 0 || lastIndex + srcLen - current > capacity || current > srcLen)
                throw new ArgumentException();
#endif
            StringNativeHelper.MemCpy(dest + lastIndex, src + current, srcLen - current);
        }

        public static string Replace (int start, int length, string replacement, string source)
        {
            if (replacement == null)
                throw new ArgumentNullException("replacement");
            if (source == null)
                throw new ArgumentNullException("source");

            int sourceLen = source.Length;
            int replacementLength = replacement.Length;
            unchecked {
#if DEBUG
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
#endif
                int newLen = sourceLen - length + replacement.Length;
                string destination = StringNativeHelper.AllocateString(newLen);
                unsafe {
                    fixed (char* dest = destination) {
                        fixed (char* src = source) {
                            fixed (char* repl = replacement) {
                                if (start > 0)
                                    StringNativeHelper.MemCpy(dest, src, start);
                                StringNativeHelper.MemCpy(dest + start, repl, replacementLength);
                                StringNativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
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
                throw new ArgumentNullException("replacement");
            if (source == null)
                throw new ArgumentNullException("source");

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
                                    StringNativeHelper.MemCpy(dest, src, start);
                                StringNativeHelper.MemCpy(dest + start, repl, replacementLength);
                                StringNativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
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
                throw new ArgumentNullException("replacement");
            if (source == null)
                throw new ArgumentNullException("source");

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
                                    StringNativeHelper.MemCpy(dest, src, start);
                                StringNativeHelper.MemCpy(dest + start, repl, replacementLength);
                                StringNativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
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
                throw new ArgumentNullException("replacement");

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
                                    StringNativeHelper.MemCpy(dest, src, start);
                                StringNativeHelper.MemCpy(dest + start, repl, replacementLength);
                                StringNativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
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
            if (element.StartIndex > 0 && source[element.StartIndex - 1] == '\n') {
                removeStart--;
                removeLength++;
                if (element.StartIndex > 1 && source[element.StartIndex - 2] == '\r') {
                    removeStart--;
                    removeLength++;
                }
            }
            else if (element.StartIndex + element.Length < source.Length
                       && source[element.StartIndex + element.Length] == '\r') {
                removeLength++;
                if (element.StartIndex + element.Length + 1 < source.Length
                    && source[element.StartIndex + element.Length + 1] == '\n')
                    removeLength++;
            }

            source = Replace(removeStart, removeLength, string.Empty, source);
            return removeLength;
        }

        public ExString Replace (int start, int length, ExString replacement)
        {
            if (_appendStrings.Length > 0)
                CommitAppend();

            if (replacement == null)
                throw new ArgumentNullException("replacement");

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
                                    StringNativeHelper.MemCpy(dest, src, start);
                                StringNativeHelper.MemCpy(dest + start, repl, replacementLength);
                                StringNativeHelper.MemCpy(dest + start + replacementLength, src + start + length, sourceLen - start - length);
                            }
                        }
                    }
                }
                return new ExString(destination);
            }
        }
    }
}