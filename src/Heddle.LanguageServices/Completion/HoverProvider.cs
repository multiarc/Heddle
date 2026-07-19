using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;

namespace Heddle.LanguageServices.Completion
{
    /// <summary>Hover content (phase 6 D15): a fenced <c>csharp</c> signature line plus plain paragraphs.</summary>
    internal static class HoverProvider
    {
        internal static HoverResult GetHover(DocumentAnalysis analysis, int offset, FunctionRegistry functions)
        {
            var (word, start, length) = WordAt(analysis.Text, offset);
            if (string.IsNullOrEmpty(word))
                return null;
            var registry = functions ?? FunctionRegistry.Default;

            // Definition name.
            var definition = analysis.Definitions.FirstOrDefault(d => d.Name == word);
            if (definition != null)
                return new HoverResult(Fence(DefinitionSignature(definition)), start, length);

            // Prop of any definition.
            foreach (var def in analysis.Definitions)
            {
                var prop = def.Props.FirstOrDefault(p => p.Name == word);
                if (prop != null)
                {
                    var decl = prop.IsRequired ? $"{prop.Name}: {prop.TypeName} (required)"
                        : $"{prop.Name}: {prop.TypeName}";
                    return new HoverResult(Fence(decl) + $"\n\ndeclared on `<{def.Name}>`", start, length);
                }
            }

            // Phase 7 (WI5): a named content region of any definition — show visibility and type.
            foreach (var def in analysis.Definitions)
            {
                var region = def.Regions.FirstOrDefault(r => r.Name == word);
                if (region != null)
                {
                    var visibility = region.IsPublic ? "public region" : "private region";
                    var typed = string.IsNullOrEmpty(region.TypeName) || region.TypeName == "object"
                        ? string.Empty
                        : $" :: {region.TypeName}";
                    var declared = region.IsPublic ? $"<:{region.Name}>{typed}" : $"<{region.Name}>{typed}";
                    return new HoverResult(Fence(declared) + $"\n\n{visibility} of `<{def.Name}>`", start, length);
                }
            }

            // Member of the innermost scope model set.
            var types = analysis.Scopes.GetModelTypesAt(offset);
            var memberHover = MemberHover(word, types);
            if (memberHover != null)
                return new HoverResult(memberHover, start, length);

            // Registered function.
            var overloads = registry.EnumerateOverloads().Where(o => o.Name == word).ToList();
            if (overloads.Count > 0)
            {
                var body = string.Join("\n", overloads.Select(o => "```csharp\n" + CompletionProvider.FormatSignature(o) + "\n```"));
                return new HoverResult(body, start, length);
            }

            // Extension name.
            if (TemplateFactory.RegisteredNames().Contains(word))
                return new HoverResult($"extension `{word}`", start, length);

            return null;
        }

        private static string MemberHover(string word, IReadOnlyList<ExType> types)
        {
            if (types == null || types.Count == 0)
                return null;
            var lines = new List<string>();
            foreach (var type in types)
            {
                if (type == null || type.IsDynamic)
                    continue;
                var property = MemberPathResolver.GetVisibleProperties(type.Type)
                    .FirstOrDefault(p => p.Name == word);
                if (property != null)
                    lines.Add($"{type}.{word} : {CompletionProvider.Friendly(property.PropertyType)}");
            }

            if (lines.Count == 0)
                return null;
            if (lines.Count == 1)
                return Fence(lines[0]);
            // Abstract body with differing per-site types (D13).
            return Fence("varies by call site") + "\n\n" + string.Join("\n", lines.Select(l => "- " + l));
        }

        internal static string DefinitionSignature(DefinitionInfo definition)
        {
            var props = definition.Props.Count == 0
                ? string.Empty
                : "(" + string.Join(", ", definition.Props.Select(p =>
                    p.IsRequired ? $"{p.Name}: {p.TypeName}" : $"{p.Name}: {p.TypeName} = {FormatDefault(p.DefaultValue)}")) + ")";
            if (definition.IsPinned)
                return $"<{definition.Name}{props}> :: {definition.ModelTypeName}";
            return $"<{definition.Name}{props}> — abstract";
        }

        private static string Fence(string signature)
        {
            return "```csharp\n" + signature + "\n```";
        }

        private static string FormatDefault(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + s + "\"";
            if (value is bool b) return b ? "true" : "false";
            return value.ToString();
        }

        internal static (string word, int start, int length) WordAt(string text, int offset)
        {
            if (string.IsNullOrEmpty(text) || offset < 0 || offset > text.Length)
                return (null, 0, 0);
            int start = offset;
            while (start > 0 && IsWordChar(text[start - 1]))
                start--;
            int end = offset;
            while (end < text.Length && IsWordChar(text[end]))
                end++;
            if (end <= start)
                return (null, 0, 0);
            return (text.Substring(start, end - start), start, end - start);
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
