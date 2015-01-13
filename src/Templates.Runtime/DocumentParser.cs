using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Templates.Core.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Strings.Core;

namespace Templates.Runtime {
    /// <summary>
    /// Parses document and creates template cache that can be used multiple times as source template representation, also used to replace templates with data multiple times (template source preserved)
    /// </summary>
    public sealed class DocumentParser: ICloneable, IDisposable {
        //private class DataWrapper
        //{
        //    public string Document;
        //    public SmartList<DocumentElement> Elements;

        //    public event UpdatedCallBack OnUpdated;

        //    public void Updated ()
        //    {
        //        UpdatedCallBack handler = OnUpdated;
        //        if (handler != null)
        //            handler();
        //    }
        //}

        private readonly CompileContext _context;
        private readonly string _document;
        private readonly ThreadLocal<Replacement[]> _mtReplacements;
        public FinishedEventHandler Completed;

        //private readonly DataWrapper _data;
        private SmartList<DocumentElement> _elements;
        private string _workingDocument;

        public DocumentParser (string document, CompileContext context)
        {
            if (string.IsNullOrWhiteSpace(document))
                throw new ArgumentNullException("document");
            _mtReplacements = new ThreadLocal<Replacement[]>(MakeNewArray);

            _document = document;
            _workingDocument = document;

            //_data = new DataWrapper();

            _context = context;
            Parse();
            //_data.OnUpdated += PerformUpdate;
            //PerformUpdate();
        }

        public string Cached
        {
            get
            {
                if (_elements.Length == 0)
                    return Document ?? string.Empty;
                return string.Empty;
            }
        }

        public string Document
        {
            get { return _document; }
        }

        private Replacement[] MakeNewArray ()
        {
            var replacements = new Replacement[_elements.Count];
            for (int i = 0; i < _elements.Count; i++)
                replacements[i].Position = _elements[i].Position;
            return replacements;
        }

        private void OnDone ()
        {
            if (Completed != null)
                Completed();
        }

        //private void PerformUpdate()
        //{
        //    _elements = _data.Elements.GetCrossThreadCopy();
        //    _document = _data.Document;
        //}

        //private DocumentParser(CompileContext context, DataWrapper data)
        //{
        //    _context = context;
        //    _data = data;
        //    _data.OnUpdated += PerformUpdate;
        //    PerformUpdate();
        //}

        /// <summary>
        /// Performs replacement and returns generated document string
        /// </summary>
        /// <returns>Full replaced document string</returns>
        public string ProcessData (object data)
        {
            try {
                if (data == null)
                    return string.Empty;
                if (!_context.ModelType.IsType(data)) {
                    throw new TemplateProcessingException
                        (string.Format
                             (CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}", _context.ModelType.FullName,
                              data.GetType().FullName));
                }
                Replacement[] replacements = _mtReplacements.Value;
                for (int i = 0; i < _elements.Count; i++)
                    replacements[i].ReplacementValue = _elements[i].TemplateBlock.ProcessData(data);
                return FastStringBuilder.BulkReplace(replacements, _workingDocument);
            }
            finally {
                OnDone();
            }
        }

        //public bool TryParse(string document)
        //{
        //    string lastDocument = _data.Document;
        //    SmartList<DocumentElement> lastTemplates = _data.Elements;
        //    try
        //    {
        //        _context.ResetContext();
        //        Parse(document);
        //    }
        //    catch (Exception)
        //    {
        //        _data.Document = lastDocument;
        //        _data.Elements = lastTemplates;
        //        _context.RevertBack();
        //        return false;
        //    }
        //    _context.Commit();
        //    _data.Updated();
        //    return true;
        //}

        /// <summary>
        /// Performs parse of document and creates full templates cache and returns it
        /// </summary>
        /// <returns>Full template list found in source template</returns>
        private void Parse ()
        {
            _elements = new SmartList<DocumentElement>();
            IEnumerable<Token> tokens = LexisParser.Tokenize(_workingDocument);
            var sytaxParser = new SyntaxParser();
            int startIndex = 0;
            int seed = 0;
            foreach (Token token in tokens) {
                try {
                    switch (sytaxParser.State) {
                        case State.Undefined:
                            sytaxParser.ParseNext(token);
                            if (sytaxParser.State == State.SequenceBegin)
                                startIndex = token.StartIndex - seed;
                            break;
                        default:
                            sytaxParser.ParseNext(token);
                            if (sytaxParser.State == State.SequenceEnd) {
                                DocumentElement element = EntityCompiler.CompileElement
                                    (sytaxParser.ResultExtensions, sytaxParser.ResultAdditionalDataName, sytaxParser.ResultDataName, _context);
                                element.Position = new Position(startIndex, token.StartIndex - seed + token.Length - startIndex);

                                //Means extension can't render or accept/return any data, so it can be skiped from processing list
                                if (element.TemplateBlock.RenderType != null)
                                    _elements.Add(element);
                                else
                                    seed += ApplyRemove(element, ref _workingDocument);
                                sytaxParser.ResetState();
                            }
                            break;
                    }
                }
                catch (TemplateParseException e) {
                    throw new TemplateInitException("Error upon parsing template", e, new Position(token.StartIndex, token.Length));
                }
                catch (TemplateCompileException e) {
                    throw new TemplateInitException("Error upon processing template", e, new Position(token.StartIndex, token.Length));
                }
                catch (ArgumentException e) {
                    throw new TemplateInitException("Error upon processing template", e, new Position(token.StartIndex, token.Length));
                }
                catch (TemplateCreateException e) {
                    throw new TemplateInitException("Error upon creating template", e, new Position(token.StartIndex, token.Length));
                }
            }
            _context.Compile();
        }

        private static int ApplyRemove (DocumentElement element, ref string source)
        {
            int removeStart = element.Position.StartIndex;
            int removeLength = element.Position.Length;
            if (element.Position.StartIndex > 0 && source[element.Position.StartIndex - 1] == '\n') {
                removeStart--;
                removeLength++;
                if (element.Position.StartIndex > 1 && source[element.Position.StartIndex - 2] == '\r') {
                    removeStart--;
                    removeLength++;
                }
            } else if (element.Position.StartIndex + element.Position.Length < source.Length
                       && source[element.Position.StartIndex + element.Position.Length] == '\r') {
                removeLength++;
                if (element.Position.StartIndex + element.Position.Length + 1 < source.Length
                    && source[element.Position.StartIndex + element.Position.Length + 1] == '\n')
                    removeLength++;
            }

            source = FastStringBuilder.Replace(removeStart, removeLength, string.Empty, source);
            return removeLength;
        }

        #region Implementation of ICloneable

        public object Clone ()
        {
            return MemberwiseClone();
        }

        #endregion

        #region Implementation of IDisposable

        public void Dispose ()
        {
            if (_elements != null) {
                foreach (DocumentElement element in _elements)
                    element.TemplateBlock.Dispose();
            }
        }

        #endregion

        #region Nested type: FinishedEventHandler

        public delegate void FinishedEventHandler ();

        #endregion

        //public DocumentParser GetCrossThreadCopy()
        //{
        //    return new DocumentParser(_context, _data);
        //}
    }
}