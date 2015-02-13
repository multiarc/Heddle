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
    internal sealed class TtlTokenTagger: ITagger<TtlTokenTag>
    {
        private ITextBuffer _buffer;
        private readonly SyntaxParser _parser = new SyntaxParser();

        internal TtlTokenTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
        }

        public IEnumerable<ITagSpan<TtlTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            var tags = new List<ITagSpan<TtlTokenTag>>();
            foreach (SnapshotSpan curSpan in spans) {
                var tokens = LexisParser.Tokenize(curSpan.GetText());
                foreach (Token token in tokens) {
                    try {
                        switch (_parser.State) {
                        case State.Undefined:
                            _parser.ParseNext(token);
                            break;
                        default:
                            _parser.ParseNext(token);
                            if (_parser.State == State.SequenceEnd) {
                                _parser.ResetState();
                            }
                            break;
                        }
                    }
                    catch (TemplateParseException e) {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(token.StartIndex, token.Length));
                        tags.Add(new TagSpan<TtlTokenTag>(tokenSpan, new TtlTokenTag(token, State.SyntaxError, new TtlTemplateErrorContainer(e, "Error parsing template"))));
                    }
                    catch (TemplateCompileException e) {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(token.StartIndex, token.Length));
                        tags.Add(new TagSpan<TtlTokenTag>(tokenSpan, new TtlTokenTag(token, State.CompileError, new TtlTemplateErrorContainer(e, "Error compiling template"))));
                    }
                    catch (ArgumentException e) {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(token.StartIndex, token.Length));
                        tags.Add(new TagSpan<TtlTokenTag>(tokenSpan, new TtlTokenTag(token, State.CompileError, new TtlTemplateErrorContainer(e, "Error compiling template"))));
                    }
                    catch (TemplateCreateException e) {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(token.StartIndex, token.Length));
                        tags.Add(new TagSpan<TtlTokenTag>(tokenSpan, new TtlTokenTag(token, State.OtherError, new TtlTemplateErrorContainer(e, "Error creating extension"))));
                    }
                    if (_parser.State != State.Undefined) {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(token.StartIndex, token.Length));
                        if (tokenSpan.IntersectsWith(curSpan)) {
                            tags.Add(new TagSpan<TtlTokenTag>(tokenSpan, new TtlTokenTag(token, _parser.State)));
                        }
                    }
                }
            }
            return tags;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
