using System.Collections.Generic;
using Heddle.Language;
using Heddle.LanguageServices;
using Heddle.Strings.Core;

namespace Heddle.LanguageServer
{
    /// <summary>
    /// Builds the delta-encoded semantic-token stream (phase 6 D17) from an analysis: a sparse projection over the
    /// TextMate base, one token type per <see cref="HeddleTokenType"/> (punctuation, C# spans, literals and parse
    /// errors are not emitted), plus the hidden-channel skipped spans as comments.
    /// </summary>
    internal static class SemanticTokensBuilder
    {
        // Legend indexes (wire values): property 0, function 1, keyword 2, operator 3, macro 4, comment 5.
        private const int Property = 0, Function = 1, Keyword = 2, Operator = 3, Macro = 4, Comment = 5;

        internal static readonly string[] TokenTypes =
            { "property", "function", "keyword", "operator", "macro", "comment" };

        private static int? Map(HeddleTokenType type)
        {
            switch (type)
            {
                case HeddleTokenType.Id: return Property;
                case HeddleTokenType.RootReference: return Keyword;
                case HeddleTokenType.MemberSelector: return Operator;
                case HeddleTokenType.Out: return Macro;
                case HeddleTokenType.CSharpStart: return Keyword;
                case HeddleTokenType.DefType: return Keyword;
                case HeddleTokenType.Delim: return Operator;
                case HeddleTokenType.DefStart: return Keyword;
                case HeddleTokenType.DefClose: return Keyword;
                case HeddleTokenType.Comment: return Comment;
                case HeddleTokenType.DefOutputOnEnd: return Keyword;
                case HeddleTokenType.Operator: return Operator;
                case HeddleTokenType.FunctionName: return Function;
                default: return null; // not emitted — TextMate keeps its lexical coloring
            }
        }

        internal static int[] Build(DocumentAnalysis analysis)
        {
            var emitted = new List<(int start, int length, int type)>();
            foreach (var token in analysis.Tokens)
            {
                var mapped = Map(token.HeddleTokenType);
                if (mapped.HasValue && token.Position.Length > 0)
                    emitted.Add((token.Position.StartIndex, token.Position.Length, mapped.Value));
            }

            foreach (var skipped in analysis.SkippedTokens)
            {
                if (skipped.Length > 0)
                    emitted.Add((skipped.StartIndex, skipped.Length, Comment));
            }

            emitted.Sort((a, b) => a.start.CompareTo(b.start));

            // Split multi-line spans at line boundaries (LSP tokens are single-line), then delta-encode.
            var lineTokens = new List<(int line, int character, int length, int type)>();
            foreach (var (start, length, type) in emitted)
            {
                int remaining = length;
                int cursor = start;
                while (remaining > 0)
                {
                    var (line, character) = analysis.Lines.OffsetToPosition(cursor);
                    int lineEnd = analysis.Lines.PositionToOffset(line + 1, 0);
                    int available = lineEnd > cursor ? lineEnd - cursor : remaining;
                    int take = available < remaining ? available : remaining;
                    if (take <= 0)
                        break;
                    lineTokens.Add((line, character, take, type));
                    cursor += take;
                    remaining -= take;
                }
            }

            var data = new List<int>(lineTokens.Count * 5);
            int prevLine = 0, prevChar = 0;
            foreach (var (line, character, length, type) in lineTokens)
            {
                int deltaLine = line - prevLine;
                int deltaChar = deltaLine == 0 ? character - prevChar : character;
                data.Add(deltaLine);
                data.Add(deltaChar);
                data.Add(length);
                data.Add(type);
                data.Add(0);
                prevLine = line;
                prevChar = character;
            }

            return data.ToArray();
        }
    }
}
