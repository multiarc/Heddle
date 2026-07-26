using System.Collections.Generic;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Item classes completion produces; the protocol layer maps each to the numeric LSP
    /// <c>CompletionItemKind</c> (Property → 10, Definition → 7, Extension → 3,
    /// Function → 3, Prop → 5, Keyword → 14).
    /// </summary>
    public enum CompletionItemKind
    {
        Property,
        Definition,
        Extension,
        Function,
        Prop,
        Keyword
    }

    /// <summary>One completion item: a label, an optional detail line, and the text to insert.</summary>
    public sealed class CompletionItem
    {
        internal CompletionItem(string label, CompletionItemKind kind, string detail, string insertText)
        {
            Label = label;
            Kind = kind;
            Detail = detail;
            InsertText = insertText;
        }

        public string Label { get; }
        public CompletionItemKind Kind { get; }

        /// <summary>CLR type, header/overload signature, prop declaration, or "varies by call site"; null
        /// for keyword items.</summary>
        public string Detail { get; }

        /// <summary>Text to insert; equals <see cref="Label"/> for every kind except Prop, which inserts
        /// "name: " with the trailing space.</summary>
        public string InsertText { get; }
    }

    /// <summary>
    /// Completion outcome for one offset. Immutable; items are fully materialized — no resolve
    /// round-trip.
    /// </summary>
    public sealed class CompletionResult
    {
        internal CompletionResult(IReadOnlyList<CompletionItem> items)
        {
            Items = items;
        }

        internal static readonly CompletionResult Empty = new CompletionResult(System.Array.Empty<CompletionItem>());

        /// <summary>Items for the offset; empty when the detected context yields none (the no-guessing rule).</summary>
        public IReadOnlyList<CompletionItem> Items { get; }
    }

    /// <summary>Hover information for a location.</summary>
    public sealed class HoverResult
    {
        internal HoverResult(string markdown, int offset, int length)
        {
            Markdown = markdown;
            Offset = offset;
            Length = length;
        }

        public string Markdown { get; }
        public int Offset { get; }
        public int Length { get; }
    }

    /// <summary>Go-to-definition target location.</summary>
    public sealed class DefinitionTarget
    {
        internal DefinitionTarget(string sourcePath, int offset, int length)
        {
            SourcePath = sourcePath;
            Offset = offset;
            Length = length;
        }

        public string SourcePath { get; }
        public int Offset { get; }
        public int Length { get; }
    }
}
