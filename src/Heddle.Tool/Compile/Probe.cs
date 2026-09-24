using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Heddle.Tool.Compile
{
    /// <summary>The design-time probe: a parse-only pass that collects each item's model spelling
    /// (<c>@model</c> directive or <c>ModelType</c> metadata) and writes <c>probe.json</c> with
    /// <c>stubs</c> (sanitized name, model spelling, generated namespace) and <c>unresolved</c>
    /// (spellings no image resolves). No engine compile, no image load beyond name resolution.</summary>
    internal static class Probe
    {
        internal sealed class Entry
        {
            internal string Key;
            internal string Sanitized;
            internal string ModelSpelling;
        }

        internal static void Run(CompileRequest request, IReadOnlyList<TemplateInput> templates,
            ImageLoadContext images, string generatedNamespace)
        {
            var stubs = new List<Entry>();
            var unresolved = new List<string>();
            var seenUnresolved = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var template in templates)
            {
                // Import-only rows serve the import map but declare no entry point.
                if (template.Item.IsImportOnly)
                    continue;
                string spelling = !string.IsNullOrEmpty(template.Item.ModelType)
                    ? template.Item.ModelType
                    : ScanModelDirective(template.Text);
                var entry = new Entry
                {
                    Key = template.Key,
                    Sanitized = SanitizeName.ForKey(template.Key),
                    ModelSpelling = spelling ?? string.Empty
                };
                stubs.Add(entry);
                if (!string.IsNullOrEmpty(spelling) &&
                    images.ResolveModelType(spelling, ScanUsingDirectives(template.Text)) == null &&
                    seenUnresolved.Add(spelling))
                    unresolved.Add(spelling);
            }

            // An import replays the imported text inside the importer, its model directive included: a
            // compiled template binds whatever model a library it imports declares, whether or not that
            // library is itself compiled. A library no compiled template reaches binds nothing at build time.
            foreach (var text in ImportedTexts(request, templates))
            {
                string spelling = ScanModelDirective(text);
                if (!string.IsNullOrEmpty(spelling) &&
                    images.ResolveModelType(spelling, ScanUsingDirectives(text)) == null &&
                    seenUnresolved.Add(spelling))
                    unresolved.Add(spelling);
            }

            var json = new StringBuilder();
            json.Append("{\"stubs\":[");
            for (int i = 0; i < stubs.Count; i++)
            {
                if (i > 0)
                    json.Append(',');
                json.Append("{\"key\":").Append(Quote(stubs[i].Key))
                    .Append(",\"name\":").Append(Quote(stubs[i].Sanitized))
                    .Append(",\"model\":").Append(Quote(stubs[i].ModelSpelling))
                    .Append(",\"namespace\":").Append(Quote(generatedNamespace ?? string.Empty))
                    .Append('}');
            }

            json.Append("],\"unresolved\":[");
            for (int i = 0; i < unresolved.Count; i++)
            {
                if (i > 0)
                    json.Append(',');
                json.Append(Quote(unresolved[i]));
            }

            json.Append("]}");
            string directory = Path.GetDirectoryName(request.Probe);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(request.Probe, json.ToString(), new UTF8Encoding(false));
        }

        /// <summary>Reads the first <c>@model(){{Spelling}}</c> directive's spelling with balanced
        /// delimiters (generic spellings nest <c>&lt;&gt;</c> and <c>{{ }}</c> does not nest the
        /// directive). Returns null when no well-formed directive leads; the real compile — which
        /// parses directives authoritatively — is the backstop for anything fancier.</summary>
        internal static string ScanModelDirective(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            int at = 0;
            while (true)
            {
                at = text.IndexOf("@model", at, System.StringComparison.Ordinal);
                if (at < 0)
                    return null;
                int i = at + "@model".Length;
                i = SkipWhitespace(text, i);
                if (i < text.Length && text[i] == '(')
                {
                    int close = MatchBalanced(text, i, '(', ')');
                    if (close > i)
                    {
                        int j = SkipWhitespace(text, close + 1);
                        if (j + 1 < text.Length && text[j] == '{' && text[j + 1] == '{')
                        {
                            int end = text.IndexOf("}}", j + 2, System.StringComparison.Ordinal);
                            if (end > j)
                            {
                                string spelling = text.Substring(j + 2, end - j - 2).Trim();
                                return spelling.Length == 0 ? null : spelling;
                            }
                        }
                    }
                }

                at += "@model".Length;
            }
        }

        /// <summary>The text of everything the compiled templates import, transitively — item rows by key or
        /// registered name, then the template root on disk, as the compile itself resolves a spelling.</summary>
        private static List<string> ImportedTexts(CompileRequest request, IReadOnlyList<TemplateInput> templates)
        {
            var byName = new Dictionary<string, TemplateInput>(System.StringComparer.Ordinal);
            foreach (var template in templates)
            {
                if (template.Key != null && !byName.ContainsKey(template.Key))
                    byName.Add(template.Key, template);
                if (!string.IsNullOrEmpty(template.Item.Name) &&
                    Heddle.Precompiled.TemplateKey.TryNormalize(template.Item.Name, out string alias) &&
                    !byName.ContainsKey(alias))
                    byName.Add(alias, template);
            }

            var texts = new List<string>();
            var visited = new HashSet<string>(System.StringComparer.Ordinal);
            var pending = new Queue<string>();
            foreach (var template in templates)
                if (!template.Item.IsImportOnly)
                    pending.Enqueue(template.Text);
            while (pending.Count > 0)
            {
                foreach (string spelling in ScanImports(pending.Dequeue()))
                {
                    if (!visited.Add(spelling))
                        continue;
                    string text = null;
                    if (Heddle.Precompiled.TemplateKey.TryNormalize(spelling, out string key) &&
                        byName.TryGetValue(key, out var row))
                    {
                        text = row.Text;
                    }
                    else
                    {
                        try
                        {
                            string path = Path.Combine(request.Root ?? string.Empty, spelling);
                            if (File.Exists(path))
                                text = File.ReadAllText(path);
                        }
                        catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException ||
                            ex is System.ArgumentException || ex is System.NotSupportedException)
                        {
                            // The real compile reports an import it cannot read.
                        }
                    }

                    if (text == null)
                        continue;
                    texts.Add(text);
                    pending.Enqueue(text);
                }
            }

            return texts;
        }

        /// <summary>Every <c>@&lt;&lt;{{path}}</c> import spelling in the text, in order.</summary>
        internal static List<string> ScanImports(string text)
        {
            var imports = new List<string>();
            if (string.IsNullOrEmpty(text))
                return imports;
            int at = 0;
            while ((at = text.IndexOf("@<<", at, System.StringComparison.Ordinal)) >= 0)
            {
                int j = SkipWhitespace(text, at + 3);
                if (j + 1 < text.Length && text[j] == '{' && text[j + 1] == '{')
                {
                    int end = text.IndexOf("}}", j + 2, System.StringComparison.Ordinal);
                    if (end > j)
                    {
                        string spelling = text.Substring(j + 2, end - j - 2);
                        if (spelling.Length != 0)
                            imports.Add(spelling);
                    }
                }

                at += 3;
            }

            return imports;
        }

        /// <summary>Every <c>@using(){{Namespace}}</c> directive spelling in the text, in order — the imports
        /// the engine resolves a bare <c>@model</c> spelling against (HED7032 needs the same answer).</summary>
        internal static System.Collections.Generic.List<string> ScanUsingDirectives(string text)
        {
            var usings = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(text))
                return usings;
            int at = 0;
            while (true)
            {
                at = text.IndexOf("@using", at, System.StringComparison.Ordinal);
                if (at < 0)
                    return usings;
                int i = SkipWhitespace(text, at + "@using".Length);
                if (i < text.Length && text[i] == '(')
                {
                    int close = MatchBalanced(text, i, '(', ')');
                    if (close > i)
                    {
                        int j = SkipWhitespace(text, close + 1);
                        if (j + 1 < text.Length && text[j] == '{' && text[j + 1] == '{')
                        {
                            int end = text.IndexOf("}}", j + 2, System.StringComparison.Ordinal);
                            if (end > j)
                            {
                                string spelling = text.Substring(j + 2, end - j - 2).Trim();
                                if (spelling.Length != 0)
                                    usings.Add(spelling);
                            }
                        }
                    }
                }
                at += "@using".Length;
            }
        }

        private static int SkipWhitespace(string text, int i)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            return i;
        }

        private static int MatchBalanced(string text, int open, char openChar, char closeChar)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == openChar)
                    depth++;
                else if (text[i] == closeChar && --depth == 0)
                    return i;
            }

            return -1;
        }

        private static string Quote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }
    }
}
