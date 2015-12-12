using System;
using Templates.Data;
using Templates.Strings.Core;

namespace Templates.Runtime
{
    public interface IDataProcessor: IDisposable {
        /// <summary>
        /// Processes template file with existing input data and additional data
        /// </summary>
        /// <param name="scope"></param>
        /// <returns>Generated string to be inserted in template instead of template</returns>
        object ProcessData(Scope scope);

        BlockPosition Position { get; set; }
    }
}