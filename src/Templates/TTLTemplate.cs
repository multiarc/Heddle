using System;
using System.Globalization;
using System.IO;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Runtime;
using Templates.Strings.Core;

namespace Templates
{
    /// <summary>
    /// Use this class to operate with engine, parse source template, make replace with data and generate result string.
    /// </summary>
    public sealed class TtlTemplate : ITtlTemplate
    {
        private CompileContext _context;
        public TtlCompileResult CompileResult { get; private set; }
        private readonly FileReader _reader;
        private readonly FileSystemWatcher _watcher;
        private volatile RuntimeDocument _runtimeDocument;
        private string _document;
        private volatile bool _disposeAfterComplete = false;
        private volatile int _runners = 0;

        public TtlTemplate(TemplateOptions options) : this(new CompileContext(options))
        {
        }

        public TtlTemplate(TemplateOptions options, ExType modelType) : this(new CompileContext(options) {ModelType = modelType})
        {
        }

        public TtlTemplate(CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            string document = null;
            try
            {
                _reader = new FileReader(context.Options);
                document = _reader.ReadEntireFile();
                CompileResult = Compile(context, document);
                if (context.Options.EnableFileChangeCheck)
                {
                    var fullPath = Path.Combine(context.Options.RootPath, context.Options.TemplateName);
                    var directory = Path.GetDirectoryName(fullPath);
                    if (directory != null)
                    {
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
            catch (Exception e)
            {
                CompileResult = new TtlCompileResult(false, document);
                CompileResult.Errors.Add(e.ToError(default(BlockPosition)));
            }
        }

        public TtlTemplate()
        {

        }

        public TtlTemplate(string document, CompileContext context = null)
        {
            CompileResult = Compile(context ?? new CompileContext(), document);
        }

        public bool Empty => _runtimeDocument?.Empty ?? true;
        public bool Compiled => _runtimeDocument != null;

        public CompileContext Context => _context;

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public DateTime MasterDateCreated { get; set; }
            = DateTime.Now;

        #region IDisposable Members

        public void Dispose()
        {
            if (_runners == 0)
            {
                _watcher?.Dispose();
                _runtimeDocument?.Dispose();
                GC.SuppressFinalize(this);
            }
            else
            {
                _disposeAfterComplete = true;
            }
        }

        ~TtlTemplate()
        {
            _watcher?.Dispose();
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
            {
                if (CompileResult == null)
                    throw new TemplateInitException("Compile first");
                throw new TemplateCompileException(CompileResult.ErrorList);
            }

#if DEBUG
            if (data != null && !_context.ModelType.Type.IsType(data))
            {
                throw new TemplateProcessingException
                    (string.Format
                        (CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}",
                            _context.ModelType.Type?.FullName ?? _context.ModelType.ToString(),
                            data.GetType().FullName));
            }
#endif
            _runners++;
            string result = null;
            try
            {
                result = _runtimeDocument.ProcessData(data, null) as string ?? string.Empty;
            }
            finally
            {
                _runners--;
                if (_disposeAfterComplete && _runners == 0)
                {
                    Dispose();
                }
            }
            return result;
        }

        public TtlCompileResult Recompile(ExType newModelType)
        {
            DateCreated = DateTime.Now;
            CompileResult = Compile(new CompileContext(newModelType), _document);
            return CompileResult;
        }

        public TtlCompileResult Recompile(string newDocument, CompileContext context = null)
        {
            DateCreated = DateTime.Now;
            CompileResult = Compile(context ?? new CompileContext((ExType) null), newDocument);
            return CompileResult;
        }

        public TtlCompileResult Compile(string document, ExType modelType = null)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            return Compile(new CompileContext(modelType), document);
        }

        public TtlCompileResult TryCompilation(string document, ExType modelType = null)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            return Compile(new CompileContext(modelType), document, true);
        }

        private TtlCompileResult Compile(CompileContext context, string document, bool simulate = false)
        {
            try
            {
                RuntimeDocument rtdoc = TtlCompiler.Compile(document, context,
                    DocumentParser.Parse(document, context, context.Options.ProvideLanguageFeatures, context.Options.ForceRemoveWhitespace), null);
                if (context.CompileErrors.Count > 0)
                {
                    context.Dispose();
                    rtdoc?.Dispose();
                    var result = new TtlCompileResult(false, document);
                    result.Errors.AddRange(context.CompileErrors);
                    return result;
                }
                if (!simulate)
                {
                    context.Compile();
                    _context = context;
                    _document = document;
                    _runtimeDocument = rtdoc;
                }
                else
                {
                    context.Dispose();
                    rtdoc.Dispose();
                }
                return new TtlCompileResult(true, document);
            }
            catch (TemplateCompileException e)
            {
                var result = new TtlCompileResult(false, document);
                result.Errors.AddRange(e.Errors);
                return result;
            }
            catch (Exception e)
            {
                var result = new TtlCompileResult(false, document);
                result.Errors.Add(e.ToError(default(BlockPosition)));
                return result;
            }
        }

        public event FileSystemEventHandler OnFileDeleted;

        public event RenamedEventHandler OnFileRenamed;

        public event FileSystemEventHandler OnFileChanged;

        private void FileDeleted(object sender, FileSystemEventArgs e)
        {
            OnFileDeleted?.Invoke(this, e);
        }

        private void FileRenamed(object sender, RenamedEventArgs e)
        {
            OnFileRenamed?.Invoke(this, e);
        }

        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                OnFileChanged?.Invoke(sender, e);
                string document = null;
                try
                {
                    document = _reader.ReadEntireFile();
                    if (!string.IsNullOrWhiteSpace(document))
                    {
                        CompileResult = Compile(_context, document);
                    }
                }
                catch (Exception ex)
                {
                    CompileResult = new TtlCompileResult(false, document);
                    CompileResult.Errors.Add(ex.ToError(default(BlockPosition)));
                }
            }
        }
    }
}