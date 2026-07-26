using System;

namespace Heddle.Data
{
    /// <summary>
    /// Renderer capability: accepts character spans without requiring a string. Additive to the shipped
    /// <see cref="IScopeRenderer"/> — the string path is unaffected. Implemented by the sink adapters
    /// (<see cref="TextWriterScopeRenderer"/>, <see cref="Utf8ScopeRenderer"/>) and by <see cref="HtmlEncodedRenderer"/>
    /// (via a string bridge).
    /// </summary>
    public interface ISpanScopeRenderer : IScopeRenderer
    {
        /// <summary>Renders a character span. Equivalent to <c>Render(new string(data))</c> with fewer allocations.</summary>
        void Render(ReadOnlySpan<char> data);
    }
}
