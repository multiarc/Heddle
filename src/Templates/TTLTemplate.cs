using System;
using System.Threading;
using Templates.Data;
using Templates.Runtime;

namespace Templates {
    /// <summary>
    /// Use this class to operate with engine, parse source template, make replace with data and generate result string.
    /// </summary>
    public sealed class TtlTemplate: IDisposable {
        private const int FileCheckDelay = 5000; //milliseconds
        private readonly CompileContext _context;
        private readonly FileReader _reader;
        private readonly Timer _timer;
        private volatile RuntimeDocument _runtimeDocument;
        private string _document;

        public TtlTemplate(TemplateOptions options) : this(new CompileContext(options))
        {
            
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
            var rtdoc = DocumentsCache.GetRuntimeDocument(document, context);
            if (rtdoc == null)
            {
                rtdoc = TtlCompiler.Compile(document, context, DocumentParser.Parse(document));
                DocumentsCache.UpdateCaches(rtdoc, null, document, context);
            }
            _context = context;
            _document = document;
            _runtimeDocument = rtdoc;

            if (context.Options.EnableFileChangeCheck)
            {
                _timer = new Timer(CheckFileChange, null, FileCheckDelay, int.MaxValue);
            }
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
            if (_timer != null) _timer.Dispose();
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
        public string Generate(object data) {
            return _runtimeDocument?.ProcessData(data, null) ?? string.Empty;
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