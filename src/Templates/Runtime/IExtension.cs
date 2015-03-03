using System;
using Templates.Data;
using Templates.Language;

namespace Templates.Runtime {
    /// <summary>
    /// Interface that should be implemented in every Template
    /// </summary>
    public interface IExtension: IDisposable {
        void SetUpRenderType(RenderType renderType);

        Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext);

        void CompleteInit(CompileContext newContext, ParseContext parseContext);

        object ProcessData(object value, object chainedResult);
    }
}