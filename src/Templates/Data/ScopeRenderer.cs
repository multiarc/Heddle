using System.Collections.Generic;
using Templates.Strings;

namespace Templates.Data
{
    public class ScopeRenderer : IScopeRenderer
    {
        private readonly SmartList<string> _items;

        public ScopeRenderer(int elementCount = 0)
        {
            _items = new SmartList<string>(elementCount);
        }

        public int TotalCount => _items.Count;

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _items.Add(data);
            }
        }

        public override string ToString()
        {
            return string.Concat(_items.Array);
        }
    }
}