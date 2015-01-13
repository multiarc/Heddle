using System;
using System.Collections.Generic;
using System.Globalization;
using Templates.Data;
using Templates.Exceptions;
using Templates.Strings.Core;

namespace Templates.Language {
    /// <summary>
    /// Syntax parser, strict automat
    /// </summary>
    public class SyntaxParser {
        private const string ExpectedError = "\"{0}\" expected. Got \"{1}\".";
        private const string ExpectedTwoError = "\"{0}\" or \"{1}\" expected. Got \"{2}\".";
        private const string BracketError = "Incorrect \"{0}\" position.";
        private const string UnexpectedError = "Unexpected \"{0}\".";
        private const string Identifier = "Identifier";
        private const string UnexpectedParameter = "Extension Parameter string didn't expected [{0}]";
        private readonly Stack<ExtensionItem> _currentExtensionsStack;
        private readonly ExStringBuilder _currentParameter;
        private readonly List<ExtensionItem> _resultExtensions;
        private readonly Stack<Token> _stack;
        private string _currentExtension;
        private string _resultAdditionalDataName;
        private string _resultDataName;

        public SyntaxParser ()
        {
            _stack = new Stack<Token>();
            _currentExtensionsStack = new Stack<ExtensionItem>();
            _resultExtensions = new List<ExtensionItem>();
            _currentParameter = new ExStringBuilder();
            ResetState();
        }

        public IEnumerable<ExtensionItem> ResultExtensions
        {
            get { return _resultExtensions; }
        }

        public string ResultAdditionalDataName
        {
            get { return _resultAdditionalDataName; }
        }

        public string ResultDataName
        {
            get { return _resultDataName; }
        }

        /// <summary>
        /// Gets current state of automat
        /// </summary>
        public State State
        {
            get;
            private set;
        }

        /// <summary>
        /// Resets all data and automat state to initial
        /// </summary>
        public void ResetState ()
        {
            _currentExtensionsStack.Clear();
            _resultExtensions.Clear();
            _currentParameter.Clear();
            _resultDataName = string.Empty;
            _resultAdditionalDataName = string.Empty;
            _currentExtension = string.Empty;
            State = State.Undefined;
            _stack.Clear();
        }

        /// <summary>
        /// Parses next symbol/token string
        /// </summary>
        /// <param name="token">Minimum 1 character string or token string parsed by Lexical analyser</param>
        /// <returns>End automat state</returns>
        /// <exception cref="TemplateParseException">Any syntax errors</exception>
        public bool ParseNext (Token token)
        {
            if (token == null)
                throw new ArgumentNullException("token");

            if (State != State.SequenceEnd && State != State.Undefined && string.IsNullOrEmpty(token.CapturedString))
                throw new TemplateParseException("Unexpected end of file.");
            switch (State) {
                case State.Undefined:
                    return GoUndefined(token);
                case State.SequenceBegin:
                    return GoBegin(token);
                case State.NameBegin:
                    return GoNameBegin(token);
                case State.NameEnd:
                    return GoNameEnd(token);
                case State.DataNameBegin:
                    return GoDataBegin(token);
                case State.DataNameEnd:
                    return GoDataEnd(token);
                case State.AdditionalDataNameBegin:
                    return GoAdditionalData(token);
                case State.AdditionalDataNameEnd:
                    return GoAdditionalEnd(token);
                case State.ParameterBegin:
                    return GoParameter(token);
                case State.ParameterEnd:
                    return GoEnd(token);
                default:
                    return false;
            }
        }

        private bool GoUndefined (Token token)
        {
            if (token.Type == TokenType.StartTtl) {
                State = State.SequenceBegin;
                _stack.Push(token);
                return true;
            }
            return false;
        }

        private bool GoBegin (Token token)
        {
            if (token.Type != TokenType.Space) {
                if (token.Type == TokenType.StartExtensionsBlock) {
                    State = State.NameBegin;
                    return true;
                }
                if (token.Type == TokenType.ValidIdentifier) {
                    State = State.DataNameBegin;
                    _resultDataName += token.CapturedString;
                    _currentExtensionsStack.Push(new ExtensionItem(string.Empty));
                    return true;
                }
                throw new TemplateParseException
                    (string.Format
                         (CultureInfo.InvariantCulture, ExpectedTwoError, ParserConfiguration.StartExtensionsBlock, Identifier, token.CapturedString));
            }
            return true;
        }

        private bool GoNameBegin (Token token)
        {
            if (token.Type != TokenType.ValidIdentifier) {
                if (token.Type == TokenType.EndExtensionsBlock) {
                    _currentExtensionsStack.Push(new ExtensionItem(_currentExtension));
                    _currentExtension = string.Empty;
                    State = State.NameEnd;
                    return true;
                }
                if (token.Type == TokenType.ExtensionDelimeter) {
                    _currentExtensionsStack.Push(new ExtensionItem(_currentExtension));
                    _currentExtension = string.Empty;
                    return true;
                }
                throw new TemplateParseException
                    (string.Format(CultureInfo.InvariantCulture, ExpectedError, ParserConfiguration.EndExtensionsBlock, token.CapturedString));
            }
            _currentExtension += token.CapturedString;
            return true;
        }

