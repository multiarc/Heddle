using System.Collections.Generic;
using System.Net;
using Templates.Strings;

namespace Templates.Data
{
    public class HtmlEncodedRenderer : IScopeRenderer
    {
        private readonly IScopeRenderer _renderer;

        public HtmlEncodedRenderer(IScopeRenderer renderer)
        {
            _renderer = renderer;
        }

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                _renderer.Render(WebUtility.HtmlEncode(data));
            }
        }
    }

    public class ScopeRenderer : IScopeRenderer
    {
        private readonly List<string> _items;
        private int _totalLength;

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
                _totalLength += data.Length;
            }
        }

        public override string ToString()
        {
            return ExStringBuilder.ConcatList(_items, _totalLength);
        }
    }
}