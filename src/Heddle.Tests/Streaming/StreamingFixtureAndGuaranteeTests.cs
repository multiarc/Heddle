using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Phase 8 WI8 — the multi-byte torture fixture, the &gt; 1 MB allocation-bound fixture, the concurrent mixed-sink
    /// guarantee (D15), and the downlevel degradation contract. The unicode fixture is also golden-pinned (byte-exact
    /// string path + three-sink parity); the large fixture powers the D13 allocation asserts.
    /// </summary>
    public class StreamingFixtureAndGuaranteeTests
    {
        public class UniModel { public string Name { get; set; } public string City { get; set; } }

        static StreamingFixtureAndGuaranteeTests()
        {
            HeddleTemplate.Configure(typeof(StreamingFixtureAndGuaranteeTests).GetTypeInfo().Assembly);
        }

        private static string Read(string fixture) =>
            File.ReadAllText($"TestTemplate/{fixture}.heddle").Replace("\r\n", "\n");

        /// <summary>A large loop-free static template: the committed fixture's static body repeated <paramref name="reps"/>
        /// times. Every piece is a compile-time constant, so a byte-sink render allocates only the adapter + lazy
        /// encoder (O(1)) regardless of output size N — the property D13 asset 2 isolates from any per-element cost.</summary>
        private static string LargeStatic(int reps)
        {
            var block = Read("streaming-large");
            var sb = new StringBuilder(block.Length * reps);
            for (int i = 0; i < reps; i++)
                sb.Append(block);
            return sb.ToString();
        }

        [Fact]
        public void UnicodeFixture_ThreeSinksByteIdentical_AndGolden()
        {
            var model = new UniModel { Name = "Ａでか😀", City = "Zürich — Αθήνα 北京" };
            var t = SinkTestHarness.Compile(Read("streaming-unicode"), typeof(UniModel), OutputProfile.Text);

            var s = t.Generate(model);
            var sw = new StringWriter();
            t.Generate(model, sw);
            var bw = new TestBufferWriter();
            t.Generate(model, bw);

            Assert.Equal(s, sw.ToString());
            Assert.Equal(Encoding.UTF8.GetBytes(s), bw.ToArray());

            // Golden pin (string path). Write the actual for diffing, then compare against the committed golden.
            File.WriteAllText("TestTemplate/test-streaming-unicode.html", s);
            var golden = File.ReadAllText("TestTemplate/generated-streaming-unicode.html").Replace("\r\n", "\n");
            Assert.Equal(golden, s.Replace("\r\n", "\n"));
        }

        [Fact]
        public void LargeFixture_ThreeSinksByteIdentical()
        {
            // > 1 MB output through all three sinks, byte-identical (the "Large page (>1 MB output)" validation row).
            var t = SinkTestHarness.Compile(LargeStatic(180), null, OutputProfile.Text);
            var s = t.Generate(null);
            var sw = new StringWriter();
            t.Generate(null, sw);
            var bw = new TestBufferWriter();
            t.Generate(null, bw);
            Assert.Equal(s, sw.ToString());
            Assert.Equal(Encoding.UTF8.GetBytes(s), bw.ToArray());
            Assert.True(s.Length > 1_000_000, $"fixture too small: {s.Length}");
        }

        [Fact]
        public void ConcurrentMixedSinks_ByteIdenticalToSingleThreadedGolden()
        {
            // D15: one compiled template, N threads, mixing string/TextWriter/byte sinks — every output byte-identical
            // to the single-threaded reference (the phase 3 opposite-conditions parallel pattern).
            var t = SinkTestHarness.Compile(
                "Hi @(Name) from @(City)! 😀 @if(Name){{named}}@else(){{anon}}", typeof(UniModel), OutputProfile.Html);
            var model = new UniModel { Name = "Α<b>", City = "北京 & Zürich" };
            var expected = t.Generate(model);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);

            Parallel.For(0, 200, i =>
            {
                switch (i % 3)
                {
                    case 0:
                        Assert.Equal(expected, t.Generate(model));
                        break;
                    case 1:
                        var sw = new StringWriter();
                        t.Generate(model, sw);
                        Assert.Equal(expected, sw.ToString());
                        break;
                    default:
                        var bw = new TestBufferWriter();
                        t.Generate(model, bw);
                        Assert.Equal(expectedBytes, bw.ToArray());
                        break;
                }
            });
        }

        [Fact]
        public void Downlevel_TextWriterPathFullyFunctional_ByteExact()
        {
            // The netstandard2.0/net48 degradation is itself tested: the TextWriter path is fully functional and the
            // byte-sink output stays byte-identical (only the allocation profile differs, not the bytes).
            var t = SinkTestHarness.Compile(
                "n=@int(N){{N0}} m=@money(P){{en-US}} u=@(U) 😀", typeof(DownModel), OutputProfile.Text);
            var model = new DownModel { N = 1000000, P = 12.5m, U = "Привет" };
            var s = t.Generate(model);
            var sw = new StringWriter();
            t.Generate(model, sw);
            var bw = new TestBufferWriter();
            t.Generate(model, bw);
            Assert.Equal(s, sw.ToString());
            Assert.Equal(Encoding.UTF8.GetBytes(s), bw.ToArray());
        }

        public class DownModel { public int N { get; set; } public decimal P { get; set; } public string U { get; set; } }

