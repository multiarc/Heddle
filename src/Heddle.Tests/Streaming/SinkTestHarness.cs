using System;
using System.IO;
using System.Reflection;
using System.Text;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests.Streaming
{
    /// <summary>
    /// Shared harness for the phase 8 three-sink parity property (WI3/WI4): render one compiled template through the
    /// string, <see cref="TextWriter"/>, and UTF-8 <c>IBufferWriter&lt;byte&gt;</c> sinks and assert
    /// <c>textWriter == string</c> and <c>utf8Bytes == Encoding.UTF8.GetBytes(string)</c> — the property oracle from the
    /// roadmap's TDD verdict, strictly stronger than sampled goldens (the string path is already golden-pinned).
    /// </summary>
    internal static class SinkTestHarness
    {
        static SinkTestHarness()
        {
            HeddleTemplate.Configure(typeof(SinkTestHarness).GetTypeInfo().Assembly);
        }

        public static HeddleTemplate Compile(string document, Type modelType, OutputProfile profile)
        {
            var options = new TemplateOptions { OutputProfile = profile };
            var t = modelType == null
                ? new HeddleTemplate(document, new CompileContext(options))
                : new HeddleTemplate(document, new CompileContext(options, modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        public static string RenderString(HeddleTemplate t, object model) => t.Generate(model);

        public static string RenderTextWriter(HeddleTemplate t, object model)
        {
            var sw = new StringWriter();
            t.Generate(model, sw);
            return sw.ToString();
        }

        public static byte[] RenderUtf8(HeddleTemplate t, object model)
        {
            var bw = new TestBufferWriter();
            t.Generate(model, bw);
            return bw.ToArray();
        }

        /// <summary>Renders through all three sinks and asserts byte parity. Returns the string output.</summary>
        public static string AssertThreeSinkParity(string document, object model, Type modelType, OutputProfile profile)
        {
            var t = Compile(document, modelType, profile);
            var s = RenderString(t, model);
            var tw = RenderTextWriter(t, model);
            var u8 = RenderUtf8(t, model);

            Assert.Equal(s, tw);
            Assert.Equal(Encoding.UTF8.GetBytes(s), u8);
            return s;
        }
    }
}
