using System;
using System.Globalization;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Samples.CustomExtensions
{
    // Sample 6 — the public Scope.Publish/TryRead channel and the branch protocol as third-party extension APIs.
    // A publisher/reader pair round-trips a value through the channel; a custom @ifmiss participant reads the
    // BranchState an @if publishes and renders on the not-taken path — a third-party @else built on the public API.
    public sealed class ChannelModel
    {
        public string Tag { get; set; }
        public bool Show { get; set; }
    }

    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            HeddleTemplate.Configure(typeof(Program).Assembly);

            // Publisher writes model data under its own key; the reader (a sibling in the same body) retrieves it.
            const string channelTemplate = "published, then read back -> @stash(Tag)@recall()\n";
            var channel = Render(channelTemplate, new ChannelModel { Tag = "hello-channel" });

            // The custom @ifmiss reads the branch state @if publishes and fires only when @if did not.
            const string branchTemplate = "@if(Show){{visible}}@ifmiss(){{fallback}}\n";
            var branchTaken = Render(branchTemplate, new ChannelModel { Show = true });
            var branchMissed = Render(branchTemplate, new ChannelModel { Show = false });

            var capture = SampleCapture.Resolve(args);
            if (capture != null)
            {
                SampleCapture.Write(capture, "channel.txt", channel);
                SampleCapture.Write(capture, "branch.txt", "Show=true  -> " + branchTaken + "Show=false -> " + branchMissed);
                Console.WriteLine("captured channel.txt, branch.txt");
                return 0;
            }

            Console.Write("=== channel round-trip ===\n" + channel);
            Console.Write("=== branch participant ===\nShow=true  -> " + branchTaken + "Show=false -> " + branchMissed);
            return 0;
        }

        private static string Render(string template, ChannelModel model)
        {
            using var t = new HeddleTemplate(template, new CompileContext(new TemplateOptions(), typeof(ChannelModel)));
            if (!t.CompileResult.Success)
                throw new InvalidOperationException("compile failed: " + t.CompileResult);
            return t.Generate(model);
        }
    }
}
