using System;
using System.Text;
using Xunit;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>Completion is asked for on every keystroke, in documents of any size. Pins that its cost grows with
    /// the document and not with its square: a document twice as long may cost about twice as much, not four
    /// times. The bound is a ratio of two byte counts taken in one process, and only a change of growth order
    /// crosses it; the ceiling catches a cost that grew at every size alike.</summary>
    public class CompletionScalingTests
    {
        private const string Path = "doc.heddle";

        private static string Document(int blocks)
        {
            var text = new StringBuilder("@model(){{Corpus.Blog}}\n");
            for (int i = 0; i < blocks; i++)
                text.Append("<section><h2>@(Title)</h2>\n@list(Articles){{ <li>@(Title) by @(Author.Name)</li> }}\n</section>\n");
            return text.ToString();
        }

        private static long CompletionAllocatedBytes(HeddleLanguageService service, int blocks)
        {
            string text = Document(blocks) + "@()";
            int offset = text.Length - 1;
            service.Analyze(Path, text, blocks);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var result = service.GetCompletions(Path, offset);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Contains(result.Items, item => item.Label == "Title");
            return allocated;
        }

        [Fact]
        public void CompletionCostGrowsWithTheDocumentNotWithItsSquare()
        {
            using var service = CorpusFixture.NewTypedService();
            // Both sizes are run once before they are measured, so nothing a first run allocates is in either
            // figure. Allocation rather than time: a request re-analyses the document on the calling thread, the
            // work that once grew with the square was copying, and a byte count does not move with the load on
            // the machine or the number of its cores.
            CompletionAllocatedBytes(service, 120);
            CompletionAllocatedBytes(service, 240);
            long small = CompletionAllocatedBytes(service, 120);
            long large = CompletionAllocatedBytes(service, 240);

            // Twice the text, about 26 KB of it. Linear growth is a ratio near 2; quadratic is 4.
            Assert.True(large <= small * 3,
                $"completion allocated {small:N0} bytes over 120 blocks and {large:N0} over 240 — a ratio of {(double) large / small:F1}, where linear growth is about 2.");
            Assert.True(large <= CeilingBytes,
                $"completion over 240 blocks allocated {large:N0} bytes; the ceiling is {CeilingBytes:N0}.");
        }

        private const long CeilingBytes = 48L * 1024 * 1024;
    }
}
