using System;
using System.IO;
using System.Web.Mvc;

namespace Templates.Mvc {
    public class TtlView : IView, IDisposable {
        private readonly TtlTemplate _template;

        public TtlView(TtlTemplate template)
        {
            _template = template;
        }

        public TtlTemplate Template => _template;

        public void Render(ViewContext viewContext, TextWriter writer)
        {
            writer.Write(_template.Generate(viewContext.ViewData.Model));
        }

        public static explicit operator TtlView(TtlTemplate template)
        {
            return new TtlView(template);
        }

        public static explicit operator TtlTemplate(TtlView view)
        {
            return view.Template;
        }

        public void Dispose()
        {
            _template?.Dispose();
        }
    }
}