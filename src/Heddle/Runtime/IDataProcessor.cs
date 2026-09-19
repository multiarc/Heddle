using System;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Runtime
{
    /// <summary>A renderable element: something that can produce its value for a scope or write its
    /// output directly.</summary>
    public interface IDataProcessor: IDisposable {
        /// <summary>Computes the element's value for <paramref name="scope"/> as an object: text for most
        /// elements, a typed value where a chain link or an argument consumes it. Not necessarily a
        /// string; the renderer never inserts it verbatim.</summary>
        /// <param name="scope">The current render scope.</param>
        /// <returns>The element's value.</returns>
        object ProcessData(in Scope scope);

        /// <summary>Writes the element's output to the scope's renderer, the direct path used when nothing
        /// consumes the value. Must agree with <see cref="ProcessData"/>.</summary>
        void RenderData(in Scope scope);

        /// <summary>The element's position in the template source.</summary>
        BlockPosition Position { get; set; }
    }
}