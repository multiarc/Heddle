using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FastStrings.Core {
    public partial class SmartList<T>: IList<T>, IList, ICloneable {
        #region ICloneable Members

        public object Clone ()
        {
            return MemberwiseClone();
        }

        #endregion

        #region IList Members

        public void Remove (object value)
        {
            if (ReferenceEquals(null, value))
                Remove(default(T));
            if (value is T)
                Remove((T) value);
        }

        object IList.this [int index]
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

        public int Add (object value)
        {
            if (value is T) {
                Add((T) value);
                return _length - 1;
            }
            if (ReferenceEquals(null, value)) {
                Add(default(T));
                return _length - 1;
            }
            return -1;
        }

        public bool Contains (object value)
        {
            if (value is T)
                return Contains((T) value);
            return ReferenceEquals(null, value) && Contains(default(T));
        }

        public int IndexOf (object value)
        {
            if (value is T)
                return IndexOf((T) value);
            if (ReferenceEquals(null, value))
                return IndexOf(default(T));
            return -1;
        }

        public void Insert (int index, object value)
        {
            if (value is T)
                Insert(index, (T) value);
            if (ReferenceEquals(null, value))
                Insert(index, default(T));
        }

        public void CopyTo (Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (index < 0 || _length + index < 0 || _length + index > array.Length)
                throw new ArgumentException();

            for (int i = 0,
                     j = index;
                 i < _length;
                 i++, j++)
                array.SetValue(_array[i], j);
        }

        public object SyncRoot
        {
            get { return this; }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }

        public bool IsFixedSize
        {
            get { return false; }
        }

        #endregion

        #region IList<T> Members

        public int IndexOf (T item)
        {
            if (!ReferenceEquals(item, null)) {
                for (int i = 0; i < _length; i++) {
                    if (ReferenceEquals(item, _array[i]))
                        return i;
                    if (item.Equals(_array[i]))
                        return i;
                }
            } else {
                for (int i = 0; i < _length; i++) {
                    if (ReferenceEquals(null, _array[i]))
                        return i;
                }
            }
            return -1;
        }

        public void Insert (int index, T item)
        {
            if (index >= _length || index < 0)
                throw new ArgumentException();

            _length++;
            if (_array.Length < _length)
                Array.Resize(ref _array, _length << 1);
            for (int i = _length; i > index; i--)
                _array[i] = _array[i - 1];
            _array[index] = item;
        }

        public void RemoveAt (int index)
        {
            if (index >= _length || index < 0)
                throw new ArgumentException();

            for (int i = index; i < _length - 1; i++)
                _array[i] = _array[i + 1];
            _length--;
        }

        [IndexerName ("Items")]
        public T this [int index]
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

        public IEnumerator<T> GetEnumerator ()
        {
            for (int i = 0; i < _length; i++)
                yield return _array[i];
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            for (int i = 0; i < _length; i++)
                yield return _array[i];
        }

        public void Add (T value)
        {
            _length++;
            if (_array.Length < _length)
                Array.Resize(ref _array, _length << 1);
            _array[_length - 1] = value;
        }

        public void Clear ()
        {
            _array = Empty;
            _length = 0;
        }

        public bool Contains (T item)
        {
            return IndexOf(item) != -1;
        }

        public void CopyTo (T[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (index < 0 || _length + index < 0 || _length + index > array.Length)
                throw new ArgumentException();

            for (int i = 0,
                     j = index;
                 i < _length;
                 i++, j++)
                array[j] = _array[i];
        }

        public bool Remove (T item)
        {
            int index = IndexOf(item);
            if (index == -1)
                return false;
            RemoveAt(index);
            return true;
        }

        public int Count
        {
            get { return _length; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        #endregion
    }
}