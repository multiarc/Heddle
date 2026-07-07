using System;
using System.Globalization;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Samples.HtmlSafeOutput
{
    // Sample 5 — the Html output profile and the 2.0 migration rehearsal. One template carrying a hostile model
    // string renders under BOTH profile defaults (Text = raw, Html = auto-encoded, with a @raw opt-out island),
    // and a third render swaps in a custom encoder through the extension seam. When the 2.0 window flips the
    // default to Html, this sample's before/after IS the migration.
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

            var text = Render(profileTemplate, model, OutputProfile.Text);
            var html = Render(profileTemplate, model, OutputProfile.Html);
            var custom = Render(customTemplate, model, OutputProfile.Text);

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                SampleCapture.Write(capture, "profile-text.html", text);
                SampleCapture.Write(capture, "profile-html.html", html);
                SampleCapture.Write(capture, "custom-encoder.html", custom);
                Console.WriteLine("captured profile-text.html, profile-html.html, custom-encoder.html");
                return 0;
            }

            Console.WriteLine("=== OutputProfile.Text (1.x default) — no encoding ===\n" + text);
            Console.WriteLine("=== OutputProfile.Html (2.0 default) — auto-encoded, @raw opts out ===\n" + html);
            Console.WriteLine("=== custom encoder (tagging stub via the extension seam) ===\n" + custom);
            return 0;
        }

        private static string Render(string template, PageModel model, OutputProfile profile)
        {
            var options = new TemplateOptions { OutputProfile = profile };
            using var t = new HeddleTemplate(template, new CompileContext(options, typeof(PageModel)));
            if (!t.CompileResult.Success)
                throw new InvalidOperationException("compile failed: " + t.CompileResult);
            return t.Generate(model);
        }
    }
}
