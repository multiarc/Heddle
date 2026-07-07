using System;
using System.Buffers;
using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using Heddle;
using Heddle.Data;
using Heddle.Native;
using Heddle.Runtime;

namespace Heddle.Performance
{
    /// <summary>
    /// Phase 8 WI9 (D6/D13, success criteria 1/4) — the sink benchmark suite over the standing home-page workload.
    /// <c>RenderString</c> guards the string path (Allocated must stay identical to the pre-phase baseline);
    /// <c>RenderTextWriter</c>/<c>RenderUtf8Buffer</c> must allocate below <c>RenderString</c> by at least the final
    /// output size (the full-output string is gone); <c>RenderUtf8PiecesOnly</c> is the ungated measurement powering
    /// D6's ≥ 25% static-piece-transcode revisit trigger. Run per runtime (net8, net10; D16).
    /// </summary>
    [MemoryDiagnoser]
    public class SinkRenderBenchmarks
    {
        private readonly object _model = new object();
        private HeddleTemplate _home;
        private HeddleTemplate _staticOnly;
        private CountingTextWriter _textWriter;
        private ResettableBufferWriter _bufferWriter;

        [GlobalSetup]
        public void Setup()
        {
            AssemblyHelper.Configure(typeof(Program).Assembly);
            _home = new HeddleTemplate(new CompileContext(new TemplateOptions("home")
            {
                FileNamePostfix = ".heddle",
                RootPath = "TestTemplates",
                AllowCSharp = true,
                ProvideLanguageFeatures = false
            }));

            // A static-only variant: the home page's static output stripped of dynamic values — powers D6's
            // transcode-share measurement. Approximated by the home page's string output frozen as pure static text.
            var frozen = _home.Generate(_model);
            _staticOnly = new HeddleTemplate(frozen, new CompileContext(new TemplateOptions()));

            _textWriter = new CountingTextWriter();
            _bufferWriter = new ResettableBufferWriter(1 << 20);
        }

        [Benchmark(Baseline = true)]
        public int RenderString() => _home.Generate(_model).Length;

        [Benchmark]
        public long RenderTextWriter()
        {
            _textWriter.Reset();
            _home.Generate(_model, _textWriter);
            return _textWriter.Count;
        }

        [Benchmark]
        public int RenderUtf8Buffer()
        {
            _bufferWriter.Reset();
            _home.Generate(_model, _bufferWriter);
            return _bufferWriter.WrittenCount;
        }

        [Benchmark]
        public int RenderUtf8PiecesOnly()
        {
            _bufferWriter.Reset();
            _staticOnly.Generate(_model, _bufferWriter);
            return _bufferWriter.WrittenCount;
        }

        private sealed class CountingTextWriter : TextWriter
        {
            public long Count;
            public void Reset() => Count = 0;
            public override Encoding Encoding => Encoding.UTF8;
            public override void Write(char value) => Count++;
            public override void Write(string value) { if (value != null) Count += value.Length; }
            public override void Write(char[] buffer, int index, int count) => Count += count;
#if NET6_0_OR_GREATER
            public override void Write(ReadOnlySpan<char> buffer) => Count += buffer.Length;
#endif
        }

        private sealed class ResettableBufferWriter : IBufferWriter<byte>
        {
            private byte[] _buffer;
            private int _written;
            public ResettableBufferWriter(int capacity) => _buffer = new byte[capacity];
            public int WrittenCount => _written;
            public void Reset() => _written = 0;
            public void Advance(int count) => _written += count;
            public Memory<byte> GetMemory(int sizeHint = 0) { Grow(sizeHint); return _buffer.AsMemory(_written); }
            public Span<byte> GetSpan(int sizeHint = 0) { Grow(sizeHint); return _buffer.AsSpan(_written); }
            private void Grow(int hint)
            {
                if (hint < 1) hint = 1;
                if (_buffer.Length - _written < hint)
                    Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _written + hint));
            }
        }
    }
}
