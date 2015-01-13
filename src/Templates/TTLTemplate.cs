using System;
using System.Threading;
using Templates.Runtime;

namespace Templates {
    /// <summary>
    /// Use this class to operate with engine, parse source template, make replace with data and generate result string.
    /// </summary>
    public class TtlTemplate: IDisposable {
        private const int FileCheckDelay = 5000; //seconds
        private readonly ManualResetEvent _allFinihed = new ManualResetEvent(true);
        private readonly CompileContext _context;
        private readonly bool _enableFileChangeCheck;
        private readonly Type _initialType;
        private readonly ManualResetEvent _objectLock = new ManualResetEvent(true);
        private readonly object _processingLock = new object();
        private readonly FileReader _reader;
        private readonly Timer _timer;
        private bool _locked;
        private DocumentParser _parser;
        private int _pasersInprocessing;

        public TtlTemplate (CompileContext context, bool enableFileChangeCheck = true)
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
            if (enableFileChangeCheck) {
                _initialType = context.ModelType;
                _enableFileChangeCheck = true;
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
                if (enableFileChangeCheck)
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

        #region IDisposable Members

        public void Dispose ()
        {
            if (_parser != null)
                _parser.Dispose();
            if (_context != null)
                _context.Dispose();
        }

        #endregion

        ~TtlTemplate ()
        {
            if (_timer != null)
                _timer.Dispose();
            _objectLock.Close();
            _allFinihed.Close();
        }

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
            if (_enableFileChangeCheck) {
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
                            _context.ResetContext();
                            var newParser = new DocumentParser(document, _context);
                            newParser.Completed += ParserDone;
                            Lock();
                            _allFinihed.WaitOne();
                            DocumentsCache.UpdateCaches(newParser, _parser.Document, _initialType, _context.Options.RootPath);
                            _parser = newParser;
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