        private bool GoNameEnd (Token token)
        {
            if (token.Type != TokenType.Space) {
                if (token.Type == TokenType.ValidIdentifier) {
                    State = State.DataNameBegin;
                    _resultDataName += token.CapturedString;
                    return true;
                }
                if (token.Type == TokenType.StartParameter) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.ParameterBegin;
                        _stack.Push(token);
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                if (token.Type == TokenType.EndTtl) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.SequenceEnd;
                        _stack.Pop();
                        _resultExtensions.AddRange(_currentExtensionsStack);
                        _currentExtensionsStack.Clear();
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, UnexpectedError, token.CapturedString));
            }
            return true;
        }

        private bool GoDataBegin (Token token)
        {
            if (token.Type != TokenType.ValidIdentifier) {
                if (token.Type == TokenType.Space) {
                    State = State.AdditionalDataNameBegin;
                    return true;
                }
                if (token.Type == TokenType.StartParameter) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.ParameterBegin;
                        _stack.Push(token);
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                if (token.Type == TokenType.EndTtl) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.SequenceEnd;
                        _stack.Pop();
                        _resultExtensions.AddRange(_currentExtensionsStack);
                        _currentExtensionsStack.Clear();
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, UnexpectedError, token.CapturedString));
            }
            _resultDataName += token.CapturedString;
            return true;
        }

        private bool GoDataEnd (Token token)
        {
            if (token.Type != TokenType.Space) {
                if (token.Type == TokenType.StartParameter) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.ParameterBegin;
                        _stack.Push(token);
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                if (token.Type == TokenType.EndTtl) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.SequenceEnd;
                        _stack.Pop();
                        _resultExtensions.AddRange(_currentExtensionsStack);
                        _currentExtensionsStack.Clear();
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                if (token.Type == TokenType.ValidIdentifier) {
                    _resultAdditionalDataName += token.CapturedString;
                    State = State.AdditionalDataNameBegin;
                    return true;
                }
                throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, UnexpectedError, token.CapturedString));
            }
            return true;
        }

        private bool GoAdditionalData (Token token)
        {
            if (token.Type != TokenType.ValidIdentifier) {
                if (token.Type == TokenType.Space) {
                    State = State.AdditionalDataNameEnd;
                    return true;
                }
                if (token.Type == TokenType.StartParameter) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.ParameterBegin;
                        _stack.Push(token);
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                if (token.Type == TokenType.EndTtl) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.SequenceEnd;
                        _stack.Pop();
                        _resultExtensions.AddRange(_currentExtensionsStack);
                        _currentExtensionsStack.Clear();
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, UnexpectedError, token.CapturedString));
            }
            _resultAdditionalDataName += token.CapturedString;
            return true;
        }

        private bool GoAdditionalEnd (Token token)
        {
            if (token.Type != TokenType.Space) {
                if (token.Type == TokenType.StartParameter) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.ParameterBegin;
                        _stack.Push(token);
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                if (token.Type == TokenType.EndTtl) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.SequenceEnd;
                        _stack.Pop();
                        _resultExtensions.AddRange(_currentExtensionsStack);
                        _currentExtensionsStack.Clear();
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, UnexpectedError, token.CapturedString));
            }
            return true;
        }

        private bool GoParameter (Token token)
        {
            if (token.Type == TokenType.StartTtl) {
                _currentParameter.Append(token.CapturedString);
                _stack.Push(token);
                return true;
            }
            if (token.Type == TokenType.StartParameter) {
                _currentParameter.Append(token.CapturedString);
                _stack.Push(token);
                return true;
            }
            if (token.Type == TokenType.EndParameter) {
                if (_stack.Peek().Type == TokenType.StartParameter) {
                    _stack.Pop();
                    if (_stack.Count == 1) {
                        State = State.ParameterEnd;
                        if (_currentExtensionsStack.Count == 0)
                            throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, UnexpectedParameter, token.CapturedString));
                        ExtensionItem extension = _currentExtensionsStack.Pop();
                        extension.ParameterTemplate = _currentParameter.ToString();
                        _resultExtensions.Add(extension);
                    } else
                        _currentParameter.Append(token.CapturedString);
                    return true;
                }
                throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
            }
            if (token.Type == TokenType.EndTtl) {
                if (_stack.Peek().Type == TokenType.StartTtl) {
                    _currentParameter.Append(token.CapturedString);
                    _stack.Pop();
                    return true;
                }
                throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
            }
            _currentParameter.Append(token.CapturedString);
            return true;
        }

        private bool GoEnd (Token token)
        {
            if (token.Type != TokenType.Space) {
                if (token.Type == TokenType.EndTtl) {
                    if (_stack.Peek().Type == TokenType.StartTtl) {
                        State = State.SequenceEnd;
                        _stack.Pop();
                        _resultExtensions.AddRange(_currentExtensionsStack);
                        _currentExtensionsStack.Clear();
                        return true;
                    }
                    throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, BracketError, token.CapturedString));
                }
                if (token.Type == TokenType.StartParameter) {
                    if (_currentExtensionsStack.Count == 0)
                        throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, UnexpectedParameter, token.CapturedString));
                    _currentParameter.Clear();
                    State = State.ParameterBegin;
                    _stack.Push(token);
                    return true;
                }
                throw new TemplateParseException(string.Format(CultureInfo.InvariantCulture, UnexpectedError, token.CapturedString));
            }
            return true;
        }
    }
}