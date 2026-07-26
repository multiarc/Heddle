using System;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Runtime
{
    public interface IDataProcessor: IDisposable {
        /// <summary>
        /// Returns the generated value for this element.
        /// </summary>
        /// <param name="scope">The current render scope.</param>
        /// <returns>Generated string to be inserted for this element.</returns>
        object ProcessData(in Scope scope);

        void RenderData(in Scope scope);

        BlockPosition Position { get; set; }
    }
}