using System;

namespace Heddle.LanguageServices.Completion
{
    internal enum CompletionContextKind
    {
        None,
        CallableNames,
        ExpressionPosition,
        MemberOfPrefix,
        RootMembers,
        NamedArgument,

        /// <summary>Phase 7 (WI5): a call-body region-override position — <c>@feed(){{ @% &lt;│ %@ }}</c>.
        /// <see cref="CompletionContext.CallName"/> carries the enclosing call's name; the provider offers the
        /// callee's PUBLIC region names.</summary>
        RegionOverride
    }

    internal readonly struct CompletionContext
    {
        public CompletionContext(CompletionContextKind kind, string[] prefix, bool rootReference, string callName)
        {
            Kind = kind;
            Prefix = prefix;
            RootReference = rootReference;
            CallName = callName;
        }

        public CompletionContextKind Kind { get; }
        public string[] Prefix { get; }
        public bool RootReference { get; }
        public string CallName { get; }

        public static readonly CompletionContext None =
            new CompletionContext(CompletionContextKind.None, null, false, null);
    }

    /// <summary>
    /// Classifies the completion context from the text before the offset plus the innermost scope span (phase 6
    /// D12 detection table, first match wins). Text-driven so it stays robust on the incomplete input a user types.
    /// </summary>
    internal static class ContextDetector
    {
        internal static CompletionContext Detect(DocumentAnalysis analysis, int offset, out string[] passedProps)
        {
            passedProps = Array.Empty<string>();
            var text = analysis.Text;
            if (offset < 0) offset = 0;
            if (offset > text.Length) offset = text.Length;

            int i = offset - 1;
            while (i >= 0 && (text[i] == ' ' || text[i] == '\t'))
                i--;

            // Rule 3 — member of a resolved prefix (anchor '.').
            if (i >= 0 && text[i] == '.')
            {
                var (prefix, rootRef) = ReadPathBefore(text, i);
                return new CompletionContext(CompletionContextKind.MemberOfPrefix, prefix, rootRef, null);
            }

            // Rule 4 — root-model members (anchor '::').
            if (i >= 1 && text[i] == ':' && text[i - 1] == ':')
                return new CompletionContext(CompletionContextKind.RootMembers, null, true, null);

            // Rules 2/5 — inside call parens.
            if (TryFindEnclosingCall(text, offset, out var callName, out var openParen))
            {
                var defProps = FindDefinitionProps(analysis, callName);
                if (defProps != null && AtArgumentBoundary(text, offset, openParen, out passedProps))
                    return new CompletionContext(CompletionContextKind.NamedArgument, null, false, callName);
                return new CompletionContext(CompletionContextKind.ExpressionPosition, null, false, callName);
            }

            // Phase 7 (WI5) — region-override position: a '<' anchor (optionally mid-word) at an override
            // position of a definition block inside a call body ({{ … }} of a '@name(...)' call). Offers the
            // callee's public region names for the '<name:name>' override form; the '@%'/'}}' anchor guard keeps
            // plain HTML tags in body text from triggering it.
            {
                int w = i;
                while (w >= 0 && (char.IsLetterOrDigit(text[w]) || text[w] == '_'))
                    w--;
                if (w >= 0 && text[w] == '<' && CompletionText.PrecededByOverrideAnchor(text, w))
                {
                    var callee = FindEnclosingCallBodyName(text, w);
                    if (!string.IsNullOrEmpty(callee))
                        return new CompletionContext(CompletionContextKind.RegionOverride, null, false, callee);
                }
            }

            // Rule 6 — callable names after '@' or a chain ':'.
            if (i >= 0 && text[i] == '@')
                return new CompletionContext(CompletionContextKind.CallableNames, null, false, null);
            if (i >= 0 && text[i] == ':' && (i == 0 || text[i - 1] != ':'))
                return new CompletionContext(CompletionContextKind.CallableNames, null, false, null);

            return CompletionContext.None;
        }

