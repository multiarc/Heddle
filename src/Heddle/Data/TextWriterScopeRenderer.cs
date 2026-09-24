using System;
using System.Buffers;
using System.IO;
using System.Text.Encodings.Web;

namespace Heddle.Data
{
    /// <summary>
    /// <see cref="IScopeRenderer"/> over a host-supplied <see cref="TextWriter"/>. Write-through: no
    /// internal buffering, and the writer is never flushed or disposed — the host owns its lifecycle (the
    /// <c>Response.BodyWriter</c> contract). Single render ownership: not thread-safe; a new instance is constructed
    /// per render.
    /// </summary>
    public sealed class TextWriterScopeRenderer : ISpanScopeRenderer, IEncoderCarrier
    {
        private readonly TextWriter _writer;
        private TextEncoder _outputEncoder;

        public TextWriterScopeRenderer(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        // null = legacy path
        internal void SetOutputEncoder(TextEncoder encoder) => _outputEncoder = encoder;
        TextEncoder IEncoderCarrier.Encoder => _outputEncoder;

        public void Render(string data)
        {
            if (!string.IsNullOrEmpty(data))
                _writer.Write(data);
        }

        public void Render(ReadOnlySpan<char> data)
        {
            if (data.IsEmpty)
                return;
#if NET8_0_OR_GREATER
            // TextWriter.Write(ReadOnlySpan<char>) is netcoreapp2.1+; StreamWriter/HttpResponseStreamWriter override
            // it with true span paths.
            _writer.Write(data);
#else
            // The span overload does not exist on netstandard2.0/net48 — rent, copy, and use Write(char[], int, int).
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
