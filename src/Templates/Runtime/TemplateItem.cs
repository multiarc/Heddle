using System.Globalization;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Strings.Core;

namespace Templates.Runtime {
    internal class TemplateItem: IDataProcessor {
        public TemplateItem(ExType returnType, IExtension extension)
        {
            ReturnType = returnType;
            Extension = extension;
        }

        public ExType ReturnType { get; }

        public RuntimeCallParameter Parameter { get; set; }

        public IExtension Extension { get; }

        public object ProcessData(object value, object chainedResult)
        {
            var result = Extension.ProcessData(Parameter.GetParameter(value, chainedResult), chainedResult);
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

        public BlockPosition Position
        {
            get; set;
        }

        public void Dispose()
        {
            Parameter.Dispose();
        }
    }
}