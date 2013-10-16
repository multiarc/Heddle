using System;
using Templates.Core.CompilerServices;

namespace Templates.Core {
    /// <summary>
    /// Interface that should be implemented in every Template
    /// </summary>
    public interface IExtension: IDisposable {
        /// <summary>
        /// Processes template file with existing input data and additional data
        /// </summary>
        /// <param name="value">Input data (serialized)</param>
        /// /// <param name="additionalValue">Additional input data (serialized)</param>
        /// <returns>Generated string to be inserted in template instead of template</returns>
        object ProcessData (object value, object additionalValue);

        void ParseParameter (string parameter, Type dataType, Type additionalType, bool directRender);

        Type InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context);
    }
}