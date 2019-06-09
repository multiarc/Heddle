using System.Globalization;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Runtime.Parameters;
using Templates.Strings.Core;

namespace Templates.Runtime {
    internal class TemplateItem: IDataProcessor {

        public ExType ReturnType { get; set; }

        public IRuntimeParameter Parameter { get; set; }

        public IExtension Extension { get; set; }

        public object ProcessData(in Scope scope)
        {
            var model = Parameter.GetParameter(scope);
            var modelScope = scope.Model(model);
            var result = Extension.ProcessData(modelScope);
#if DEBUG
            if (result != null && ReturnType != null && !ReturnType.Type.IsType(result))
            {
                throw new TemplateProcessingException
                    (string.Format
                        (CultureInfo.InvariantCulture, "Returned data type not valid. Needed [{0}] Got [{1}]",
                            ReturnType.Type.FullName,
                            result.GetType().FullName));
            }
#endif
            return result;
        }

        public void RenderData(in Scope scope)
        {
            var model = Parameter.GetParameter(scope);
            var modelScope = scope.Model(model);
            Extension.RenderData(modelScope);
        }

        public BlockPosition Position
        {
            get; set;
        }

        public void Dispose()
        {
            Parameter?.Dispose();
            Extension?.Dispose();
        }
    }
}