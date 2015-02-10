using System;

namespace Templates.Collections {
    [Serializable]
    public sealed partial class SmartList<T> {
        private static readonly T[] Empty = new T[0];
        private T[] _array;
        private int _length;

        public SmartList ()
        {
            _array = Empty;
        }

        public SmartList (T[] value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            _array = value;
            _length = _array.Length;
        }

        public SmartList (int capacity)
        {
            if (capacity < 0)
                throw new ArgumentException();
            _array = new T[capacity];
        }

        public int Length
        {
            get { return _length; }
        }

        public int Capacity
        {
            get { return _array.Length; }
        }

        public T[] ToArray ()
        {
            if (_array.Length != _length)
                Array.Resize(ref _array, _length);
            return _array;
        }

        public static implicit operator T[] (SmartList<T> value)
        {
            if (value == null)
                return null;
            return value.ToArray();
        }

        public static implicit operator SmartList<T> (T[] value)
        {
            if (value == null)
                return null;

            return new SmartList<T>(value);
        }

        public SmartList<T> GetCrossThreadCopy ()
        {
            var result = new SmartList<T>
            {
                _array = new T[_length],
                _length = _length
            };
            Array.Copy(_array, result._array, _length);
            return result;
        }
    }
}