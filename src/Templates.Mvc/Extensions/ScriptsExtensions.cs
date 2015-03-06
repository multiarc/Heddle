using System;
using System.Web.Optimization;
using Templates.Attributes;
using Templates.Core;
using Templates.Language;
using Templates.Mvc.Extensions;
using Templates.Runtime;

[assembly: ExportExtensions(typeof(ScriptsExtensions))]

namespace Templates.Mvc.Extensions {
    [Name("html.scripts")]
    public class ScriptsExtensions: AbstractExtension
    {
        private string _bundle;
        private bool _renderAlways;

        public override object ProcessData(object value, object chainedResult)
        {
            if (!_renderAlways)
                return _bundle;
            return Scripts.RenderFormat(value as string, _bundle);
        }

        public override Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context,
            ParseContext parseContext)
        {
            if (dataType != null && dataType == typeof(string))
            {
                _renderAlways = true;
                _bundle = parameterTemplate;
            }
            else
            {
                _bundle = Scripts.Render(parameterTemplate).ToString();
            }
            return typeof (string);
        }
    }
}