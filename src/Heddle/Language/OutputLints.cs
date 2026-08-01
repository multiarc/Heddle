using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Language
{
    /// <summary>
    /// The two warning-only document scans that read nothing but the working document, the parse spans and the
    /// effective output profile: the misread-brace lint and the HTML-context encoder lint. Neither needs a runtime
    /// type, an extension instance or an expression tree, which is why both tiers can run them and why they live
    /// here rather than in either compiler.
    /// <para>Both are strictly advisory — they never change a byte and never refuse a template.</para>
    /// </summary>
    internal static class OutputLints
    {
        /// <summary>
        /// Matches Liquid/Jinja style braces around a single ASCII identifier or dotted path.
        /// </summary>
        private static readonly Regex BraceMisreadRegex =
            new Regex(
                @"\{\{[ \t]*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)[ \t]*\}\}",
                RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Warns when <c>{{ … }}</c> in text renders literal braces instead of interpolating.
        /// Skips matches inside exclusion spans. Warning-only; never blocks compilation or changes bytes.
        /// </summary>
        internal static void ScanBraceMisreads(ParseContext parseContext,
            ICollection<HeddleCompileWarning> warnings, string workingDocument)
        {
            static bool Contains(BlockPosition b, int i) => i >= b.StartIndex && i < b.StartIndex + b.Length;

            foreach (Match m in BraceMisreadRegex.Matches(workingDocument))
            {
                int at = m.Index;
                bool excluded = false;
                foreach (var chain in parseContext.OutputChains)
                {
                    if (Contains(chain.BlockPosition, at)) { excluded = true; break; }
                }

                if (!excluded)
                {
                    foreach (var raw in parseContext.RawOutputItems)
                    {
                        if (Contains(raw.BlockPosition, at)) { excluded = true; break; }
                    }
                }

                if (!excluded)
                {
                    foreach (var definition in parseContext.DefinitionsBlock.Positions)
                    {
                        if (Contains(definition, at)) { excluded = true; break; }
                    }
                }

                if (excluded)
                    continue;

                var path = m.Groups[1].Value;
                warnings.Add(new HeddleCompileWarning
                {
                    Error = $"'{{{{ {path} }}}}' in text renders literal braces — '{{{{ … }}}}' is a subtemplate body, not interpolation, so the value of '{path}' is not printed.",
                    Fix = $"To output the value, use '@({path})'.",
                    Position = new BlockPosition(parseContext.AbsoluteOffset + at, 2),
                    DiagnosticId = HeddleDiagnosticIds.LiquidStyleInterpolationMisread
                });
            }
        }

        /// <summary>Classification of bare <c>@(value)</c> block position for the HTML-context lint.</summary>
        private enum HtmlContext
        {
            None,
            Attribute,
            Script,
            Url
        }

        /// <summary>Attributes carrying URL values.</summary>
        private static readonly HashSet<string> UrlAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "href", "src", "action", "formaction", "cite", "poster", "background", "manifest",
            "data", "longdesc", "usemap", "srcset"
        };

        /// <summary>
        /// Warning-only lint for bare <c>@(value)</c> blocks under the Html profile; classifies surrounding HTML
        /// position. <paramref name="isHtmlProfile"/> is the profile in force where the chain sits, after any
        /// <c>@profile()</c> flip earlier in the document.
        /// </summary>
        internal static void ScanHtmlContextLint(OutputChain chain, List<BlockPosition> leftSpans,
            ICollection<HeddleCompileWarning> warnings, string workingDocument, bool isHtmlProfile)
        {
            if (!isHtmlProfile)
                return; // the explicit Html profile is the sole gate; cheapest check first.

            var leftmost = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0] : null;
            if (leftmost == null)
                return;
            // Only bodiless unnamed carriers warn; named encoders and bodied calls never do.
            if (leftmost.ExtensionName.Length != 0 || !string.IsNullOrEmpty(leftmost.ParameterTemplate))
                return;

            var context = ClassifyHtmlContext(workingDocument, chain.BlockPosition.StartIndex, leftSpans);
            if (context == HtmlContext.None)
                return;

            string error, fix;
            switch (context)
            {
                case HtmlContext.Script:
                    error = "A bare '@(...)' output is inside a <script> block under the Html profile; HTML element-text encoding is wrong for a JavaScript context.";
                    fix = "Use '@js(...)' for the JavaScript-string context, or '@raw(...)' if the value is trusted.";
                    break;
                case HtmlContext.Url:
                    error = "A bare '@(...)' output is in a URL component under the Html profile; element-text encoding does not percent-encode it.";
                    fix = "Use '@url(...)' for the URL-component context, or '@raw(...)' if the value is trusted.";
                    break;
                default:
                    error = "A bare '@(...)' output is inside an HTML tag under the Html profile (attribute value or an unquoted/name position); element-text encoding is insufficient there.";
                    fix = "Use '@attr(...)' for the attribute context, or '@raw(...)' if the value is trusted.";
                    break;
            }

            warnings.Add(new HeddleCompileWarning
            {
                Error = error,
                Fix = fix,
                Position = leftmost.Position, // original-source coordinates for reporting.
                DiagnosticId = HeddleDiagnosticIds.MissingContextEncoder
            });
        }

        /// <summary>
        /// Left-only literal heuristic (earlier blocks excised): Step 1 check <c>&lt;script&gt;</c> containment.
        /// Step 2 find nearest tag boundary. Step 3 detect quote parity and URL attribute.
        /// </summary>
        private static HtmlContext ClassifyHtmlContext(string workingDocument, int blockStart,
            List<BlockPosition> leftSpans)
        {
            if (blockStart < 0 || blockStart > workingDocument.Length)
                return HtmlContext.None; // bounds discipline — a shifted position overshot; never dereference.

            var left = BuildLiteralLeft(workingDocument, blockStart, leftSpans);

            // Step 1: <script>-element containment.
            int open = -1;
            for (int m = 0; (m = left.IndexOf("<script", m, StringComparison.OrdinalIgnoreCase)) >= 0; m++)
            {
                int after = m + 7;
                if (after >= left.Length)
                    continue;  // Tag name interrupted.
                char c = left[after];
                if (c != '/' && c != '>' && !char.IsWhiteSpace(c))
                    continue;  // Not a script tag (e.g., <script-loader>).
                if (!HasUnquotedGreaterThan(left, after))
                    continue;  // Start tag not yet closed.
                open = m;
            }

            if (open >= 0)
            {
                int close = -1;
                for (int m = 0; (m = left.IndexOf("</script", m, StringComparison.OrdinalIgnoreCase)) >= 0; m++)
                {
                    int after = m + 8;
                    if (after < left.Length)
                    {
                        char c = left[after];
                        if (c != '/' && c != '>' && !char.IsWhiteSpace(c))
                            continue; // </scriptx> is not an end tag; EOF is a valid boundary.
                    }

                    close = m;
                }

                if (close < 0 || close < open)
                    return HtmlContext.Script;
            }

            // Step 2: nearest tag boundary (unquoted '>' only counts as tag close).
            int tagStart = left.LastIndexOf('<');
            if (tagStart < 0 || HasUnquotedGreaterThan(left, tagStart + 1))
                return HtmlContext.None; // element text — the default element-text encoder is correct.

            // Step 3: classify by quote parity and attribute name.
            string tagText = left.Substring(tagStart);
            int doubleQuotes = CountChar(tagText, '"');
            int singleQuotes = CountChar(tagText, '\'');
            int quote;
            if (doubleQuotes % 2 == 1)
                quote = tagText.LastIndexOf('"');
            else if (singleQuotes % 2 == 1)
                quote = tagText.LastIndexOf('\'');
            else
                return HtmlContext.Attribute; // unquoted position or between attributes — generic in-tag signal.

            string valueSoFar = tagText.Substring(quote + 1);
            int i = quote - 1;
            while (i >= 0 && char.IsWhiteSpace(tagText[i]))
                i--;
            if (i < 0 || tagText[i] != '=')
                return HtmlContext.Attribute;
            i--;
            while (i >= 0 && char.IsWhiteSpace(tagText[i]))
                i--;
            int nameEnd = i;
            while (i >= 0 && IsAttributeNameChar(tagText[i]))
                i--;
            if (i == nameEnd)
                return HtmlContext.Attribute; // no identifier run — not a recognizable attribute value.

            string attributeName = tagText.Substring(i + 1, nameEnd - i);
            bool componentSignal = valueSoFar.IndexOf('?') >= 0 || valueSoFar.IndexOf('&') >= 0 ||
                                   valueSoFar.IndexOf('=') >= 0 ||
                                   (valueSoFar.Length > 0 && valueSoFar[valueSoFar.Length - 1] == '/');
            return UrlAttributes.Contains(attributeName) && componentSignal
                ? HtmlContext.Url
                : HtmlContext.Attribute;
        }

        /// <summary>
        /// Left text of <paramref name="blockStart"/> with earlier producing blocks' source spans excised.
        /// </summary>
        private static string BuildLiteralLeft(string workingDocument, int blockStart, List<BlockPosition> leftSpans)
        {
            if (leftSpans == null || leftSpans.Count == 0)
                return workingDocument.Substring(0, blockStart);

            var builder = new StringBuilder(blockStart);
            int position = 0;
            foreach (var span in leftSpans)
            {
                int start = span.StartIndex;
                int end = span.StartIndex + span.Length;
                if (start >= blockStart)
                    break;
                if (start < position)
                    continue; // defensive — spans are ascending and non-overlapping by construction.
                if (end > blockStart)
                    end = blockStart;
                builder.Append(workingDocument, position, start - position);
                position = end;
            }

            if (position < blockStart)
                builder.Append(workingDocument, position, blockStart - position);
            return builder.ToString();
        }

        /// <summary>
        /// Returns true if an unquoted <c>&gt;</c> exists from <paramref name="from"/> onward.
        /// </summary>
        private static bool HasUnquotedGreaterThan(string text, int from)
        {
            char quote = '\0';
            for (int i = from; i < text.Length; i++)
            {
                char c = text[i];
                if (quote != '\0')
                {
                    if (c == quote)
                        quote = '\0';
                }
                else if (c == '"' || c == '\'')
                {
                    quote = c;
                }
                else if (c == '>')
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountChar(string text, char c)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == c)
                    count++;
            }

            return count;
        }

        private static bool IsAttributeNameChar(char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
            c == ':' || c == '_' || c == '-';
    }
}
