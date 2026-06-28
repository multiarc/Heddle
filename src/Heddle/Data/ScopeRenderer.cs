using System.Collections.Generic;
using System.Text;
using Heddle.Strings;

namespace Heddle.Data;

#if NET8_0_OR_GREATER
    public class ScopeRenderer : IScopeRenderer
    {
        private readonly StringBuilder _stringBuilder;

        public ScopeRenderer(int capacity)
        {
            _stringBuilder = new StringBuilder(capacity);
        }

        public int TotalLength => _stringBuilder.Length;

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _stringBuilder.Append(data);
            }
        }

        public override string ToString() {
            return _stringBuilder.ToString();
        }

        public void Clear() {
            _stringBuilder.Clear();
        }
    }
#else
    public class ScopeRenderer : IScopeRenderer
    {
        private readonly List<string> _items;
        private int _length;

        public ScopeRenderer(int elementCount = 0)
        {
            _items = new List<string>(elementCount);
        }

        public int TotalCount => _items.Count;

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _items.Add(data);
                _length += data.Length;
            }
        }

        public override string ToString() {
            return ExStringBuilder.Concat(_items, _length);
        }

        public void Clear()
        {
            _items.Clear();
            _length = 0;
        }
    }
#endif