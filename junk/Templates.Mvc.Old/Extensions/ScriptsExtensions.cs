using System;
using System.Web.Optimization;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;
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

        public override ExType InitStart(string parameterTemplate, ExType dataType, ExType chainedType, CompileContext context,
            ParseContext parseContext)
        {
            base.InitStart(parameterTemplate, dataType, chainedType, context, parseContext);
            parameterTemplate = GetInnerResult(null, null);
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