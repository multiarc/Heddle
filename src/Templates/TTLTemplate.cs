using System;
using System.Threading;
using Templates.Helpers;
using Templates.Runtime;

namespace Templates {
    /// <summary>
    /// Use this class to operate with engine, parse source template, make replace with data and generate result string.
    /// </summary>
    public sealed class TtlTemplate: IDisposable {
        private const int FileCheckDelay = 5000; //milliseconds
        private readonly ManualResetEvent _allFinihed = new ManualResetEvent(true);
        private readonly CompileContext _context;
        private readonly Type _initialType;
        private readonly ManualResetEvent _objectLock = new ManualResetEvent(true);
        private readonly object _processingLock = new object();
        private readonly FileReader _reader;
        private readonly Timer _timer;
        private bool _locked;
        private DocumentParser _parser;
        private int _pasersInprocessing;

        public TtlTemplate (CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (string.IsNullOrWhiteSpace(context.Options.FileNamePostfix))
                throw new ArgumentException("File Name postfix (extension) should not be empty");
            if (string.IsNullOrWhiteSpace(context.Options.RootPath))
                throw new ArgumentException("Root Path (directory) should not be empty");
            if (string.IsNullOrWhiteSpace(context.Options.TemplateName))
                throw new ArgumentException("Template Name should not be empty");

            _context = context;
            if (context.Options.EnableFileChangeCheck) {
                _initialType = context.ModelType;
                _timer = new Timer(CheckFileChange, null, FileCheckDelay, int.MaxValue);
            }

            try {
                _reader = new FileReader(context.Options);
            }
            catch (Exception e) {
                throw new ArgumentException("Cannot open file", e);
            }

            try {
                _parser = DocumentsCache.GetDocumentParser(_reader.ReadEntireFile(), context);
                if (context.Options.EnableFileChangeCheck)
                    _parser.Completed += ParserDone;
            }
            catch (ArgumentException e) {
                throw new ArgumentException("File not found", e);
            }
        }

        public TtlTemplate (string document, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            _context = context;
            _parser = DocumentsCache.GetDocumentParser(document, context);
        }

        internal string Cached
        {
            get { return _parser.Cached; }
        }

        private bool _disposing;

        #region IDisposable Members

        public void Dispose ()
        {
            if (!_disposing)
            {
                _disposing = true;
                if (_timer != null)
                    _timer.Dispose();
                _objectLock.Close();
                _allFinihed.Close();
            }
        }

        #endregion

        private void Lock ()
        {
            _objectLock.Reset();
            _locked = true;
        }

        private void Unlock ()
        {
            _objectLock.Set();
            _locked = false;
        }

        private void ParserDone ()
        {
            lock (_processingLock) {
                _pasersInprocessing--;
                if (_pasersInprocessing == 0)
                    _allFinihed.Set();
            }
        }

        /// <summary>
        /// Generates result string (source template replaced with data). Data non-serialized
        /// </summary>
        /// <param name="data">Input object</param>
        /// <returns>Fully replaced string</returns>
        public string GenerateString (object data)
        {
            if (_context.Options.EnableFileChangeCheck) {
                if (_locked)
                    _objectLock.WaitOne();
                if (_parser != null) {
                    lock (_processingLock) {
                        _pasersInprocessing++;
                        if (_pasersInprocessing == 1)
                            _allFinihed.Reset();
                    }
                    return _parser.ProcessData(data);
                }
            }
            return _parser != null ? _parser.ProcessData(data) : string.Empty;
        }

        private void CheckFileChange (object state)
        {
            try {
                if (_reader != null && _reader.GetIsModified()) {
                    string document = _reader.ReadEntireFile();
                    if (!string.IsNullOrWhiteSpace(document)) {
                        try {
                            _context.Clear();
                            var newParser = new DocumentParser(_context);
                            newParser.Completed += ParserDone;
                            Lock();
                            _allFinihed.WaitOne();
                            DocumentsCache.UpdateCaches(newParser, _parser.Document, _initialType, _context.Options.RootPath);
                            _parser = newParser;
                            _parser.Parse(document);
                            _context.Commit();
                            Unlock();
                        }
                        catch (Exception e) {
                            Unlock();
                            _context.RevertBack();
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