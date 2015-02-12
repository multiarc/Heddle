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

namespace Templates.Editor {
    internal sealed class TokenTagger: ITagger<TokenTag>
    {
        private ITextBuffer _buffer;
        private readonly SyntaxParser _parser = new SyntaxParser();

        internal TokenTagger(ITextBuffer buffer,
                               ITagAggregator<TokenTag> ookTagAggregator,
                               IClassificationTypeRegistryService typeService)
        {
            _buffer = buffer;
        }

        public IEnumerable<ITagSpan<TokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (SnapshotSpan curSpan in spans)
            {
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
                        throw new TemplateInitException("Error upon parsing template", e, new BlockPosition(token.StartIndex, token.Length));
                    }
                    catch (TemplateCompileException e) {
                        throw new TemplateInitException("Error upon processing template", e, new BlockPosition(token.StartIndex, token.Length));
                    }
                    catch (ArgumentException e) {
                        throw new TemplateInitException("Error upon processing template", e, new BlockPosition(token.StartIndex, token.Length));
                    }
                    catch (TemplateCreateException e) {
                        throw new TemplateInitException("Error upon creating template", e, new BlockPosition(token.StartIndex, token.Length));
                    }
                    if (_parser.State != State.Undefined) {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(token.StartIndex, token.Length));
                        if (tokenSpan.IntersectsWith(curSpan)) {
                            yield return new TagSpan<TokenTag>(tokenSpan, new TokenTag(token, _parser.State));
                        }
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
