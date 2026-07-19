using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 8 D7 (WI7) — the precompiled-backend sink lanes. The differential byte lane renders each template through
    /// the generated string / TextWriter / IBufferWriter&lt;byte&gt; entry points and asserts they match the dynamic
    /// runtime string render (byte sink UTF-8-normalized), with <c>HeddleEmitUtf8Pieces</c> both off and on.
    /// <see cref="Utf8FastPath_OptedInPieces_BypassTranscode"/> proves an opted-in template's static pieces reach the
    /// byte sink via the zero-transcode <c>RenderUtf8</c> branch (no <c>GetSpan(len*3)</c> transcode request).
    /// </summary>
    public class PrecompiledSinkTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        private sealed class GrowBufferWriter : IBufferWriter<byte>
        {
            private byte[] _b = new byte[256];
            private int _n;
            public byte[] ToArray() => _b.AsSpan(0, _n).ToArray();
            public void Advance(int count) => _n += count;
            public Memory<byte> GetMemory(int sizeHint = 0) { Grow(sizeHint); return _b.AsMemory(_n); }
            public Span<byte> GetSpan(int sizeHint = 0) { Grow(sizeHint); return _b.AsSpan(_n); }
            private void Grow(int hint) { if (hint < 1) hint = 1; if (_b.Length - _n < hint) Array.Resize(ref _b, Math.Max(_b.Length * 2, _n + hint)); }
        }

        /// <summary>Records the sizeHints requested so the transcode path (GetSpan(len*3) &gt; 0) is distinguishable
        /// from the u8 straight-copy path (BuffersExtensions.Write → GetSpan(0)).</summary>
        private sealed class RecordingBufferWriter : IBufferWriter<byte>
        {
            private byte[] _b = new byte[1 << 16];
            private int _n;
            public readonly List<int> Hints = new List<int>();
            public byte[] ToArray() => _b.AsSpan(0, _n).ToArray();
            public void Advance(int count) => _n += count;
            public Memory<byte> GetMemory(int sizeHint = 0) { Record(sizeHint); Grow(sizeHint); return _b.AsMemory(_n); }
            public Span<byte> GetSpan(int sizeHint = 0) { Record(sizeHint); Grow(sizeHint); return _b.AsSpan(_n); }
            private void Record(int hint) => Hints.Add(hint);
            private void Grow(int hint) { if (hint < 1) hint = 1; if (_b.Length - _n < hint) Array.Resize(ref _b, Math.Max(_b.Length * 2, _n + hint)); }
        }

        private static (MethodInfo str, MethodInfo tw, MethodInfo bw) SinkEntries(Assembly asm)
        {
            var entryType = asm.GetTypes().First(t => t.IsClass && t.IsAbstract && t.IsSealed &&
                t.Namespace == DifferentialHarness.GeneratedNamespace &&
                t.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(m => m.Name == "Generate"));
            var gens = entryType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "Generate").ToList();
            return (
                gens.First(m => m.ReturnType == typeof(string)),
                gens.First(m => m.GetParameters()[1].ParameterType == typeof(TextWriter)),
                gens.First(m => typeof(IBufferWriter<byte>).IsAssignableFrom(m.GetParameters()[1].ParameterType)));
        }

        private static void AssertByteLane(string template, object model, bool emitUtf8)
        {
            var opts = emitUtf8
                ? new Dictionary<string, string> { ["build_property.HeddleEmitUtf8Pieces"] = "true" }
                : null;
            var gen = DifferentialHarness.Generate(new[] { ("views/sink.heddle", template) }, opts);
            Assert.False(gen.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
            Assert.NotNull(gen.Assembly);

            var (str, tw, bw) = SinkEntries(gen.Assembly);
            var precompiledStr = (string) str.Invoke(null, new object[] { model, null, null });
            var sw = new StringWriter();
            tw.Invoke(null, new object[] { model, sw, null, null });
            var writer = new GrowBufferWriter();
            bw.Invoke(null, new object[] { model, writer, null, null });

            var dyn = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), typeof(Product)))
                .Generate(model);

            Assert.Equal(dyn, precompiledStr);                 // string entry == runtime
            Assert.Equal(dyn, sw.ToString());                  // TextWriter entry == runtime
            Assert.Equal(Encoding.UTF8.GetBytes(dyn), writer.ToArray());  // byte entry == runtime UTF-8
        }

        public static TheoryData<string> Templates()
        {
            return new TheoryData<string>
            {
                "@model(){{" + ProductType + "}}@\\\n<h1>Hello — Привет 😀</h1>\n",          // static-only
                "@model(){{" + ProductType + "}}@\\\n<h1>@(Name)</h1><p>@(Description) 😀</p>\n", // dynamic values
                "@model(){{" + ProductType + "}}@\\\n<b>@(Manufacturer.Name)</b> d=@(Description)\n",   // nested path
            };
        }

        [Theory]
        [MemberData(nameof(Templates))]
        public void ByteLane_Utf8Off_MatchesRuntime(string template)
        {
            AssertByteLane(template, Product(), emitUtf8: false);
        }

        [Theory]
        [MemberData(nameof(Templates))]
        public void ByteLane_Utf8On_MatchesRuntime(string template)
        {
            AssertByteLane(template, Product(), emitUtf8: true);
        }

        [Fact]
        public void ByteLane_NullModel_MatchesRuntime()
        {
            AssertByteLane("@model(){{" + ProductType + "}}@\\\n<h1>@(Name)</h1>\n", null, emitUtf8: true);
        }

        [Fact]
        public void Utf8FastPath_OptedInPieces_BypassTranscode()
        {
            // A static-only opted-in template: every write is a piece routed through WritePiece → RenderUtf8 (u8 twin)
            // → BuffersExtensions.Write → GetSpan(0). The transcode path would request GetSpan(len*3) > 0. So with the
            // opt-in the writer sees no positive size hint; without it, the piece transcodes and a positive hint appears.
            var template = "@model(){{" + ProductType + "}}@\\\n<h1>Hello — Привет 😀 static only</h1>\n";

            var onGen = DifferentialHarness.Generate(new[] { ("views/fp.heddle", template) },
                new Dictionary<string, string> { ["build_property.HeddleEmitUtf8Pieces"] = "true" });
            var (_, _, bwOn) = SinkEntries(onGen.Assembly);
            var wOn = new RecordingBufferWriter();
            bwOn.Invoke(null, new object[] { Product(), wOn, null, null });
            Assert.True(wOn.ToArray().Length > 0);
            Assert.All(wOn.Hints, h => Assert.Equal(0, h));   // fast path: no transcode size hint

            var offGen = DifferentialHarness.Generate(new[] { ("views/fp.heddle", template) });
            var (_, _, bwOff) = SinkEntries(offGen.Assembly);
            var wOff = new RecordingBufferWriter();
            bwOff.Invoke(null, new object[] { Product(), wOff, null, null });
            Assert.Contains(wOff.Hints, h => h > 0);          // no opt-in: the piece transcodes (GetSpan(len*3))
        }

        private static Product Product() => new Product
        {
            Name = "Widget & Co <b>",
            Description = "Ünïcödé — 中文 😀",
            Manufacturer = new Manufacturer { Name = "Acme" }
        };
    }
}
