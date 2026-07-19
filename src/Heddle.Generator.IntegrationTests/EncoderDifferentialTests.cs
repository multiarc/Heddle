using System.Collections.Generic;
using System.Text.Encodings.Web;
using Heddle.Data;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// B2-R7 — the fold-site verification. A <see cref="TemplateOptions.Encoder"/> only stays out of the precompiled
    /// options fingerprint if the generator never constant-folds an *encoded* value into a literal. This suite renders
    /// encode-bearing templates through BOTH backends under a marker encoder (which wraps every HTML-special as
    /// <c>[E:name]</c>) and asserts byte-identical output: if the generator baked a WebUtility-encoded value at compile
    /// time, the precompiled side would show HTML entities where the runtime side shows markers, and these would
    /// diverge. They do not — value encoding is deferred to render on both backends (the <c>RenderType.Encode</c>
    /// marker), so <c>Encoder</c> correctly stays out of the fingerprint.
    /// </summary>
    public class EncoderDifferentialTests
    {
        /// <summary>Wraps every HTML-significant character in a visible <c>[E:name]</c> token — a deterministic,
        /// unmistakably-not-WebUtility encoding, so a divergence between backends is obvious.</summary>
        private sealed class MarkerEncoder : TextEncoder
        {
            private static readonly Dictionary<int, string> Map = new Dictionary<int, string>
            {
                { '<', "[E:lt]" }, { '>', "[E:gt]" }, { '&', "[E:amp]" }, { '"', "[E:quot]" }, { '\'', "[E:apos]" },
            };

            public override int MaxOutputCharactersPerInputCharacter => 16;

            public override bool WillEncode(int unicodeScalar) => Map.ContainsKey(unicodeScalar);

            public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
            {
                for (int i = 0; i < textLength; i++)
                    if (Map.ContainsKey(text[i]))
                        return i;
                return -1;
            }

            public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength,
                out int numberOfCharactersWritten)
            {
                if (!Map.TryGetValue(unicodeScalar, out var replacement) || replacement.Length > bufferLength)
                {
                    numberOfCharactersWritten = 0;
                    return false;
                }

                for (int i = 0; i < replacement.Length; i++)
                    buffer[i] = replacement[i];
                numberOfCharactersWritten = replacement.Length;
                return true;
            }
        }

        [Fact]
        public void MarkerEncoder_BareModelOutput_IsByteIdenticalAcrossBackends()
        {
            const string content = "<p>@()</p>";
            const string hostile = "<b>&\"'x</b>";
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };

            var (precompiled, dyn) = DifferentialHarness.RenderWithOptions(
                "views/encoder-bare.heddle", content, typeof(string), hostile, options);

            Assert.Equal(dyn, precompiled);                 // B2-R7 / G-R3: byte-identical on both backends
            Assert.Contains("[E:lt]", precompiled);          // the marker encoder actually ran (not WebUtility)
            Assert.DoesNotContain("&lt;", precompiled);      // no compile-time WebUtility fold leaked through
        }

        public sealed class HostileModel { public string V { get; set; } }

        [Fact]
        public void MarkerEncoder_TypedMemberOutput_IsByteIdenticalAcrossBackends()
        {
            // A typed @(V) precompiles (the streaming-unicode family); its value is deferred to render (RenderType.Encode).
            // If the generator ever baked a WebUtility-encoded literal here, the backends would diverge under the marker.
            const string content = "<p>@(V)</p>";
            var model = new HostileModel { V = "<b>&\"'x</b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };

            var (precompiled, dyn) = DifferentialHarness.RenderWithOptions(
                "views/encoder-typed.heddle", content, typeof(HostileModel), model, options);

            Assert.Equal(dyn, precompiled);
            Assert.Contains("[E:lt]", precompiled);
            Assert.DoesNotContain("&lt;", precompiled);
        }

        // B2-R7 condition: the fold-site guarantee must also hold "including under HeddleEmitUtf8Pieces". With the
        // UTF-8 piece emission toggle on, static text is emitted as u8 literals — but value encoding must still defer
        // to render (RenderType.Encode). These two exercise that dimension: the marker encoder proves no WebUtility
        // value was constant-folded even when the generator is emitting UTF-8 pieces.
        private static readonly Dictionary<string, string> Utf8PiecesOn =
            new Dictionary<string, string> { ["build_property.HeddleEmitUtf8Pieces"] = "true" };

        [Fact]
        public void MarkerEncoder_BareModelOutput_Utf8Pieces_IsByteIdenticalAcrossBackends()
        {
            const string content = "<p>@()</p>";
            const string hostile = "<b>&\"'x</b>";
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };

            var (precompiled, dyn) = DifferentialHarness.RenderWithOptions(
                "views/encoder-bare-u8.heddle", content, typeof(string), hostile, options, Utf8PiecesOn);

            Assert.Equal(dyn, precompiled);                 // B2-R7: byte-identical with HeddleEmitUtf8Pieces=true
            Assert.Contains("[E:lt]", precompiled);          // the marker encoder ran at render time
            Assert.DoesNotContain("&lt;", precompiled);      // no compile-time WebUtility fold, even under u8 pieces
        }

        [Fact]
        public void MarkerEncoder_TypedMemberOutput_Utf8Pieces_IsByteIdenticalAcrossBackends()
        {
            const string content = "<p>@(V)</p>";
            var model = new HostileModel { V = "<b>&\"'x</b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };

            var (precompiled, dyn) = DifferentialHarness.RenderWithOptions(
                "views/encoder-typed-u8.heddle", content, typeof(HostileModel), model, options, Utf8PiecesOn);

            Assert.Equal(dyn, precompiled);
            Assert.Contains("[E:lt]", precompiled);
            Assert.DoesNotContain("&lt;", precompiled);
        }

        [Fact]
        public void NullEncoder_DefaultPath_IsByteIdenticalAndUsesWebUtility()
        {
            const string content = "<p>@()</p>";
            const string hostile = "<b>&\"'x</b>";
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html }; // Encoder == null -> legacy path

            var (precompiled, dyn) = DifferentialHarness.RenderWithOptions(
                "views/encoder-null.heddle", content, typeof(string), hostile, options);

            Assert.Equal(dyn, precompiled);
            Assert.Equal("<p>&lt;b&gt;&amp;&quot;&#39;x&lt;/b&gt;</p>", precompiled); // WebUtility baseline preserved
        }
    }
}
