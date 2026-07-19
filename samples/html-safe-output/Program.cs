using System;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Samples.HtmlSafeOutput
{
    // Sample 5 — the Html output profile and pluggable output encoding. One template carrying a hostile model
    // string renders under BOTH profile defaults (Text = raw, Html = auto-encoded via the built-in WebUtility path,
    // with a @raw opt-out island). Two more renders swap the encoder: the @tagged extension (the 1.x seam) and
    // TemplateOptions.Encoder (the modern seam — a System.Text.Encodings.Web.TextEncoder applied at every encode site).
    internal sealed class PageModel
    {
        public string Payload { get; set; }
    }

    internal static class Program
    {
        // Hostile string: HTML-significant characters, quotes, an ampersand, and non-ASCII text.
        private const string Payload = "<script>alert(\"xss\") & 'grüße' © 2026</script>";

        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            HeddleTemplate.Configure(typeof(Program).Assembly);

            var model = new PageModel { Payload = Payload };

            // The same template exercises the auto-encoding unnamed sink and the @raw opt-out island.
            const string profileTemplate =
                "<p>encoded: @(Payload)</p>\n<p>raw island: @raw(Payload)</p>\n";
            // The custom-encoder render uses the tagging extension (under Text so only our transform applies).
            const string customTemplate = "<p>tagged: @tagged(Payload)</p>\n";

            // The HED2004 HTML-context lint: a bare @(value) inside an attribute value under the Html profile
            // warns (suggesting @attr) yet renders byte-identically — the lint never changes output.
            const string lintTemplate = "<a title=\"@(Payload)\">link</a>\n";

            var text = Render(profileTemplate, model, OutputProfile.Text);
            var html = Render(profileTemplate, model, OutputProfile.Html);
            var custom = Render(customTemplate, model, OutputProfile.Text);
            // The modern seam: a pluggable TextEncoder applied at every encode site under the Html profile. Unlike the
            // built-in WebUtility path (profile-html.html), HtmlEncoder.Create(UnicodeRanges.All) leaves ü/ß/© as
            // literal Unicode and encodes the apostrophe — no per-extension wiring, and @raw still opts out.
            var optionsEncoder = Render(profileTemplate, model, OutputProfile.Html, HtmlEncoder.Create(UnicodeRanges.All));
            var contextLint = RenderWithContextLint(lintTemplate, model);

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                SampleCapture.Write(capture, "profile-text.html", text);
                SampleCapture.Write(capture, "profile-html.html", html);
                SampleCapture.Write(capture, "custom-encoder.html", custom);
                SampleCapture.Write(capture, "options-encoder.html", optionsEncoder);
                SampleCapture.Write(capture, "context-lint.html", contextLint);
                Console.WriteLine("captured profile-text.html, profile-html.html, custom-encoder.html, options-encoder.html, context-lint.html");
                return 0;
            }

            Console.WriteLine("=== OutputProfile.Text (1.x compatibility) — no encoding ===\n" + text);
            Console.WriteLine("=== OutputProfile.Html (default) — auto-encoded (WebUtility), @raw opts out ===\n" + html);
            Console.WriteLine("=== @tagged extension (the 1.x custom-encoder seam) ===\n" + custom);
            Console.WriteLine("=== TemplateOptions.Encoder (the modern seam — HtmlEncoder.Create(UnicodeRanges.All)) ===\n" + optionsEncoder);
            Console.WriteLine("=== HED2004 context lint — a bare @(value) in an attribute warns, bytes unchanged ===\n" + contextLint);
            return 0;
        }

        // The HED2004 demo render: compiles the attribute-position template under Html, asserts the compile
        // surfaces exactly one HED2004 (MissingContextEncoder) warning, and returns the (unchanged) bytes.
        private static string RenderWithContextLint(string template, PageModel model)
        {
            var options = new TemplateOptions { OutputProfile = OutputProfile.Html };
            using var t = new HeddleTemplate(template, new CompileContext(options, typeof(PageModel)));
            if (!t.CompileResult.Success)
                throw new InvalidOperationException("compile failed: " + t.CompileResult);
            int lints = 0;
            foreach (var warning in t.Context.CompileWarnings)
            {
                if (warning.DiagnosticId == HeddleDiagnosticIds.MissingContextEncoder)
                {
                    lints++;
                    Console.WriteLine("HED2004 surfaced: " + warning.Error + " Fix: " + warning.Fix);
                }
            }

            if (lints != 1)
                throw new InvalidOperationException($"expected exactly one HED2004 warning, got {lints}");
            return t.Generate(model);
        }

        private static string Render(string template, PageModel model, OutputProfile profile, TextEncoder encoder = null)
        {
            var options = new TemplateOptions { OutputProfile = profile, Encoder = encoder };
            using var t = new HeddleTemplate(template, new CompileContext(options, typeof(PageModel)));
            if (!t.CompileResult.Success)
                throw new InvalidOperationException("compile failed: " + t.CompileResult);
            return t.Generate(model);
        }
    }
}
