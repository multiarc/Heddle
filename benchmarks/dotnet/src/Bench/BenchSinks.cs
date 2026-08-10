using System;
using System.Buffers;
using System.IO;
using System.Text;

namespace Heddle.Benchmarks.Dotnet.Bench
{
    /// <summary>
    /// Measurement sinks: the cheapest honest consumer of a streaming render. Never used by the gate.
    ///
    /// <para><b>Why these exist.</b> The gate's <c>Materialisation.Checksum*</c> writers fold an FNV-1a
    /// hash over every unit of output — for the TextWriter one through a <i>per-char virtual call</i> —
    /// and allocate a fresh 64 KB buffer per render. That is right for a gate, which must prove the
    /// engine produced the bytes, and wrong for a benchmark, which must measure the engine rather than
    /// the harness. No competitor row pays anything like it: their timed body is "engine renders, string
    /// comes out". Timing the sink rows through the checksum writers therefore measured plumbing, and —
    /// because both backends paid the same large constant — it also compressed the runtime-vs-precompiled
    /// ratio toward 1.0, making every measured deficit a floor rather than an estimate.</para>
    ///
    /// <para><b>Why dropping the checksum is safe here.</b> Verification belongs to the gate, measurement
    /// to the bench. Correctness of every sink is already proven, untimed, in the very process that does
    /// the timing: <c>CrossStackSuite.Setup</c> gates every cell, <c>SelfTest.SinkMaterialisation</c> and
    /// <c>TechniqueDifferential</c> assert count, checksum and byte-for-byte parity through the checksum
    /// writers, and each suite's <c>[GlobalSetup]</c> renders once through these bench sinks and asserts
    /// the result against the gated string render. The historical failures the checksums guard against —
    /// a count-only sink, the JS lazy-rope run — are engine/sink <i>laziness</i> properties, established
    /// once per process. They do not need re-proving on every timed iteration.</para>
    ///
    /// <para>Elision is still impossible: the engine's writes are stores through spans into escaping heap
    /// arrays across non-inlined virtual calls, and each benchmark body returns the sink's written count
    /// for BenchmarkDotNet to consume.</para>
    /// </summary>
    public static class BenchSinks
    {
        /// <summary>
        /// Pre-sized, reused byte sink. <see cref="Advance"/> moves an offset and does nothing else.
        /// This is the true floor for a streaming caller — what a pooled <c>PipeWriter</c> costs.
        /// </summary>
        public sealed class BenchBufferWriter : IBufferWriter<byte>
        {
            private byte[] _buffer;
            private int _written;

            public BenchBufferWriter(int capacity) => _buffer = new byte[capacity < 16 ? 16 : capacity];

            public int WrittenCount => _written;
            public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);
            public int Capacity => _buffer.Length;

            public void Reset() => _written = 0;

            public void Advance(int count) => _written += count;

            public Memory<byte> GetMemory(int sizeHint = 0) { Grow(sizeHint); return _buffer.AsMemory(_written); }
            public Span<byte> GetSpan(int sizeHint = 0) { Grow(sizeHint); return _buffer.AsSpan(_written); }

            // Setup pre-sizes past the high-water mark, so in a correctly warmed suite this never
            // resizes inside the timed region. It stays because a Grow that cannot happen is free,
            // and one that can and is missing is a corrupt measurement.
            private void Grow(int hint)
            {
                if (hint < 1) hint = 1;
                if (_buffer.Length - _written < hint)
                    Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _written + hint));
            }
        }

        /// <summary>
        /// Pre-sized, reused char sink. Every overload is a bulk copy — no per-char loop, no virtual
        /// call per character. This is the least work any real char-stream consumer performs (a
        /// <see cref="StringWriter"/> or <see cref="StreamWriter"/> does at least this much), so the
        /// row measures "engine plus cheapest possible consumer".
        /// </summary>
        public sealed class BenchTextWriter : TextWriter
        {
            private char[] _buffer;
            private int _length;

            public BenchTextWriter(int capacity) => _buffer = new char[capacity < 16 ? 16 : capacity];

            public int Length => _length;
            public ReadOnlySpan<char> WrittenSpan => _buffer.AsSpan(0, _length);
            public int Capacity => _buffer.Length;

            public void Reset() => _length = 0;

            /// <summary>UTF-16: this writer consumes chars. The gate writer claimed UTF-8 while doing
            /// the same, which was simply untrue.</summary>
            public override Encoding Encoding => Encoding.Unicode;

            public override void Write(char value)
            {
                EnsureRoom(1);
                _buffer[_length++] = value;
            }

            // Heddle's TextWriterScopeRenderer drives this overload for static pieces and rendered
            // values alike, so it is the one whose cost dominates the row. One CopyTo, not a loop.
            public override void Write(string value)
            {
                if (value == null) return;
                EnsureRoom(value.Length);
                value.AsSpan().CopyTo(_buffer.AsSpan(_length));
                _length += value.Length;
            }

            public override void Write(char[] buffer, int index, int count)
            {
                EnsureRoom(count);
                buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(_length));
                _length += count;
            }

            public override void Write(ReadOnlySpan<char> buffer)
            {
                EnsureRoom(buffer.Length);
                buffer.CopyTo(_buffer.AsSpan(_length));
                _length += buffer.Length;
            }

            private void EnsureRoom(int needed)
            {
                if (_buffer.Length - _length < needed)
                    Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + needed));
            }
        }
    }
}
