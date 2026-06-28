using System;
using System.Collections.Generic;
using System.Net;

namespace Heddle.Data
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
}