        /// <summary>Phase 7 (WI5): the name of the call whose <c>{{ … }}</c> body encloses
        /// <paramref name="position"/>, or null. Scans backwards balancing <c>}}</c>/<c>{{</c> pairs; the
        /// unbalanced enclosing <c>{{</c> must follow a <c>@name(...)</c> call shape.</summary>
        private static string FindEnclosingCallBodyName(string text, int position)
        {
            int bodyDepth = 0;
            for (int i = position - 1; i >= 1; i--)
            {
                if (text[i] == '}' && text[i - 1] == '}')
                {
                    bodyDepth++;
                    i--;
                    continue;
                }

                if (text[i] == '{' && text[i - 1] == '{')
                {
                    if (bodyDepth > 0)
                    {
                        bodyDepth--;
                        i--;
                        continue;
                    }

                    // The enclosing body open. It must directly follow a '@name(...)' call (whitespace allowed).
                    int j = i - 2;
                    while (j >= 0 && (text[j] == ' ' || text[j] == '\t' || text[j] == '\r' || text[j] == '\n'))
                        j--;
                    if (j < 0 || text[j] != ')')
                        return null;
                    int parenDepth = 1;
                    j--;
                    while (j >= 0 && parenDepth > 0)
                    {
                        if (text[j] == ')') parenDepth++;
                        else if (text[j] == '(') parenDepth--;
                        j--;
                    }

                    if (parenDepth != 0)
                        return null;
                    var name = ReadNameBefore(text, j + 1, out bool sigil);
                    return sigil && name.Length != 0 ? name : null;
                }
            }

            return null;
        }

        private static (string[] segments, bool rootRef) ReadPathBefore(string text, int dotIndex)
        {
            int end = dotIndex; // exclusive of the '.'
            int start = end;
            while (start > 0)
            {
                char c = text[start - 1];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
                    start--;
                else
                    break;
            }

            bool rootRef = start >= 2 && text[start - 1] == ':' && text[start - 2] == ':';
            var raw = text.Substring(start, end - start);
            var segments = raw.Length == 0 ? Array.Empty<string>() : raw.Split('.');
            return (segments, rootRef);
        }

        private static bool TryFindEnclosingCall(string text, int offset, out string callName, out int openParen)
        {
            callName = null;
            openParen = -1;
            int depth = 0;
            for (int i = offset - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == '}' || c == '{')
                    return false; // do not scan across a body boundary
                if (c == ')')
                    depth++;
                else if (c == '(')
                {
                    if (depth == 0)
                    {
                        openParen = i;
                        callName = ReadNameBefore(text, i, out bool sigil);
                        return sigil; // must be a '@name(' or '@(' construct
                    }

                    depth--;
                }
            }

            return false;
        }

        private static string ReadNameBefore(string text, int parenIndex, out bool sigil)
        {
            int start = parenIndex;
            while (start > 0)
            {
                char c = text[start - 1];
                if (char.IsLetterOrDigit(c) || c == '_')
                    start--;
                else
                    break;
            }

            var name = text.Substring(start, parenIndex - start);
            sigil = start > 0 && text[start - 1] == '@';
            return name;
        }

        private static System.Collections.Generic.IReadOnlyList<PropInfo> FindDefinitionProps(
            DocumentAnalysis analysis, string callName)
        {
            if (string.IsNullOrEmpty(callName))
                return null;
            foreach (var definition in analysis.Definitions)
            {
                if (definition.Name == callName && definition.Props.Count > 0)
                    return definition.Props;
            }

            return null;
        }

        private static bool AtArgumentBoundary(string text, int offset, int openParen, out string[] passedProps)
        {
            var props = new System.Collections.Generic.List<string>();
            int i = offset - 1;
            while (i > openParen && (text[i] == ' ' || text[i] == '\t'))
                i--;
            bool boundary = i == openParen || (i > openParen && text[i] == ',');

            // Collect already-passed prop names ("name:") between the paren and the offset.
            var region = text.Substring(openParen + 1, Math.Max(0, offset - openParen - 1));
            foreach (var part in region.Split(','))
            {
                int colon = part.IndexOf(':');
                if (colon > 0)
                {
                    var name = part.Substring(0, colon).Trim();
                    if (name.Length > 0 && IsIdentifier(name))
                        props.Add(name);
                }
            }

            passedProps = props.ToArray();
            return boundary;
        }

        private static bool IsIdentifier(string s)
        {
            foreach (var c in s)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return s.Length > 0;
        }
    }
}
