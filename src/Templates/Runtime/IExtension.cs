using System;
using Templates.Data;
using Templates.Language;

namespace Templates.Runtime {
    /// <summary>
    /// Interface that should be implemented in every Template
    /// </summary>
    public interface IExtension: IDisposable
    {
        /// <summary>
        /// Processes template file with existing input data and additional data
        /// </summary>
        /// <param name="value">Input data (serialized)</param>
        /// <param name="chainedResult">Additional input data (serialized)</param>
        /// <returns>Generated string to be inserted in template instead of template</returns>
        object ProcessData (object value, object chainedResult);

        void SetUpRenderType(RenderType renderType);

        Type InitStart(string parameterTemplate, Type dataType, Type chainedType, CompileContext context, ParseContext parseContext);

        void CompleteInit(CompileContext newContext, ParseContext parseContext);

        RuntimeDocument SubTemplate { get; }
    }
}