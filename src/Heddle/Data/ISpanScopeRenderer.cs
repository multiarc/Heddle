using System;

namespace Heddle.Data
{
    /// <summary>
    /// Renderer capability (phase 8 D2): accepts character spans without requiring a string. Additive to the shipped
    /// <see cref="IScopeRenderer"/> — the string path is unaffected. Implemented by the sink adapters
    /// (<see cref="TextWriterScopeRenderer"/>, <see cref="Utf8ScopeRenderer"/>) and by <see cref="HtmlEncodedRenderer"/>
    /// (via a 1.x string bridge, D9).
    /// </summary>
    public interface ISpanScopeRenderer : IScopeRenderer
    {
        /// <summary>Renders a character span. Equivalent to <c>Render(new string(data))</c> with fewer allocations.</summary>
        void Render(ReadOnlySpan<char> data);
    }
}