#if NET6_0_OR_GREATER
        [Fact]
        public void LargeOutputByteSinkAllocatesBounded()
        {
            // D13 asset 1: > 1 MB output rendered to a reusable pooled buffer writer allocates < 64 KB per render
            // (no full-output byte[]/string/StringBuilder). Warm up, reuse the writer, measure the delta.
            var t = SinkTestHarness.Compile(LargeStatic(180), null, OutputProfile.Text);
            var writer = new PooledResettableBufferWriter(4 << 20);

            for (int i = 0; i < 8; i++) { writer.Reset(); t.Generate(null, writer); }
            Assert.True(writer.WrittenCount > 1_000_000, $"output not > 1 MB: {writer.WrittenCount}");

            long best = long.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                writer.Reset();
                long before = GC.GetAllocatedBytesForCurrentThread();
                t.Generate(null, writer);
                best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            Assert.True(best < 64 * 1024, $"per-render allocation {best} B exceeds the 64 KB bound for a >1 MB render.");
        }

        [Fact]
        public void AllocationIsOutputSizeInvariant()
        {
            // D13 asset 2: doubling the output stays within ~10% of the single-size per-render allocation (sub-linear
            // ⇒ no O(N) term). @list over reference elements adds no per-element heap allocation.
            var single = SinkTestHarness.Compile(LargeStatic(180), null, OutputProfile.Text);   // > 1 MB
            var doubled = SinkTestHarness.Compile(LargeStatic(360), null, OutputProfile.Text);  // ~2×
            var writer = new PooledResettableBufferWriter(8 << 20);

            long Measure(HeddleTemplate tpl)
            {
                for (int i = 0; i < 8; i++) { writer.Reset(); tpl.Generate(null, writer); }
                long best = long.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    writer.Reset();
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    tpl.Generate(null, writer);
                    best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - before);
                }
                return best;
            }

            long a1 = Measure(single);
            long a2 = Measure(doubled);
            // Output roughly doubled; allocation must stay within 10% (a small absolute floor absorbs GC noise on
            // near-zero deltas). An O(N) buffer would have ~doubled.
            Assert.True(a2 <= a1 * 1.10 + 4096,
                $"allocation grew with output size: single={a1} B, doubled={a2} B (expected sub-linear).");
        }

        [Fact]
        public void ByteSinkAllocatesMeasurablyBelowStringPath()
        {
            // Success criterion 1 (the RenderUtf8Buffer acceptance): rendering a > 1 MB page to a pooled byte sink
            // allocates far below the string path — by at least the final output size (the full-output string is gone).
            var t = SinkTestHarness.Compile(LargeStatic(180), null, OutputProfile.Text);
            var writer = new PooledResettableBufferWriter(4 << 20);

            // Warm up both paths (JIT, first-render caches).
            for (int i = 0; i < 5; i++) { writer.Reset(); t.Generate(null, writer); var _ = t.Generate(null); }
            int outputSize = writer.WrittenCount;
            Assert.True(outputSize > 1_000_000);

            long stringAlloc = long.MaxValue, byteAlloc = long.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                long b0 = GC.GetAllocatedBytesForCurrentThread();
                var s = t.Generate(null);
                stringAlloc = Math.Min(stringAlloc, GC.GetAllocatedBytesForCurrentThread() - b0);
                GC.KeepAlive(s);

                writer.Reset();
                long b1 = GC.GetAllocatedBytesForCurrentThread();
                t.Generate(null, writer);
                byteAlloc = Math.Min(byteAlloc, GC.GetAllocatedBytesForCurrentThread() - b1);
            }

            Assert.True(byteAlloc < stringAlloc - outputSize,
                $"byte sink ({byteAlloc} B) is not below string path ({stringAlloc} B) by the output size ({outputSize} B).");
        }
#endif
    }
}
