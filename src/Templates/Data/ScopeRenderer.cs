using Templates.Strings;

namespace Templates.Data
{
    public class ScopeRenderer : IScopeRenderer
    {
        private readonly LinearList<string> _items;
        private int _length;

        public ScopeRenderer(int elementCount = 0)
        {
            _items = new LinearList<string>(elementCount);
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

        public override string ToString()
        {
            return ExStringBuilder.Concat(_items.Array, _items.Count, _length);
        }

        public void Clear()
        {
            _items.Clear();
            _length = 0;
        }
    }
}