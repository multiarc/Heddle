using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Templates.Data
{
    internal sealed class LinearList<T> : IList<T>, IList
    {
        private T[] _array;
        private int _length;

        public LinearList()
        {
            _array = new T[4];
        }

        public LinearList(T[] value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            _array = new T[value.Length];
            value.CopyTo(_array, 0);
            _length = _array.Length;
        }

        public LinearList(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentException();
            _array = new T[capacity < 4 ? 4 : capacity];
        }

        public int Length => _length;

        public int Capacity => _array.Length;

        internal T[] Array => _array;

        public T[] ToArray()
        {
            var result = new T[_length];
            _array.CopyTo(result, _length);
            return result;
        }

        public static implicit operator T[](LinearList<T> value)
        {
            return value?.ToArray();
        }

        public static implicit operator LinearList<T>(T[] value)
        {
            if (value == null)
                return null;

            return new LinearList<T>(value);
        }

        public LinearList<T> AddRange(ICollection<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (_length == int.MaxValue || (long) _length + items.Count > int.MaxValue)
                throw new OutOfMemoryException();
            var length = _length;
            _length += items.Count;
            if (_array.Length < _length)
            {
                if ((long) _length * 15 / 10 > int.MaxValue)
                {
                    System.Array.Resize(ref _array, int.MaxValue);
                }
                else
                {
                    System.Array.Resize(ref _array, (int) ((long) _length * 15 / 10));
                }
            }

            items.CopyTo(_array, length);
            return this;
        }

        public LinearList<T> AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            foreach (var item in items)
            {
                Add(item);
            }

            return this;
        }

        public LinearList<T> GetCrossThreadCopy()
        {
            var result = new LinearList<T>
            {
                _array = new T[_length],
                _length = _length
            };
            _array.CopyTo(result._array, _length);
            return result;
        }

        public void Remove(object value)
        {
            throw new NotSupportedException();
        }

        void IList.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        object IList.this[int index]
        {
            get
            {
                if (index >= _length || index < 0)
                    throw new ArgumentException();

                return _array[index];
            }
            set
            {
                if (index >= _length || index < 0 || !(value is T))
                    throw new ArgumentException();
                _array[index] = (T) value;
            }
        }

        public int Add(object value)
        {
            if (value is T)
            {
                Add((T) value);
                return _length - 1;
            }

            if (ReferenceEquals(null, value))
            {
                Add(default(T));
                return _length - 1;
            }

            return -1;
        }

        public bool Contains(object value)
        {
            if (value is T)
                return Contains((T) value);
            return ReferenceEquals(null, value) && Contains(default(T));
        }

        public int IndexOf(object value)
        {
            if (value is T)
                return IndexOf((T) value);
            if (ReferenceEquals(null, value))
                return IndexOf(default(T));
            return -1;
        }

        public void Insert(int index, object value)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (index < 0 || _length + index < 0 || _length + index > array.Length)
                throw new ArgumentException();

            for (int i = 0,
                j = index;
                i < _length;
                i++, j++)
                array.SetValue(_array[i], j);
        }

        public object SyncRoot => this;

        public bool IsSynchronized => false;

        public bool IsFixedSize => false;

        public int IndexOf(T item)
        {
            if (item != null)
            {
                for (int i = 0; i < _length; i++)
                {
                    if (ReferenceEquals(item, _array[i]))
                        return i;
                    if (item.Equals(_array[i]))
                        return i;
                }
            }
            else
            {
                for (int i = 0; i < _length; i++)
                {
                    if (null == _array[i])
                        return i;
                }
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        [IndexerName("Items")]
        public T this[int index]
        {
            get
            {
                if (index >= _length || index < 0)
                    throw new ArgumentException();

                return _array[index];
            }
            set
            {
                if (index >= _length || index < 0)
                    throw new ArgumentException();

                _array[index] = value;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _length; i++)
                yield return _array[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < _length; i++)
                yield return _array[i];
        }

        public void Add(T value)
        {
            if (((long) _length + 1) > int.MaxValue)
                throw new OutOfMemoryException();
            _length++;
            if (_array.Length < _length)
            {
                if ((long) _length * 2 > int.MaxValue)
                {
                    System.Array.Resize(ref _array, int.MaxValue);
                }
                else
                {
                    System.Array.Resize(ref _array, _length * 2);
                }
            }

            _array[_length - 1] = value;
        }

        public void Clear()
        {
            _length = 0;
        }

        public bool Contains(T item)
        {
            return IndexOf(item) != -1;
        }

        public void CopyTo(T[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (index < 0 || _length + index < 0 || _length + index > array.Length)
                throw new ArgumentException();
            for (int i = 0,
                j = index;
                i < _length;
                i++, j++)
                array[j] = _array[i];
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public int Count => _length;

        public bool IsReadOnly => false;
    }
}