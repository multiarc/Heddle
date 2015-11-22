using System;
using Templates.Data;
using Templates.Language;
using Templates.Runtime;
using Templates.Strings.Core;

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

        private static ExType InitSubTemplate(ref string parameterTemplate, ExType dataType, ExType chainedType,
            CompileContext context,
            ParseContext parseContext, out IDataProcessor result)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            RuntimeDocument subTemplate;
            if (string.IsNullOrEmpty(parameterTemplate))
                subTemplate = null;
            else
            {
                var newContext = new CompileContext(context, dataType);
                subTemplate = TtlCompiler.Compile(parameterTemplate, newContext, parseContext, chainedType);
                newContext.Compile();
            }
            if (subTemplate != null)
            {
                parameterTemplate = subTemplate.Document;
            }
            if (subTemplate == null || subTemplate.Empty)
            {
                result = null;
            }
            else
            {
                result = subTemplate.CanOptimizeSelf ? subTemplate.SingleProcessor : subTemplate;
            }

            return typeof (string);
        }

        public virtual ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.Context == null)
                throw new ArgumentNullException(nameof(initContext.Context));
            IDataProcessor subTemplate;
            var type = InitSubTemplate(ref initContext.ParameterTemplate, dataType, chainedType, initContext.Context, initContext.ParseContext, out subTemplate);
            if (subTemplate == null)
                _innerResult = initContext.ParameterTemplate;
            SubTemplate = subTemplate;
            return type;
        }

        public virtual void CompleteInit(CompileContext newContext, ParseContext parseContext)
        {
            
        }

        public abstract object ProcessData(object data, object chained, object parent);
        public BlockPosition Position { get; set; }
    }
}
