using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Heddle.Data;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Differential parity for the <see cref="HtmlEncodedRenderer"/> span path: over an adversarial corpus and a
    /// seeded fuzz set, its output must be byte-identical to <c>WebUtility.HtmlEncode(string)</c> (legacy path) or
    /// <c>TextEncoder.Encode(string)</c> (configured path), through both a span-accepting and a string-only sink.
    /// Non-ASCII input is written as \u escapes to resist source-encoding normalization.
    /// </summary>
    public class HtmlEncodedRendererSpanParityTests
    {
        private sealed class SpanSink : ISpanScopeRenderer, IEncoderCarrier
        {
            private readonly StringBuilder _sb = new StringBuilder();
            public TextEncoder EncoderValue;
            TextEncoder IEncoderCarrier.Encoder => EncoderValue;

            public void Render(string data) => _sb.Append(data);

            public void Render(ReadOnlySpan<char> data) => _sb.Append(data.ToString());

            public override string ToString() => _sb.ToString();
        }

        private sealed class StringOnlySink : IScopeRenderer, IEncoderCarrier
        {
            private readonly StringBuilder _sb = new StringBuilder();
            public TextEncoder EncoderValue;
            TextEncoder IEncoderCarrier.Encoder => EncoderValue;

            public void Render(string data) => _sb.Append(data);

            public override string ToString() => _sb.ToString();
        }

        /// <summary>Expands <c>&lt;</c> beyond the renderer's chunk buffer so a single scalar can make no chunked
        /// progress, forcing the string-path fallback inside the configured-encoder branch.</summary>
        private sealed class OversizedScalarEncoder : TextEncoder
        {
            public const int Expansion = 300;

            public override int MaxOutputCharactersPerInputCharacter => Expansion;

            public override bool WillEncode(int unicodeScalar) => unicodeScalar == '<';

            public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
            {
                for (int i = 0; i < textLength; i++)
                    if (text[i] == '<')
                        return i;
                return -1;
            }

            public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength,
                out int numberOfCharactersWritten)
            {
                if (unicodeScalar != '<' || bufferLength < Expansion)
                {
                    numberOfCharactersWritten = 0;
                    return false;
                }

                for (int i = 0; i < Expansion; i++)
                    buffer[i] = 'x';
                numberOfCharactersWritten = Expansion;
                return true;
            }
        }

        private static string RenderSpanThroughSpanSink(string input, TextEncoder encoder)
        {
            var sink = new SpanSink { EncoderValue = encoder };
            new HtmlEncodedRenderer(sink).Render(input.AsSpan());
            return sink.ToString();
        }

        private static string RenderSpanThroughStringOnlySink(string input, TextEncoder encoder)
        {
            var sink = new StringOnlySink { EncoderValue = encoder };
            new HtmlEncodedRenderer(sink).Render(input.AsSpan());
            return sink.ToString();
        }

        private static string Expected(string input, TextEncoder encoder)
            => encoder == null ? WebUtility.HtmlEncode(input) : encoder.Encode(input);

        private static void AssertParity(string input, TextEncoder encoder)
        {
            var expected = Expected(input, encoder);
            Assert.Equal(expected, RenderSpanThroughSpanSink(input, encoder));
            Assert.Equal(expected, RenderSpanThroughStringOnlySink(input, encoder));
        }

        private static IEnumerable<TextEncoder> Encoders()
        {
            yield return null;
            yield return HtmlEncoder.Default;
            yield return HtmlEncoder.Create(UnicodeRanges.All);
        }

        private const string AstralPair = "\uD83D\uDE00";    // U+1F600

        private static IEnumerable<string> AdversarialCorpus()
        {
            yield return "";
            yield return "plain ascii text, no triggers: azAZ 0123456789 =/+.!";
            yield return "<";
            yield return ">";
            yield return "\"";
            yield return "'";
            yield return "&";
            yield return "\u009F";               // below the Latin-1 reference band: raw on the legacy path
            yield return "\u00A0";               // band start
            yield return "\u00A1";
            yield return "\u00FF";               // band end
            yield return "\u0100";               // above the band: raw on the legacy path
            yield return "\u4E2D";               // CJK, raw on the legacy path
            yield return "\uD800";               // lone high surrogate
            yield return "\uDC00";               // lone low surrogate
            yield return AstralPair;
            yield return "x\uD83D";              // lone high at end
            yield return "\uDE00x";              // lone low at start
            yield return "<abc";                 // dirty at position 0
            yield return "ab<cd";                // dirty mid
            yield return "abc<";                 // dirty at end
            yield return new string('c', 1000);  // clean, longer than the chunk buffer
            yield return new string('&', 600);   // dirty run whose encoded form spans many chunks
            yield return new string('a', 300) + "<" + new string('b', 300);
            yield return string.Concat("\u00E9", new string('a', 254), AstralPair, new string('b', 40));

            // Clean prefixes bracketing the chunk-buffer length ahead of a dirty run with an astral pair.
            for (int prefix = 255; prefix <= 257; prefix++)
                yield return new string('p', prefix) + "<\u00E9" + AstralPair + new string('q', 300);

            // Multi-KB blob mixing every category, with astral pairs free to straddle chunk boundaries.
            var blob = new StringBuilder();
            for (int i = 0; i < 400; i++)
                blob.Append("segment ").Append(i).Append('<').Append('\u00E9').Append(AstralPair)
                    .Append('\u4E2D').Append('&').Append('\'');
            yield return blob.ToString();

            var pairs = new StringBuilder();
            for (int i = 0; i < 500; i++)
                pairs.Append(AstralPair);
            yield return pairs.ToString();
        }

        [Fact]
        public void SpanPath_AdversarialCorpus_ByteIdenticalOnBothEncoderPathsAndBothSinks()
        {
            foreach (var encoder in Encoders())
                foreach (var input in AdversarialCorpus())
                    AssertParity(input, encoder);
        }

        /// <summary>Seeded fuzz over an alphabet of trigger chars, Latin-1 band edges, surrogate halves and pairs;
        /// the seed is fixed so a failure reproduces.</summary>
        [Fact]
        public void SpanPath_SeededFuzz_ByteIdenticalOnBothEncoderPathsAndBothSinks()
        {
            string[] alphabet =
            {
                "a", "Z", " ", "<", ">", "&", "\"", "'", "\u009F", "\u00A0", "\u00FF", "\u0100",
                "\u4E2D", AstralPair, "\uD800", "\uDC00", "\r\n", "\t", "=/",
            };
            var rng = new Random(12345);
            for (int round = 0; round < 200; round++)
            {
                var sb = new StringBuilder();
                int pieces = rng.Next(0, 400);
                for (int i = 0; i < pieces; i++)
                    sb.Append(alphabet[rng.Next(alphabet.Length)]);
                var input = sb.ToString();
                foreach (var encoder in Encoders())
                    AssertParity(input, encoder);
            }
        }

        /// <summary>Exhaustive single-char sweep of the BMP proving the legacy span path's trigger set matches
        /// <c>WebUtility.HtmlEncode</c> for every character, lone surrogates included.</summary>
        [Fact]
        public void LegacySpanPath_EveryBmpChar_MatchesWebUtility()
        {
            for (int c = 0; c <= 0xFFFF; c++)
            {
                var input = ((char)c).ToString();
                Assert.Equal(WebUtility.HtmlEncode(input), RenderSpanThroughSpanSink(input, null));
            }
        }

        [Fact]
        public void ConfiguredPath_ScalarWiderThanChunkBuffer_FallsBackByteIdentically()
        {
            var encoder = new OversizedScalarEncoder();
            AssertParity("a<b", encoder);
            AssertParity("<", encoder);
            AssertParity(new string('a', 300) + "<" + new string('b', 300), encoder);
        }

#if NET10_0_OR_GREATER
        private sealed class FixedBufferSpanSink : ISpanScopeRenderer, IEncoderCarrier
        {
            private readonly char[] _buffer = new char[4096];
            private int _length;
            public TextEncoder EncoderValue;
            TextEncoder IEncoderCarrier.Encoder => EncoderValue;

            public void Render(string data) => Render(data.AsSpan());

            public void Render(ReadOnlySpan<char> data)
            {
                data.CopyTo(_buffer.AsSpan(_length));
                _length += data.Length;
            }

            public override string ToString() => new string(_buffer, 0, _length);
        }

        [Fact]
        public void CleanSpan_FastPath_DoesNotAllocate()
        {
            foreach (var encoder in new[] { (TextEncoder)null, HtmlEncoder.Default })
            {
                var sink = new FixedBufferSpanSink { EncoderValue = encoder };
                var renderer = new HtmlEncodedRenderer(sink);
                renderer.Render("warmup clean text".AsSpan());

                long before = GC.GetAllocatedBytesForCurrentThread();
                renderer.Render("clean ascii text with nothing to encode 0123456789".AsSpan());
                long after = GC.GetAllocatedBytesForCurrentThread();
                Assert.Equal(0, after - before);
            }
        }
#endif
    }
}
