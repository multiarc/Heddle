using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Templates.Data;
using Templates.Runtime;

namespace Templates.Mvc {
    public class TemplateResolver {

        private Dictionary<string, TtlTemplate> TemplatesCache { get; set; }

        public TemplateResolver() {
            TemplatesCache = new Dictionary<string, TtlTemplate>(StringComparer.OrdinalIgnoreCase);
        }

        public TtlTemplate GetView(string viewPath) {
            TtlTemplate result;
            if (TemplatesCache.TryGetValue(viewPath, out result)) {
                return result;
            }
            TemplateOptions options = new TemplateOptions
            {
                EnableFileChangeCheck = false,
                FileNamePostfix = Path.GetExtension(viewPath),
                RootPath = Path.GetDirectoryName(HttpRuntime.AppDomainAppPath) + Path.GetDirectoryName(viewPath),
                TemplateName = Path.GetFileNameWithoutExtension(viewPath)
            };

            result = new TtlTemplate(new CompileContext(options));
            TemplatesCache.Add(viewPath, result);
            return result;
        }
    }
}
