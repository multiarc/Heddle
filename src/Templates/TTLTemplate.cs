using System;
using System.Globalization;
using System.IO;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Runtime;

namespace Templates {
    /// <summary>
    /// Use this class to operate with engine, parse source template, make replace with data and generate result string.
    /// </summary>
    public sealed class TtlTemplate: ITtlTemplate
    {
        private CompileContext _context;
        public TtlCompileResult CompileResult { get; private set; }
        private readonly FileReader _reader;
        private readonly FileSystemWatcher _watcher;
        private volatile RuntimeDocument _runtimeDocument;
        private string _document;

        public TtlTemplate(TemplateOptions options) : this(new CompileContext(options)) {
        }

        public TtlTemplate(TemplateOptions options, ExType modelType) : this(new CompileContext(options) { ModelType =  modelType }) {
        }

        public TtlTemplate(CompileContext context) {
            if (context == null)
                throw new ArgumentNullException("context");
            try {
                _reader = new FileReader(context.Options);
                var document = _reader.ReadEntireFile();
                CompileResult = Compile(context, document);
                if (context.Options.EnableFileChangeCheck) {
                    var fullPath = Path.Combine(context.Options.RootPath, context.Options.TemplateName);
                    var directory = Path.GetDirectoryName(fullPath);
                    if (directory != null) {
                        _watcher = new FileSystemWatcher(directory)
                        {
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                            Filter = Path.GetFileName(fullPath)
                        };
                        _watcher.Changed += FileChanged;
                        _watcher.Deleted += FileDeleted;
                        _watcher.Renamed += FileRenamed;
                    }
                }
            }
            catch (Exception e) {
                CompileResult = new TtlCompileResult(false);
                CompileResult.Errors.Add(e.ToError());
            }
        }

        public TtlTemplate() {

        }

        public TtlTemplate(string document) {
            CompileResult = Compile(new CompileContext(), document);
        }

        public TtlTemplate(string document, CompileContext context) {
            if (context == null)
                throw new ArgumentNullException("context");
            CompileResult = Compile(context, document);
        }

        public bool Empty => _runtimeDocument?.Empty ?? true;

        public CompileContext Context => _context;

        #region IDisposable Members

        public void Dispose() {
            _watcher?.Dispose();
            _runtimeDocument?.Dispose();
            GC.SuppressFinalize(this);
        }

        ~TtlTemplate() {
            _watcher?.Dispose();
            _runtimeDocument?.Dispose();
        }


        #endregion

        /// <summary>
        /// Generates result string (source template replaced with data). Data non-serialized
        /// </summary>
        /// <param name="data">Input object</param>
        /// <returns>Generated string</returns>
        public string Generate(object data) {
            if (_runtimeDocument == null) {
                if (CompileResult == null)
                    throw new TemplateInitException("Compile first.");
                throw new TemplateCompileException(CompileResult.ErrorList);
            }
                
#if DEBUG
            if (data != null && !_context.ModelType.Type.IsType(data)) {
                throw new TemplateProcessingException
                    (string.Format
                         (CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}", _context.ModelType.Type?.FullName ?? _context.ModelType.ToString(),
                          data.GetType().FullName));
            }
#endif
            return _runtimeDocument.ProcessData(data, null) as string ?? string.Empty;
        }
        public TtlCompileResult Recompile(ExType newModelType) {
            return Compile(new CompileContext(newModelType), _document);
        }

        public TtlCompileResult Compile(string document, ExType modelType = null) {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            return Compile(new CompileContext(modelType), document);
        }


        private TtlCompileResult Compile(CompileContext context, string document) {
            try {
                var rtdoc = DocumentsCache.GetRuntimeDocument(document, context);
                if (rtdoc == null) {
                    rtdoc = TtlCompiler.Compile(document, context, DocumentParser.Parse(document));
                    DocumentsCache.UpdateCaches(rtdoc, null, context);
                    context.Compile();
                }
                _context = context;
                _document = document;
                _runtimeDocument = rtdoc;
                return new TtlCompileResult(true);
            }
            catch (Exception e) {
                var result = new TtlCompileResult(false);
                result.Errors.Add(e.ToError());
                return result;
            }
        }

        public event FileSystemEventHandler OnFileDeleted;

        public event RenamedEventHandler OnFileRenamed;

        public event FileSystemEventHandler OnFileChanged;

        private void FileDeleted(object sender, FileSystemEventArgs e) {
            OnFileDeleted?.Invoke(this, e);
        }

        private void FileRenamed(object sender, RenamedEventArgs e) {
            OnFileRenamed?.Invoke(this, e);
        }

        private void FileChanged(object sender, FileSystemEventArgs e) {
            if (e.ChangeType == WatcherChangeTypes.Changed) {
                OnFileChanged?.Invoke(sender, e);
                try {
                    string document = _reader.ReadEntireFile();
                    if (!string.IsNullOrWhiteSpace(document))
                    {
                        CompileResult = Compile(_context, document);
                    }
                }
                catch (Exception ex)
                {
                    CompileResult = new TtlCompileResult(false);
                    CompileResult.Errors.Add(ex.ToError());
                }
            }
        }
    }
}