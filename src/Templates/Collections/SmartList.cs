using System;
using System.Collections.Generic;

namespace Templates.Collections {
#if !ASPNETCORE50
    [Serializable]
#endif
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

        public int Length => _length;

        public int Capacity => _array.Length;

        public T[] ToArray ()
        {
            if (_array.Length != _length)
                Array.Resize(ref _array, _length);
            return _array;
        }

        public static implicit operator T[] (SmartList<T> value)
        {
            return value?.ToArray();
        }

        public static implicit operator SmartList<T> (T[] value)
        {
            if (value == null)
                return null;

            return new SmartList<T>(value);
        }

        public SmartList<T> AddRange(ICollection<T> items)
        {
            if (items == null) throw new ArgumentNullException("items");
            if (_length == int.MaxValue || (long)_length + items.Count > int.MaxValue)
                throw new OutOfMemoryException();
            var length = _length;
            _length += items.Count;
            if (_array.Length < _length)
            {
                if ((long) _length*2 > int.MaxValue)
                {
                    Array.Resize(ref _array, int.MaxValue);
                }
                else
                {
                    Array.Resize(ref _array, _length*2);
                }
            }
            items.CopyTo(_array, length);
            return this;
        }

        public SmartList<T> AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException("items");
            foreach (var item in items)
            {
                Add(item);
            }
            return this;
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