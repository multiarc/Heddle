using System;
using System.Buffers;
using System.Collections.Generic;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Test-owned <see cref="IBufferWriter{T}"/> implementations (phase 8 WI2). <c>ArrayBufferWriter&lt;byte&gt;</c> is
    /// in-box only on netcoreapp3.0+/netstandard2.1 and is <b>not</b> in the System.Memory package — so on the net48
    /// test lane the sink tests run over these writers instead (same assertions, same fixtures).
    /// </summary>
    internal sealed class TestBufferWriter : IBufferWriter<byte>
    {
        private byte[] _buffer;
        private int _written;

        public TestBufferWriter(int initialCapacity = 256)
        {
            _buffer = new byte[Math.Max(1, initialCapacity)];
        }

        public int WrittenCount => _written;
        public ReadOnlySpan<byte> WrittenSpan => new ReadOnlySpan<byte>(_buffer, 0, _written);
        public byte[] ToArray() => WrittenSpan.ToArray();
        public void Clear() => _written = 0;

        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (_written + count > _buffer.Length)
                throw new InvalidOperationException("Advance past the end of the buffer.");
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return new Memory<byte>(_buffer, _written, _buffer.Length - _written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return new Span<byte>(_buffer, _written, _buffer.Length - _written);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 1)
                sizeHint = 1;
            int available = _buffer.Length - _written;
            if (available >= sizeHint)
                return;
            int newSize = Math.Max(_buffer.Length * 2, _written + sizeHint);
            Array.Resize(ref _buffer, newSize);
        }
    }

    /// <summary>
    /// A deliberately stingy <see cref="IBufferWriter{T}"/> that returns spans of <b>exactly</b> the requested hint
    /// (the minimum the contract allows) and a fresh array every call — stressing that the sink adapters honor the
    /// GetSpan contract (never assume a larger span; never write after Advance; request a fresh span each iteration).
    /// </summary>
    internal sealed class StingyBufferWriter : IBufferWriter<byte>
    {
        private readonly List<byte> _data = new List<byte>();
        private byte[] _current;

        public byte[] ToArray() => _data.ToArray();

        public void Advance(int count)
        {
            if (_current == null)
                throw new InvalidOperationException("Advance without a prior GetSpan/GetMemory.");
            if (count < 0 || count > _current.Length)
                throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++)
                _data.Add(_current[i]);
            _current = null;
        }

        public Memory<byte> GetMemory(int sizeHint = 0) => Allocate(sizeHint);
        public Span<byte> GetSpan(int sizeHint = 0) => Allocate(sizeHint);

        private byte[] Allocate(int sizeHint)
        {
            _current = new byte[sizeHint <= 0 ? 1 : sizeHint];
            return _current;
        }
    }

    /// <summary>
    /// A resettable, pool-backed <see cref="IBufferWriter{T}"/> for the allocation guarantee tests (D13): after warm-up
    /// its backing array is reused across renders, so a per-render allocation delta reflects only the engine's own
    /// managed allocations, not the sink's buffer growth.
    /// </summary>
    internal sealed class PooledResettableBufferWriter : IBufferWriter<byte>
    {
        private byte[] _buffer;
        private int _written;

        public PooledResettableBufferWriter(int initialCapacity = 1 << 20)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, initialCapacity));
        }

        public int WrittenCount => _written;
        public void Reset() => _written = 0;

        public void Advance(int count)
        {
            if (count < 0 || _written + count > _buffer.Length)
                throw new InvalidOperationException("Advance past the end of the buffer.");
            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return new Memory<byte>(_buffer, _written, _buffer.Length - _written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return new Span<byte>(_buffer, _written, _buffer.Length - _written);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 1)
                sizeHint = 1;
            int available = _buffer.Length - _written;
            if (available >= sizeHint)
                return;
            int newSize = Math.Max(_buffer.Length * 2, _written + sizeHint);
            var grown = ArrayPool<byte>.Shared.Rent(newSize);
            Array.Copy(_buffer, grown, _written);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = grown;
        }
    }
}
