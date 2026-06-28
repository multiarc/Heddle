using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Heddle.Data;
using Heddle.Editor.Classification;
using Heddle.Exceptions;
using Heddle.Language;
using Heddle.Runtime;

namespace Heddle.Editor {
    internal sealed class HeddleTokenTagger: ITagger<HeddleTokenTag> {
        private ITextSnapshot _snapshot;
        private readonly List<ITagSpan<HeddleTokenTag>> _tags;
        private readonly HeddleTemplate _template;
        private readonly TemplateOptions _options;

        internal HeddleTokenTagger(ITextBuffer buffer) {
            _snapshot = buffer.CurrentSnapshot;
            _tags = new List<ITagSpan<HeddleTokenTag>>();
            _template = new HeddleTemplate();
            _options = new TemplateOptions
            {
                ProvideLanguageFeatures = true,
                AllowCSharp = true
            };
            var compileResult = _template.TryCompilation(_snapshot.GetText(), _options);
            ParseCompileResult(compileResult);
        }

        public IEnumerable<ITagSpan<HeddleTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_snapshot == spans[0].Snapshot)
                return _tags;
            _snapshot = spans[0].Snapshot;
            _tags.Clear();
            var compileResult = _template.TryCompilation(_snapshot.GetText(), _options);
            ParseCompileResult(compileResult);
            return _tags;
        }

        private void ParseCompileResult(HeddleCompileResult parseErrors)
        {
            _tags.AddRange(
                parseErrors.ErrorList.Select(
                    m =>
                        new TagSpan<HeddleTokenTag>(
                            new SnapshotSpan(_snapshot, m.Position.StartIndex < 0 ? 0 : m.Position.StartIndex,
                                m.Position.Length == 0 ? 1 : m.Position.Length),
                            new HeddleTokenTag(new Token
                            {
                                Length = m.Position.Length == 0 ? 1 : m.Position.Length,
                                StartIndex = m.Position.StartIndex < 0 ? 0 : m.Position.StartIndex,
                                Error = m.Error,
                                Type = HeddleTokenType.ParseError
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
                        new TagSpan<HeddleTokenTag>(
                            new SnapshotSpan(_snapshot, m.Position.StartIndex < 0 ? 0 : m.Position.StartIndex,
                                m.Position.Length == 0 ? 1 : m.Position.Length),
                            new HeddleTokenTag(new Token
                            {
                                Length = m.Position.Length == 0 ? 1 : m.Position.Length,
                                StartIndex = m.Position.StartIndex < 0 ? 0 : m.Position.StartIndex,
                                Type = m.HeddleTokenType
                            }))));
            foreach (var subContext in context.SubContexts)
            {
                ParseContext(subContext);
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}