using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.AspNet.Mvc.ViewEngines;

namespace Templates.Mvc {
    public class TtlView : IView, IDisposable {
        private readonly TtlTemplate _template;

        public TtlView(TtlTemplate template)
        {
            _template = template;
        }

        public TtlTemplate Template => _template;

        public static explicit operator TtlView(TtlTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            return new TtlView(template);
        }

        public static explicit operator TtlTemplate(TtlView view)
        {
            return view?.Template;
        }

        public void Dispose()
        {
            _template?.Dispose();
        }

        public async Task RenderAsync(ViewContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            await context.Writer.WriteAsync(_template.Generate(context.ViewData.Model));
        }

        public string Path => _template.Context.Options.FullPath;
    }
}