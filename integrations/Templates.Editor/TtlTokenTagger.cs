using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Editor.Classification;
using Templates.Editor.Error;
using Templates.Exceptions;
using Templates.Language;

namespace Templates.Editor {
    internal sealed class TtlTokenTagger: ITagger<TtlTokenTag> {
        private ITextSnapshot _snapshot;
        private readonly List<ITagSpan<TtlTokenTag>> _tags;

        internal TtlTokenTagger(ITextBuffer buffer) {
            _snapshot = buffer.CurrentSnapshot;
            _tags = new List<ITagSpan<TtlTokenTag>>();
            GetTags();
        }

        public IEnumerable<ITagSpan<TtlTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (_snapshot == spans[0].Snapshot)
                return _tags;
            _snapshot = spans[0].Snapshot;
            _tags.Clear();
            GetTags();
            return _tags;
        }

        private void GetTags()
        {
            try
            {
                var context = DocumentParser.Parse(_snapshot.GetText());
                _tags.AddRange(context.Tokens.Select(m => new TagSpan<TtlTokenTag>(new SnapshotSpan(_snapshot, m.Position.StartIndex, m.Position.Length), new TtlTokenTag(new Token
                {
                    Length = m.Position.Length == 0 ? 1 : m.Position.Length,
                    StartIndex = m.Position.StartIndex,
                    Type = m.TtlTokenType
                }))));
                _tags.AddRange(context.Errors.Select(m => new TagSpan<TtlTokenTag>(new SnapshotSpan(_snapshot, m.Position.StartIndex, m.Position.Length), new TtlTokenTag(new Token
                {
                    Length = m.Position.Length == 0 ? 1 : m.Position.Length,
                    StartIndex = m.Position.StartIndex,
                    Error = m.Error,
                    Type = TtlTokenType.ParseError
                }))));
            }
            catch (TemplateParseException e)
            {
                _tags.AddRange(e.Errors.Select(m => new TagSpan<TtlTokenTag>(new SnapshotSpan(_snapshot, m.Position.StartIndex, m.Position.Length), new TtlTokenTag(new Token
                {
                    Length = m.Position.Length == 0 ? 1 : m.Position.Length,
                    StartIndex = m.Position.StartIndex,
                    Error = m.Error,
                    Type = TtlTokenType.ParseError
                }))));
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
