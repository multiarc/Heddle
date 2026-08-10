using System.Collections.Generic;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>The golden offset→position vectors for the canonical line rule:
    /// LF, CRLF, a CRLF blank line, a <c>"\n\r"</c> sequence, a lone <c>'\r'</c>, a mixed document, one without a
    /// trailing newline, and the empty document — with one expected 1-based line and 0-based column per offset
    /// from 0 to <c>Length</c> inclusive.</para>
    /// <para>Plain data with no Heddle dependency, so the three surfaces that must agree can each assert it from
    /// their own suite: <c>Heddle.Tests</c> (the shared <c>LineIndex</c> and <c>HeddleCompileResult</c>),
    /// <c>Heddle.Generator.Tests</c> (the generator's <c>LineMapper</c>) and
    /// <c>Heddle.LanguageServices.Tests</c> (the LSP's <c>LineMap</c>) — the file is linked into the latter two.
    /// One table, three consumers; a rule change cannot be made to look right on one surface only.</para>
    /// </summary>
    internal static class LineIndexVectors
    {
        internal sealed class Document
        {
            public Document(string name, string text, int[] lines, int[] columns)
            {
                Name = name;
                Text = text;
                Lines = lines;
                Columns = columns;
            }

            /// <summary>Vector name, used as the xunit theory label.</summary>
            public string Name { get; }

            public string Text { get; }

            /// <summary>Expected 1-based line per offset (index = offset).</summary>
            public int[] Lines { get; }

            /// <summary>Expected 0-based column per offset (index = offset).</summary>
            public int[] Columns { get; }
        }

        /// <summary>One row per (document, offset): name, text, offset, 1-based line, 0-based column.</summary>
        internal static IEnumerable<object[]> Rows()
        {
            foreach (var document in Documents)
                for (var offset = 0; offset < document.Lines.Length; offset++)
                    yield return new object[]
                    {
                        document.Name, document.Text, offset, document.Lines[offset], document.Columns[offset]
                    };
        }

        internal static IEnumerable<object[]> Names()
        {
            foreach (var document in Documents)
                yield return new object[] { document.Name };
        }

        internal static Document ByName(string name)
        {
            foreach (var document in Documents)
                if (document.Name == name)
                    return document;
            return null;
        }

        internal static readonly Document[] Documents =
        {
            new Document("empty", "",
                new[] { 1 },
                new[] { 0 }),
            new Document("no-eol", "abc",
                new[] { 1, 1, 1, 1 },
                new[] { 0, 1, 2, 3 }),
            new Document("lf", "a\nb\nc",
                new[] { 1, 1, 2, 2, 3, 3 },
                new[] { 0, 1, 0, 1, 0, 1 }),
            new Document("crlf", "a\r\nb\r\nc",
                new[] { 1, 1, 1, 2, 2, 2, 3, 3 },
                new[] { 0, 1, 2, 0, 1, 2, 0, 1 }),
            // The '\r' at offset 3 opens the blank line's terminator; 'b' at offset 5 starts line 3.
            new Document("crlf-blank-line", "a\r\n\r\nb",
                new[] { 1, 1, 1, 2, 2, 3, 3 },
                new[] { 0, 1, 2, 0, 1, 0, 1 }),
            // A '\r' that follows (rather than precedes) a '\n' is ordinary content on the new line.
            new Document("newline-then-cr", "a\n\rb",
                new[] { 1, 1, 2, 2, 2 },
                new[] { 0, 1, 0, 1, 2 }),
            // A lone '\r' is not a terminator: the whole document is one line.
            new Document("lone-cr", "a\rb",
                new[] { 1, 1, 1, 1 },
                new[] { 0, 1, 2, 3 }),
            new Document("mixed", "a\nb\r\n\rc\n",
                new[] { 1, 1, 2, 2, 2, 3, 3, 3, 4 },
                new[] { 0, 1, 0, 1, 2, 0, 1, 2, 0 })
        };
    }
}
