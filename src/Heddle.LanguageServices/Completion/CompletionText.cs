using System.Text;

namespace Heddle.LanguageServices.Completion
{
    /// <summary>
    /// Repairs an in-progress buffer so the enclosing block parses (the engine's ANTLR parser is strict, but a
    /// user typing <c>@(</c>, <c>@(x.</c>, <c>@(::</c> has an incomplete construct). A placeholder identifier is
    /// inserted at the cursor and the parens opened in the innermost body are closed after it, so the surrounding
    /// <c>@list(...){{ … }}</c> body still compiles and records its narrowed model type — the offset is unchanged,
    /// so context detection and the scope-map query stay aligned with the cursor.
    /// </summary>
    internal static class CompletionText
    {
        internal const string Placeholder = "_hcp_";

        internal static (string text, int offset) Repair(string text, int offset)
        {
            if (text == null)
                return (string.Empty, 0);
            if (offset < 0) offset = 0;
            if (offset > text.Length) offset = text.Length;

            int bodyStart = InnermostBodyStart(text, offset);
            int depth = 0;
            for (int i = bodyStart; i < offset; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')') depth--;
            }

            if (depth < 0) depth = 0;

            // Discount any closing parens the cursor already sits in front of.
            int t = offset;
            while (t < text.Length && (text[t] == ' ' || text[t] == '\t'))
                t++;
            int trailing = 0;
            while (t < text.Length && text[t] == ')' && trailing < depth)
            {
                trailing++;
                t++;
            }

            int need = depth - trailing;
            var insert = new StringBuilder(NamedArgumentBody(text, offset, depth));
            for (int i = 0; i < need; i++)
                insert.Append(')');

            var repaired = text.Substring(0, offset) + insert + text.Substring(offset);
            return (repaired, offset);
        }

        /// <summary>
        /// Chooses the placeholder body: a bare identifier for an expression position, or a valid named argument
        /// (<c>_hcp_: 0</c>) when the cursor sits at a named-argument boundary of a named call — a trailing bare
        /// positional after named args would not parse and would drop every definition from the analysis.
        /// </summary>
        private static string NamedArgumentBody(string text, int offset, int depth)
        {
            if (depth <= 0)
                return Placeholder;

            // Find the innermost enclosing '(' and the call name before it.
            int d = 0, open = -1;
            for (int i = offset - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == '{' || c == '}') break;
                if (c == ')') d++;
                else if (c == '(')
                {
                    if (d == 0) { open = i; break; }
                    d--;
                }
            }

            if (open <= 0)
                return Placeholder;

            int nameStart = open;
            while (nameStart > 0 && (char.IsLetterOrDigit(text[nameStart - 1]) || text[nameStart - 1] == '_'))
                nameStart--;
            bool named = nameStart < open && nameStart > 0 && text[nameStart - 1] == '@';
            if (!named)
                return Placeholder; // '@(' expression position

            var region = text.Substring(open + 1, offset - open - 1);
            bool argBoundary = HasComma(region) || HasNamedArg(region);
            return argBoundary ? Placeholder + ": 0" : Placeholder;
        }

        private static bool HasComma(string region)
        {
            return region.IndexOf(',') >= 0;
        }

        private static bool HasNamedArg(string region)
        {
            for (int i = 0; i < region.Length; i++)
            {
                if (region[i] != ':')
                    continue;
                int j = i - 1;
                while (j >= 0 && region[j] == ' ') j--;
                if (j >= 0 && (char.IsLetterOrDigit(region[j]) || region[j] == '_'))
                    return true;
            }

            return false;
        }

        private static int InnermostBodyStart(string text, int offset)
        {
            var stack = new System.Collections.Generic.Stack<int>();
            for (int i = 0; i + 1 < offset; i++)
            {
                if (text[i] == '{' && text[i + 1] == '{')
                {
                    stack.Push(i + 2);
                    i++;
                }
                else if (text[i] == '}' && text[i + 1] == '}')
                {
                    if (stack.Count > 0) stack.Pop();
                    i++;
                }
            }

            return stack.Count > 0 ? stack.Peek() : 0;
        }
    }
}
