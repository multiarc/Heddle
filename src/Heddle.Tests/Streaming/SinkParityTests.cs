using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Phase 8 WI3 — the three-sink parity property oracle over the runtime backend: every fixture rendered through
    /// {string, TextWriter, IBufferWriter&lt;byte&gt;} must be byte-identical (the byte sink UTF-8-normalized). Covers
    /// raw text, the Html profile (encode proxy), all five formatters, unicode/surrogate content, the chunked-tier
    /// large value, branches, lists, and streamed partials.
    /// </summary>
    public class SinkParityTests
    {
        public class Item
        {
            public string Text { get; set; }
            public bool On { get; set; }
        }

        public class M
        {
            public string Name { get; set; }
            public string Body { get; set; }
            public int Count { get; set; }
            public decimal Price { get; set; }
            public DateTime When { get; set; }
            public Guid Id { get; set; }
            public bool Flag { get; set; }
            public List<Item> Items { get; set; }
        }

        private static M Model() => new M
        {
            Name = "Café — Привет 😀",
            Body = "<script>alert(1)</script> & <b>ok</b>",
            Count = 1234567,
            Price = 1999.95m,
            When = new DateTime(2026, 7, 7, 13, 45, 9, DateTimeKind.Utc),
            Id = Guid.Parse("12345678-90ab-cdef-1234-567890abcdef"),
            Flag = true,
            Items = new List<Item>
            {
                new Item { Text = "α", On = true },
                new Item { Text = "😀", On = false },
                new Item { Text = "<x>", On = true }
            }
        };

        public static TheoryData<string, OutputProfile> Fixtures()
        {
            var data = new TheoryData<string, OutputProfile>();
            // Raw text.
            data.Add("Hello @(Name)! Body=@(Body) Count=@(Count)", OutputProfile.Text);
            // Html profile: encoded values through HtmlEncodedRenderer.
            data.Add("<p>@(Body)</p><b>@(Name)</b> n=@(Count)", OutputProfile.Html);
            // Formatters with explicit formats/locale (Text).
            data.Add("i=@int(Count){{N0}} m=@money(Price){{en-US}} d=@date(When){{yyyy-MM-dd}} t=@time(When){{HH:mm:ss}} g=@guid(Id){{D}} s=@string(Name)", OutputProfile.Text);
            // Formatters, default formats (Text).
            data.Add("i=@int(Count) d=@date(When) t=@time(When) g=@guid(Id)", OutputProfile.Text);
            // Formatters under Html (encode-carrier bridge, guid raw).
            data.Add("i=@int(Count){{N0}} m=@money(Price){{en-US}} d=@date(When){{yyyy-MM-dd}} g=@guid(Id)", OutputProfile.Html);
            // Unicode / mixed scripts / surrogate pairs.
            data.Add("Привет @(Name)! 😀 Café — @(Body) 中文 テスト", OutputProfile.Text);
            // Branch set (root-level participants → root locals frame on every sink).
            data.Add("@if(Flag){{yes:@(Name)}}@else(){{no}} | @ifnot(Flag){{off}}", OutputProfile.Text);
            // List over typed elements (member-path element access).
            data.Add("@list(Items){{[@(Text)|@if(On){{on}}@else(){{off}}]}}", OutputProfile.Text);
            // List under Html (each element encoded through the proxy).
            data.Add("@list(Items){{<li>@(Text)</li>}}", OutputProfile.Html);
            return data;
        }

        [Theory]
        [MemberData(nameof(Fixtures))]
        public void RuntimeBackend_ThreeSinksByteIdentical(string document, OutputProfile profile)
        {
            SinkTestHarness.AssertThreeSinkParity(document, Model(), typeof(M), profile);
        }

        [Fact]
        public void LargeChunkedValue_ThreeSinksByteIdentical()
        {
            // A dynamic value > 5 461 UTF-16 units drives the Utf8 sink into the chunked (Encoder.Convert) tier;
            // a large static piece does too. Both must be byte-identical across sinks.
            var big = new string('x', 20000);
            var model = new M { Name = big, Body = new string('Ω', 8000) };
            var doc = "START" + new string('.', 7000) + "@(Name)MID@(Body)END";
            SinkTestHarness.AssertThreeSinkParity(doc, model, typeof(M), OutputProfile.Text);
        }

        [Fact]
        public void StreamedPartial_ThreeSinksByteIdentical()
        {
            // Proves @partial streams identically across sinks (D11): the parent's partial output interleaves through
            // each sink in call order, byte-identical to the string path.
            var dir = Path.Combine(Path.GetTempPath(), "heddle_sinkpartial_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "child.heddle"), "[child:@(Name) 😀 <x>]");
                var options = new TemplateOptions
                {
                    RootPath = dir + Path.DirectorySeparatorChar,
                    FileNamePostfix = ".heddle",
                    OutputProfile = OutputProfile.Text
                };
                var parent = "BEFORE @partial(){{child}} MIDDLE @partial(){{child}} AFTER";
                var t = new HeddleTemplate(parent, new CompileContext(options, typeof(M)));
                Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
                var model = Model();

                var s = t.Generate(model);
                var sw = new StringWriter();
                t.Generate(model, sw);
                var bw = new TestBufferWriter();
                t.Generate(model, bw);

                Assert.Equal(s, sw.ToString());
                Assert.Equal(Encoding.UTF8.GetBytes(s), bw.ToArray());
                Assert.Contains("[child:", s);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void NullModel_ThreeSinksByteIdentical()
        {
            SinkTestHarness.AssertThreeSinkParity("static only, no model reads", null, typeof(M), OutputProfile.Text);
        }

        [Fact]
        public void NullWriter_Throws()
        {
            var t = SinkTestHarness.Compile("hi @(Name)", typeof(M), OutputProfile.Text);
            Assert.Throws<ArgumentNullException>(() => t.Generate(Model(), (TextWriter) null));
            Assert.Throws<ArgumentNullException>(() => t.Generate(Model(), (System.Buffers.IBufferWriter<byte>) null));
        }
    }
}
