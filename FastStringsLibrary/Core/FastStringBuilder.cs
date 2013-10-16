using System;
using NativeFunctions;

namespace FastStrings.Core {
    [Serializable]
    public sealed class FastStringBuilder {
        private readonly SmartList<FastString> _appendFastStrings = new SmartList<FastString>();
        private readonly SmartList<string> _appendStrings = new SmartList<string>();
        private int _appendlength;
        private string _data;

        public FastStringBuilder (FastString value)
        {
            _data = value ?? FastString.Empty;
        }

        public FastStringBuilder (string value)
        {
            _data = value ?? string.Empty;
        }

        public FastStringBuilder ()
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
            _data = FastString.Empty;
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

        public void Append (FastString value)
        {
            if (!FastString.IsNullOrEmpty(value)) {
                _appendFastStrings.Add(value);
                _appendlength += value.Length;
            }
        }

        public override string ToString ()
        {
            CommitAppend();
            return _data;
        }

        public FastString ToFastString ()
        {
            CommitAppend();
            return _data;
        }

        public FastString BulkReplace (Replacement[] replacements)
        {
            if (_appendStrings.Length > 0)
                CommitAppend();
            if (replacements == null)
                throw new ArgumentNullException("replacements");

            int count = replacements.Length;

            if (count == 0)
                return _data;

            if (_data.Length == 0)
                return FastString.Empty;

            int capacity = _data.Length;
            int srcLen = capacity;
            unchecked {
                for (int i = 0; i < count; i++) {
                    Replacement replacement = replacements[i];
                    if (replacement.ReplacementValue == null)
                        replacement.ReplacementValue = string.Empty;
                    if (replacement.Position.Length < 0)
                        throw new ArgumentException();
                    if (replacement.Position.StartIndex < 0)
                        throw new ArgumentException();
                    capacity += replacement.ReplacementValue.Length - replacement.Position.Length;
                    if (capacity < 0)
                        throw new ArgumentException();
                }

                if (capacity == 0)
                    return FastString.Empty;

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
                    if (replacement.Position.Length < 0)
                        throw new ArgumentException();
                    if (replacement.Position.StartIndex < 0)
                        throw new ArgumentException();
                    capacity += (replacement.ReplacementValue != null ? replacement.ReplacementValue.Length : 0) - replacement.Position.Length;
                    if (capacity < 0)
                        throw new ArgumentException();
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

        public static string BulkReplace (Replacement[] replacements, FastString source)
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
                    if (replacement.Position.Length < 0)
                        throw new ArgumentException();
                    if (replacement.Position.StartIndex < 0)
                        throw new ArgumentException();
                    capacity += (replacement.ReplacementValue != null ? replacement.ReplacementValue.Length : 0) - replacement.Position.Length;
                    if (capacity < 0)
                        throw new ArgumentException();
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
                if (lastIndex + replacement.Position.StartIndex - current + chunkLength < 0
                    || lastIndex + replacement.Position.StartIndex - current + chunkLength > capacity || replacement.Position.StartIndex > srcLen
                    || current > srcLen)
                    throw new ArgumentException();
                fixed (char* middle = replacementString) {
                    StringNativeHelper.MemCpy(dest + lastIndex, src + current, replacement.Position.StartIndex - current);
                    lastIndex += replacement.Position.StartIndex - current;

                    StringNativeHelper.MemCpy(dest + lastIndex, middle, chunkLength);
                    current = replacement.Position.StartIndex + replacement.Position.Length;
                    lastIndex += chunkLength;
                }
            }
            if (lastIndex + srcLen - current < 0 || lastIndex + srcLen - current > capacity || current > srcLen)
                throw new ArgumentException();
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
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
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

        public static FastString Replace (int start, int length, string replacement, FastString source)
        {
            if (replacement == null)
                throw new ArgumentNullException("replacement");
            if (source == null)
                throw new ArgumentNullException("source");

            int sourceLen = source.Length;
            int replacementLength = replacement.Length;
            unchecked {
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
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
                return new FastString(destination);
            }
        }

        public static FastString Replace (int start, int length, FastString replacement, FastString source)
        {
            if (replacement == null)
                throw new ArgumentNullException("replacement");
            if (source == null)
                throw new ArgumentNullException("source");

            int sourceLen = source.Length;
            int replacementLength = replacement.Length;
            unchecked {
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
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
                return new FastString(destination);
            }
        }

        public FastString Replace (int start, int length, string replacement)
        {
            if (_appendStrings.Length > 0)
                CommitAppend();

            if (replacement == null)
                throw new ArgumentNullException("replacement");

            int sourceLen = _data.Length;
            int replacementLength = replacement.Length;
            unchecked {
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
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
                return new FastString(destination);
            }
        }

        public FastString Replace (int start, int length, FastString replacement)
        {
            if (_appendStrings.Length > 0)
                CommitAppend();

            if (replacement == null)
                throw new ArgumentNullException("replacement");

            int sourceLen = _data.Length;
            int replacementLength = replacement.Length;
            unchecked {
                if (start < 0 || start + length < 0 || start + length > sourceLen || sourceLen - length + replacement.Length < 0)
                    throw new ArgumentException();
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
                return new FastString(destination);
            }
        }
    }
}