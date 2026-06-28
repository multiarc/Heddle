using System;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Runtime
{
    public interface IDataProcessor: IDisposable {
        /// <summary>
        /// Processes template file with existing input data and additional data
        /// </summary>
        /// <param name="scope"></param>
        /// <returns>Generated string to be inserted in template instead of template</returns>
        object ProcessData(in Scope scope);

        void RenderData(in Scope scope);

        BlockPosition Position { get; set; }
    }
}