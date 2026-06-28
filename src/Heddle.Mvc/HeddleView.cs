using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;

namespace Heddle.Mvc {
    public class HeddleView : IView, IDisposable {
        private readonly HeddleTemplate _template;

        public HeddleView(HeddleTemplate template)
        {
            _template = template;
        }

        public HeddleTemplate Template => _template;

        public static explicit operator HeddleView(HeddleTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            return new HeddleView(template);
        }

        public static explicit operator HeddleTemplate(HeddleView view)
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