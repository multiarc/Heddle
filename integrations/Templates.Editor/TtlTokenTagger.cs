using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Data;
using Templates.Editor.Classification;
using Templates.Exceptions;
using Templates.Language;
using Templates.Runtime;

namespace Templates.Editor {
    internal sealed class TtlTokenTagger: ITagger<TtlTokenTag> {
        private ITextSnapshot _snapshot;
        private readonly List<ITagSpan<TtlTokenTag>> _tags;
        private readonly TtlTemplate _template;
        private readonly TemplateOptions _options;

        internal TtlTokenTagger(ITextBuffer buffer) {
            _snapshot = buffer.CurrentSnapshot;
            _tags = new List<ITagSpan<TtlTokenTag>>();
            _template = new TtlTemplate();
            _options = new TemplateOptions
            {
                ProvideLanguageFeatures = true,
                AllowCSharp = true
            };
            var compileResult = _template.TryCompilation(_snapshot.GetText(), _options);
            ParseCompileResult(compileResult);
        }

        public IEnumerable<ITagSpan<TtlTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_snapshot == spans[0].Snapshot)
                return _tags;
            _snapshot = spans[0].Snapshot;
            _tags.Clear();
            var compileResult = _template.TryCompilation(_snapshot.GetText(), _options);
            ParseCompileResult(compileResult);
            return _tags;
        }

        private void ParseCompileResult(TtlCompileResult parseErrors)
        {
            _tags.AddRange(
                parseErrors.ErrorList.Select(
                    m =>
                        new TagSpan<TtlTokenTag>(
                            new SnapshotSpan(_snapshot, m.Position.StartIndex < 0 ? 0 : m.Position.StartIndex,
                                m.Position.Length == 0 ? 1 : m.Position.Length),
                            new TtlTokenTag(new Token
                            {
                                Length = m.Position.Length == 0 ? 1 : m.Position.Length,
                                StartIndex = m.Position.StartIndex < 0 ? 0 : m.Position.StartIndex,
                                Error = m.Error,
                                Type = TtlTokenType.ParseError
                            }))));
            if (parseErrors.Context != null)
            {
                ParseContext(parseErrors.Context);
            }
        }

        private void ParseContext(ParseContext context)
        {
            _tags.AddRange(
                context.Tokens.Select(
                    m =>
                        new TagSpan<TtlTokenTag>(
                            new SnapshotSpan(_snapshot, m.Position.StartIndex < 0 ? 0 : m.Position.StartIndex,
                                m.Position.Length == 0 ? 1 : m.Position.Length),
                            new TtlTokenTag(new Token
                            {
                                Length = m.Position.Length == 0 ? 1 : m.Position.Length,
                                StartIndex = m.Position.StartIndex < 0 ? 0 : m.Position.StartIndex,
                                Type = m.TtlTokenType
                            }))));
            foreach (var subContext in context.SubContexts)
            {
                ParseContext(subContext);
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}