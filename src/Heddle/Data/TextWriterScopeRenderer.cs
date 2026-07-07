using System;
using System.Buffers;
using System.IO;

namespace Heddle.Data
{
    /// <summary>
    /// <see cref="IScopeRenderer"/> over a host-supplied <see cref="TextWriter"/> (phase 8 D2/D4). Write-through: no
    /// internal buffering, and the writer is never flushed or disposed — the host owns its lifecycle (the
    /// <c>Response.BodyWriter</c> contract). Single render ownership: not thread-safe; a new instance is constructed
    /// per render.
    /// </summary>
    public sealed class TextWriterScopeRenderer : ISpanScopeRenderer
    {
        private readonly TextWriter _writer;

        public TextWriterScopeRenderer(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
                _writer.Write(data);
        }

        public void Render(ReadOnlySpan<char> data)
        {
            if (data.IsEmpty)
                return;
#if NET6_0_OR_GREATER
            // TextWriter.Write(ReadOnlySpan<char>) is netcoreapp2.1+; StreamWriter/HttpResponseStreamWriter override
            // it with true span paths.
            _writer.Write(data);
#else
            // The span overload does not exist on netstandard2.0/net48 — rent, copy, and use Write(char[], int, int)
            // (present everywhere). This is exactly what the BCL's own base TextWriter.Write(ReadOnlySpan<char>) does.
            var buffer = ArrayPool<char>.Shared.Rent(data.Length);
            try
            {
                data.CopyTo(buffer);
                _writer.Write(buffer, 0, data.Length);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
#endif
        }
    }
}
