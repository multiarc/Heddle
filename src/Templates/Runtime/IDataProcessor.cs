using System;
using Templates.Strings.Core;

namespace Templates.Runtime
{
    public interface IDataProcessor: IDisposable {
        /// <summary>
        /// Processes template file with existing input data and additional data
        /// </summary>
        /// <param name="value">Input data (serialized)</param>
        /// <param name="chainedResult">Additional input data (serialized)</param>
        /// <returns>Generated string to be inserted in template instead of template</returns>
        object ProcessData(object value, object chainedResult);

        BlockPosition Position { get; set; }
    }
}