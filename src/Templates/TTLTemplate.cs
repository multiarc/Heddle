using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Runtime;

namespace Templates {
    /// <summary>
    /// Use this class to operate with engine, parse source template, make replace with data and generate result string.
    /// </summary>
    public sealed class TtlTemplate: IDisposable {
        private const int FileCheckDelay = 5000; //milliseconds
        private CompileContext _context;
        private readonly FileReader _reader;
        private readonly Timer _timer;
        private volatile RuntimeDocument _runtimeDocument;
        private string _document;

        public TtlTemplate(TemplateOptions options) : this(new CompileContext(options))
        {
            
        }

        public TtlTemplate(TemplateOptions options, Type modelType) : this(new CompileContext(options) {ModelType =  modelType}) {

        }

        public TtlTemplate(CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (string.IsNullOrWhiteSpace(context.Options.FileNamePostfix))
                throw new ArgumentException("File Name postfix (extension) should not be empty");
            if (string.IsNullOrWhiteSpace(context.Options.RootPath))
                throw new ArgumentException("Root Path (directory) should not be empty");
            if (string.IsNullOrWhiteSpace(context.Options.TemplateName))
                throw new ArgumentException("Template Name should not be empty");

            try
            {
                _reader = new FileReader(context.Options);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Cannot open file", e);
            }
            string document;
            try
            {
                document = _reader.ReadEntireFile();
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException("File not found", e);
            }
            Compile(context, document);
            if (context.Options.EnableFileChangeCheck)
            {
                _timer = new Timer(CheckFileChange, null, FileCheckDelay, int.MaxValue);
            }
        }

        public TtlTemplate()
        {
            
        }

        public TtlTemplate (string document, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            var rtdoc = DocumentsCache.GetRuntimeDocument(document, context);
            if (rtdoc == null)
            {
                rtdoc = TtlCompiler.Compile(document, context, DocumentParser.Parse(document));
                DocumentsCache.UpdateCaches(rtdoc, null, document, context);
            }
            _runtimeDocument = rtdoc;
            _context = context;
            _document = document;
        }

        public bool Empty => _runtimeDocument?.Empty ?? true;

        #region IDisposable Members

        public void Dispose ()
        {
            _timer?.Dispose();
            _runtimeDocument?.Dispose();
            GC.SuppressFinalize(this);
        }

        ~TtlTemplate()
        {
            _timer?.Dispose();
            _runtimeDocument?.Dispose();
        }


        #endregion

        /// <summary>
        /// Generates result string (source template replaced with data). Data non-serialized
        /// </summary>
        /// <param name="data">Input object</param>
        /// <returns>Generated string</returns>
        public string Generate(object data)
        {
            if (_runtimeDocument == null)
                throw new TemplateInitException("Compile first.");
#if DEBUG
            if (data != null && !_context.ModelType.IsType(data)) {
                throw new TemplateProcessingException
                    (string.Format
                         (CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}", _context.ModelType.FullName,
                          data.GetType().FullName));
            }
#endif
            return _runtimeDocument.ProcessData(data, null) as string ?? string.Empty;
        }
        public TtlCompileResult Recompile(Type newModelType)
        {
            try
            {
                return Compile(new CompileContext(newModelType), _document);
            }
            catch (Exception e)
            {
                var result = new TtlCompileResult(false);
                result.Errors.Add(new TtlCompileError()
                {
                    Error = e.Message,
                    Exception = e
                });
                return result;
            }
        }

        public TtlCompileResult Compile(string document, Type modelType = null)
        {
            try {
                return Compile(new CompileContext(modelType), document);
            }
            catch (Exception e) {
                var result = new TtlCompileResult(false);
                result.Errors.Add(new TtlCompileError()
                {
                    Error = e.Message,
                    Exception = e
                });
                return result;
            }
        }


        private TtlCompileResult Compile(CompileContext context, string document) {
            var rtdoc = DocumentsCache.GetRuntimeDocument(document, context);
            if (rtdoc == null) {
                rtdoc = TtlCompiler.Compile(document, context, DocumentParser.Parse(document));
                DocumentsCache.UpdateCaches(rtdoc, null, document, context);
            }
            _context = context;
            _document = document;
            _runtimeDocument = rtdoc;
            return new TtlCompileResult(true);
        }

        private void CheckFileChange (object state)
        {
            try {
                if (_reader != null && _reader.GetIsModified()) {
                    string document = _reader.ReadEntireFile();
                    if (!string.IsNullOrWhiteSpace(document)) {
                        try
                        {
                            var rtdoc = TtlCompiler.Compile(document, _context, DocumentParser.Parse(document));
                            DocumentsCache.UpdateCaches(rtdoc, _document, document, _context);
                            _runtimeDocument = rtdoc;
                            _document = document;
                        }
                        catch (Exception e) {
                            //TODO: Log Exception here
                        }
                    }
                }
            }
            catch (Exception e) {
                //TODO: Log Exception here
            }
            finally {
                _timer.Change(FileCheckDelay, int.MaxValue);
            }
        }
    }
}