using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Templates.Data;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Core {
    public abstract class AbstractExtension: IExtension {
        protected bool DirectRender;
        private string _innerResult = string.Empty;
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                SubTemplate?.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        public IDataProcessor SubTemplate { get; protected set; }
        protected string GetInnerResult(object data, object chainedResult) {
            return (string)SubTemplate?.ProcessData(data, chainedResult) ?? _innerResult;
        }

        public virtual void SetUpRenderType(RenderType renderType)
        {
            DirectRender = renderType == RenderType.Encode;
        }

        public static Type InitSubTemplate(string parameterTemplate, Type dataType, Type chainedType, CompileContext context,
            ParseContext parseContext, out IDataProcessor result) {
            if (context == null)
                throw new ArgumentNullException("context");

            var subTemplate = string.IsNullOrEmpty(parameterTemplate)
                ? null
                : TtlCompiler.Compile(parameterTemplate, new CompileContext(context, dataType), parseContext);
            if (subTemplate == null || subTemplate.Empty) {
                subTemplate?.Dispose();
                result = null;
            }
            else {
                if (subTemplate.CanFullOptimize) {
                    result = subTemplate.SingleProcessor;
                }
                else {
                    result = subTemplate;
                }
            }

            return typeof(string);
        }

        public virtual Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context,
            ParseContext parseContext)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            IDataProcessor subTemplate;
            var type = InitSubTemplate(parameterTemplate, dataType, chainedType, context, parseContext, out subTemplate);
            if (subTemplate == null)
                _innerResult = parameterTemplate;
            SubTemplate = subTemplate;
            return type;
        }

        public virtual void CompleteInit(CompileContext newContext, ParseContext parseContext)
        {
            
        }

        public abstract object ProcessData(object value, object chainedResult);
    }
}
