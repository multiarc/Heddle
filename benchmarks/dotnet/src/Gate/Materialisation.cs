using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace Heddle.Benchmarks.Dotnet.Gate
{
    /// <summary>
    /// De-optimization guards for the sink render techniques.
    ///
    /// A sink that only COUNTS what it is handed does not prove the characters were produced: it
    /// proves a length was computed. That defect has shipped in this repository twice: a sink suite
    /// whose writer was <c>Write(char[] buffer, int index, int count) =&gt; Count += count</c>, and a
    /// whole published JS run whose "render" never walked the rope it built. The lesson generalises:
    /// a benchmark's sink has to touch every unit of output, or the measurement is of the plumbing.
    ///
    /// The writers here fold every char and every byte into a rolling FNV-1a hash. Reading each unit
    /// is what makes elision impossible; the hash is also the cheapest useful thing to do with the
    /// data, so it does not distort what is being timed the way, say, appending to a list would.
    /// </summary>
    public static class Materialisation
    {
        private const ulong FnvOffset = 14695981039346656037;
        private const ulong FnvPrime = 1099511628211;

        /// <summary>
        /// A <see cref="TextWriter"/> that reads every character it is given. Exposes the running
        /// hash and the count so a caller can assert BOTH that the right number of characters
        /// arrived and that their content was what the oracle says.
        /// </summary>
        public sealed class ChecksumTextWriter : TextWriter
        {
            private ulong _hash = FnvOffset;
            public long Count { get; private set; }
            public ulong Hash => _hash;

            public void Reset() { _hash = FnvOffset; Count = 0; }

            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char value)
            {
                _hash = (_hash ^ value) * FnvPrime;
                Count++;
            }

            // Heddle's TextWriterScopeRenderer drives THIS overload for static pieces and rendered
            // values alike, so it is the one that must read rather than count. Verified by injecting
            // a count-only body here and watching the differential fail on all eight workloads; the
            // span overload below is never reached by the current engine but is implemented for the
            // same reason.
            public override void Write(string value)
            {
                if (value == null) return;
                for (var i = 0; i < value.Length; i++) Write(value[i]);
            }

            public override void Write(char[] buffer, int index, int count)
            {
                for (var i = 0; i < count; i++) Write(buffer[index + i]);
            }

            public override void Write(ReadOnlySpan<char> buffer)
            {
                for (var i = 0; i < buffer.Length; i++) Write(buffer[i]);
            }
        }

        /// <summary>
        /// An <see cref="IBufferWriter{T}"/> that reads back every byte the engine advances over.
        /// Retains the bytes as well, so a caller can decode and gate the output rather than trust
        /// a count.
        /// </summary>
        public sealed class ChecksumBufferWriter : IBufferWriter<byte>
        {
            private byte[] _buffer;
            private int _written;
            private ulong _hash = FnvOffset;

            public ChecksumBufferWriter(int capacity = 1 << 16) => _buffer = new byte[capacity];

            public int WrittenCount => _written;
            public ulong Hash => _hash;
            public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

            public void Reset() { _written = 0; _hash = FnvOffset; }

            public void Advance(int count)
            {
                // Hash the bytes the engine just wrote. This is the read that makes the write real:
                // without it the buffer could in principle never be touched again.
                var end = _written + count;
                for (var i = _written; i < end; i++) _hash = (_hash ^ _buffer[i]) * FnvPrime;
                _written = end;
            }

            public Memory<byte> GetMemory(int sizeHint = 0) { Grow(sizeHint); return _buffer.AsMemory(_written); }
            public Span<byte> GetSpan(int sizeHint = 0) { Grow(sizeHint); return _buffer.AsSpan(_written); }

            private void Grow(int hint)
            {
                if (hint < 1) hint = 1;
                if (_buffer.Length - _written < hint)
                    Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _written + hint));
            }
        }

        /// <summary>FNV-1a over a string, for comparing a sink's hash against a known-good render.</summary>
        public static ulong HashOf(string text)
        {
            var hash = FnvOffset;
            for (var i = 0; i < text.Length; i++) hash = (hash ^ text[i]) * FnvPrime;
            return hash;
        }

        /// <summary>FNV-1a over bytes.</summary>
        public static ulong HashOf(ReadOnlySpan<byte> bytes)
        {
            var hash = FnvOffset;
            for (var i = 0; i < bytes.Length; i++) hash = (hash ^ bytes[i]) * FnvPrime;
            return hash;
        }
    }
}
