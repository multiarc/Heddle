using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Templates.Data;
using Templates.Exceptions;
using Templates.Language;
using Templates.Runtime;
using Templates.Strings.Core;
using Microsoft.VisualStudio.Text.Classification;
using Templates.Editor.Error;

namespace Templates.Editor {
    internal sealed class TtlTokenTagger: ITagger<TtlTokenTag> {
        private ITextSnapshot _snapshot;
        private readonly SyntaxParser _parser = new SyntaxParser();
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

        private void GetTags() {
            var tokens = LexisParser.Tokenize(_snapshot.GetText());
            foreach (Token token in tokens) {
                try {
                    _parser.ParseNext(token);
                }
                catch (TemplateParseException e) {
                    var tokenSpan = new SnapshotSpan(_snapshot, new Span(token.StartIndex, token.Length));
                    _tags.Add(new TagSpan<TtlTokenTag>(tokenSpan,
                        new TtlTokenTag(token, State.SyntaxError,
                            new TtlTemplateErrorContainer(e, "Error parsing template"))));
                    _parser.ResetState();
                }
                catch (TemplateCompileException e) {
                    var tokenSpan = new SnapshotSpan(_snapshot, new Span(token.StartIndex, token.Length));
                    _tags.Add(new TagSpan<TtlTokenTag>(tokenSpan,
                        new TtlTokenTag(token, State.CompileError,
                            new TtlTemplateErrorContainer(e, "Error compiling template"))));
                    _parser.ResetState();
                }
                catch (ArgumentException e) {
                    var tokenSpan = new SnapshotSpan(_snapshot, new Span(token.StartIndex, token.Length));
                    _tags.Add(new TagSpan<TtlTokenTag>(tokenSpan,
                        new TtlTokenTag(token, State.CompileError,
                            new TtlTemplateErrorContainer(e, "Error compiling template"))));
                    _parser.ResetState();
                }
                catch (TemplateCreateException e) {
                    var tokenSpan = new SnapshotSpan(_snapshot, new Span(token.StartIndex, token.Length));
                    _tags.Add(new TagSpan<TtlTokenTag>(tokenSpan,
                        new TtlTokenTag(token, State.OtherError,
                            new TtlTemplateErrorContainer(e, "Error creating extension"))));
                    _parser.ResetState();
                }
                if (_parser.State != State.Undefined) {
                    var tokenSpan = new SnapshotSpan(_snapshot, new Span(token.StartIndex, token.Length));
                    _tags.Add(new TagSpan<TtlTokenTag>(tokenSpan, new TtlTokenTag(token, _parser.State)));
                    if (_parser.State == State.SequenceEnd) {
                        _parser.ResetState();
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
