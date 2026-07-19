using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// B2-R10 — <see cref="TemplateOptions.Encoder"/> behavior: a custom encoder is invoked at every HTML-encode site
    /// (Html-profile bare output and <c>[EncodeOutput]</c> extensions), never at non-encode sites (<c>@raw</c>,
    /// <see cref="OutputProfile.Text"/> bare output, literal text); the <c>null</c> default is byte-identical to the
    /// legacy <see cref="WebUtility.HtmlEncode(string)"/> path (B2-R3); the encoder flows through the span/UTF-8 sink
    /// paths; parallel renders over one options instance are safe (B2-R7 thread-model); and <c>Encoder</c> keys the
    /// options identity by reference (B2-R6).
    /// </summary>
    public class EncoderOptionTests
    {
        /// <summary>Wraps every HTML-significant character in a visible <c>[E:name]</c> token — unmistakably not the
        /// legacy WebUtility path, so an "encoder ran / did not run" assertion is exact.</summary>
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

        public sealed class VModel { public string V { get; set; } }
        public sealed class NumModel { public int N { get; set; } }

        private static string Render(string template, object model, ExType modelType, TemplateOptions options)
        {
            HeddleTemplate.Configure(typeof(EncoderOptionTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template, new CompileContext(options, modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        // ---- Custom encoder invoked for encoding sites ----

        [Fact]
        public void MarkerEncoder_InvokedForHtmlProfileBareOutput()
        {
            var m = new VModel { V = "<b>&'</b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };
            Assert.Equal("[E:lt]b[E:gt][E:amp][E:apos][E:lt]/b[E:gt]", Render("@(V)", m, typeof(VModel), options));
        }

        [Fact]
        public void MarkerEncoder_InvokedForEncodeOutputExtension_Html()
        {
            var m = new VModel { V = "<b>" };
            // @html is an [EncodeOutput] extension; it encodes even under Text.
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text, Encoder = new MarkerEncoder() };
            Assert.Equal("[E:lt]b[E:gt]", Render("@html(V)", m, typeof(VModel), options));
        }

        [Fact]
        public void MarkerEncoder_InvokedForEncodeOutputExtension_String()
        {
            var m = new VModel { V = "<b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text, Encoder = new MarkerEncoder() };
            Assert.Equal("[E:lt]b[E:gt]", Render("@string(V)", m, typeof(VModel), options));
        }

        [Fact]
        public void MarkerEncoder_RoutesFormatterExtensionThroughEncoder()
        {
            // @() of a non-string ([EncodeOutput] integer formatter) under Html routes through the encoder; a bare
            // number carries no specials, so the marker is a no-op — proving the value reached the encoder unharmed.
            var m = new NumModel { N = 42 };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };
            Assert.Equal("42", Render("@(N)", m, typeof(NumModel), options));
        }

        // ---- Custom encoder NOT invoked for non-encoding sites (B2-R8) ----

        [Fact]
        public void MarkerEncoder_NotInvokedForRaw()
        {
            var m = new VModel { V = "<b>&'</b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };
            Assert.Equal("<b>&'</b>", Render("@raw(V)", m, typeof(VModel), options));
        }

        [Fact]
        public void MarkerEncoder_NotInvokedForTextProfileBareOutput()
        {
            var m = new VModel { V = "<b>&'</b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text, Encoder = new MarkerEncoder() };
            Assert.Equal("<b>&'</b>", Render("@(V)", m, typeof(VModel), options));
        }

        [Fact]
        public void MarkerEncoder_NotInvokedForLiteralText()
        {
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };
            Assert.Equal("<p>&amp;</p>", Render("<p>&amp;</p>", null, typeof(object), options));
        }

        [Fact]
        public void MarkerEncoder_AppliesToHtmlRegionOfProfileSwitch()
        {
            // @profile(){{html}} switches to Html mid-template; the same effective encoder applies to that region.
            var m = new VModel { V = "<b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Text, Encoder = new MarkerEncoder() };
            Assert.Equal("[E:lt]b[E:gt]", Render("@profile(){{html}}@(V)", m, typeof(VModel), options));
        }

        // ---- HtmlEncoder.Create(UnicodeRanges.All): the modern opt-in differs from WebUtility ----

        [Fact]
        public void HtmlEncoderAll_DiffersFromWebUtilityOnLatin1AndApostrophe()
        {
            var m = new VModel { V = "grüße '<b>'" };  // grüße with apostrophes and a tag
            var options = new TemplateOptions
            {
                OutputProfile = OutputProfile.Html,
                Encoder = HtmlEncoder.Create(UnicodeRanges.All),
            };
            var actual = Render("@(V)", m, typeof(VModel), options);
            // HtmlEncoder.Create(UnicodeRanges.All) passes ü/ß through (no Latin-1 numeric refs) and encodes ' and <>.
            Assert.Contains("grüße", actual);
            Assert.Contains("&lt;b&gt;", actual);
            Assert.Contains("&#x27;", actual);           // apostrophe encoded (hex form, unlike WebUtility's &#39;)
            Assert.NotEqual(WebUtility.HtmlEncode(m.V), actual);  // genuinely a different encoder
        }

        // ---- null path: byte-identical to the legacy WebUtility baseline (B2-R3) ----

        [Fact]
        public void NullEncoder_IsByteIdenticalToWebUtility()
        {
            var m = new VModel { V = "<b>&\"' grüße ©" };
            var htmlNull = new TemplateOptions { OutputProfile = OutputProfile.Html };  // Encoder null
            var actual = Render("@(V)", m, typeof(VModel), htmlNull);
            Assert.Equal(WebUtility.HtmlEncode(m.V), actual);
        }

#if NET8_0_OR_GREATER
        // ---- span / UTF-8 sink paths carry the encoder (B2-R10) ----

        [Fact]
        public void Encoder_FlowsThroughTextWriterSink()
        {
            var m = new VModel { V = "<b>&</b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };
            HeddleTemplate.Configure(typeof(EncoderOptionTests).GetTypeInfo().Assembly);
            using var t = new HeddleTemplate("@(V)", new CompileContext(options, typeof(VModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            using var sw = new StringWriter();
            t.Generate(m, sw);
            Assert.Equal("[E:lt]b[E:gt][E:amp][E:lt]/b[E:gt]", sw.ToString());
        }

        [Fact]
        public void Encoder_FlowsThroughUtf8Sink()
        {
            var m = new VModel { V = "<b>&</b>" };
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };
            HeddleTemplate.Configure(typeof(EncoderOptionTests).GetTypeInfo().Assembly);
            using var t = new HeddleTemplate("@(V)", new CompileContext(options, typeof(VModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var writer = new System.Buffers.ArrayBufferWriter<byte>();
            t.Generate(m, writer);
            var text = System.Text.Encoding.UTF8.GetString(writer.WrittenSpan);
            Assert.Equal("[E:lt]b[E:gt][E:amp][E:lt]/b[E:gt]", text);
        }
#endif

        // ---- concurrency: one options instance, parallel renders (B2-R7 thread model) ----

        [Fact]
        public void Encoder_OneOptionsInstance_ParallelRendersAreConsistent()
        {
            HeddleTemplate.Configure(typeof(EncoderOptionTests).GetTypeInfo().Assembly);
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html, Encoder = new MarkerEncoder() };
            using var t = new HeddleTemplate("@(V)", new CompileContext(options, typeof(VModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            const string expected = "[E:lt]b[E:gt][E:amp][E:lt]/b[E:gt]";
            Parallel.For(0, 512, _ =>
            {
                var r = t.Generate(new VModel { V = "<b>&</b>" });
                Assert.Equal(expected, r);
            });
        }

        // ---- options identity: Encoder participates by reference (B2-R6) ----

        [Fact]
        public void Encoder_ParticipatesInEqualsAndHashCodeByReference()
        {
            var enc1 = new MarkerEncoder();
            var enc2 = new MarkerEncoder();
            var a = new TemplateOptions { Encoder = enc1 };
            var b = new TemplateOptions { Encoder = enc1 };
            var c = new TemplateOptions { Encoder = enc2 };

            Assert.True(a.Equals(b));                       // same reference -> equal
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.False(a.Equals(c));                      // different reference -> not equal
            Assert.False(new TemplateOptions().Equals(a));  // null vs non-null encoder -> not equal
        }
    }
}
