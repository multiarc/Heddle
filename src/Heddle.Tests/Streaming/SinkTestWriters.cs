using System;
using System.Buffers;
using System.Collections.Generic;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Test <see cref="IBufferWriter{T}"/> implementations. <c>ArrayBufferWriter&lt;byte&gt;</c> unavailable on net48, so sink tests use these instead.
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
    /// Returns spans of exactly the requested hint (minimum the contract allows), fresh array each call — stresses sink adapters honor the GetSpan contract.
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
    /// Pool-backed and resettable; for allocation tests where the backing array reuses after warm-up so deltas reflect engine allocations only.
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
