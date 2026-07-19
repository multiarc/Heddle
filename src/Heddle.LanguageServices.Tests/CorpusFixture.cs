using System;
using System.Collections.Generic;
using System.IO;
using Heddle.LanguageServices;

namespace Heddle.LanguageServices.Tests
{
    /// <summary>
    /// Shared helpers for the facade suite: the corpus model/export assembly path (a separate project copied into
    /// the test output but not referenced, so the facade loads it fresh via its ALC / one-shot scan) and a
    /// cursor-marker template parser.
    /// </summary>
    internal static class CorpusFixture
    {
        internal const char Cursor = '§'; // §

        internal static string ModelAssemblyPath =>
            Path.Combine(AppContext.BaseDirectory, "Heddle.LanguageServices.Tests.Corpus.dll");

        internal static HeddleLanguageService NewTypedService(string rootPath = null)
        {
            var options = new HeddleLanguageServiceOptions
            {
                AssemblyPaths = new List<string> { ModelAssemblyPath },
                RootPath = rootPath
            };
            return new HeddleLanguageService(options);
        }

        internal static HeddleLanguageService NewTypelessService(string rootPath = null)
        {
            return new HeddleLanguageService(new HeddleLanguageServiceOptions { RootPath = rootPath });
        }

        /// <summary>Splits a template carrying a single <c>§</c> cursor marker into (text, offset).</summary>
        internal static (string text, int offset) At(string markedTemplate)
        {
            int offset = markedTemplate.IndexOf(Cursor);
            if (offset < 0)
                throw new ArgumentException("template has no § cursor marker");
            return (markedTemplate.Remove(offset, 1), offset);
        }

        internal static IReadOnlyList<string> Labels(CompletionResult result)
        {
            var labels = new List<string>();
            foreach (var item in result.Items)
                labels.Add(item.Label);
            return labels;
        }
    }
}
