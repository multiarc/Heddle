using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Language;
using Templates.Native;
using Templates.Runtime;
using Templates.Strings.Core;

namespace Templates
{
    /// <summary>
    /// Use this class to operate with engine, parse source template, make replace with data and generate result string.
    /// </summary>
    public sealed class TtlTemplate : ITtlTemplate
    {
        private CompileScope _context;
        public TtlCompileResult CompileResult { get; private set; }
        private FileReader _reader;
        private FileSystemWatcher _watcher;
        private volatile RuntimeDocument _runtimeDocument;
        private volatile IProcessStrategy _processStrategy;
        private string _document;
        private volatile bool _disposeAfterComplete;
        private volatile int _runners;

        public TtlTemplate(TemplateOptions options) : this(new CompileContext(options))
        {
        }

        public TtlTemplate(TemplateOptions options, ExType modelType) : this(
            new CompileContext(options,
                modelType))
        {
        }

        public TtlTemplate(CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            
            CompileResult = Compile(context);
        }

        public TtlTemplate()
        {
        }

        public TtlTemplate(string document, CompileContext context = null)
        {
            CompileResult = Compile(new CompileScope(context ?? new CompileContext()), document);
        }

        public bool Empty => _runtimeDocument?.Empty ?? true;
        public bool Compiled => _runtimeDocument != null;

        public CompileContext Context => _context.CompileContext;

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
        /// <param name="callerData"></param>
        /// <param name="chained"></param>
        /// <returns>Generated string</returns>
        public string Generate(object data, object chained = null, dynamic callerData = null)
        {
            if (_processStrategy == null)
            {
                if (CompileResult == null)
                    throw new TemplateInitException("Compile first");
                throw new TemplateCompileException(CompileResult.ErrorList);
            }

#if DEBUG
            if (data != null && !_context.ScopeType.Type.IsType(data))
            {
                throw new TemplateProcessingException
                    (string.Format
                        (CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}",
                            _context.ScopeType.Type?.FullName ?? _context.ScopeType.ToString(),
                            data.GetType().FullName));
            }
#endif
            if (_disposeAfterComplete)
            {
                throw new ObjectDisposedException($"{GetType()} Disposed");
            }
            _runners++;
            string result;
            try
            {
                var scope = new Scope(data, callerData, data, chained);
                result = _processStrategy.Execute(ref scope);
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
            CompileResult = Compile(new CompileScope(new CompileContext(newModelType)), _document);
            return CompileResult;
        }

        public TtlCompileResult Recompile(string newDocument, CompileContext context = null)
        {
            CompileResult = Compile(new CompileScope(context ?? new CompileContext()), newDocument);
            return CompileResult;
        }

        public TtlCompileResult Compile(CompileContext context)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");

            string document = null;
            try
            {
                _reader = new FileReader(context.Options);
                document = _reader.ReadEntireFile();
                CompileResult = Compile(new CompileScope(context), document);
                if (context.Options.EnableFileChangeCheck)
                {
                    var fullPath = Path.Combine(context.Options.RootPath, context.Options.TemplateName);
                    var directory = Path.GetDirectoryName(fullPath);
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
            catch (Exception e)
            {
                CompileResult = new TtlCompileResult(false, document, null);
                CompileResult.Errors.Add(e.ToError(default(BlockPosition)));
            }
            return CompileResult;
        }

        public TtlCompileResult Compile(string document, ExType modelType = null)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            return Compile(new CompileScope(new CompileContext(modelType)), document);
        }

        public TtlCompileResult TryCompilation(CompileContext context)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            var reader = new FileReader(context.Options);
            var document = reader.ReadEntireFile();
            return Compile(new CompileScope(context), document, true);
        }

        public TtlCompileResult TryCompilation(string document, TemplateOptions options = null, ExType modelType = null)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            return Compile(new CompileScope(new CompileContext(options ?? new TemplateOptions(),
                modelType)), document, true);
        }

        private TtlCompileResult Compile(CompileScope compileScope, string document, bool simulate = false)
        {
            try
            {
                var parseContext = DocumentParser.Parse(document, compileScope.CompileContext);
                try
                {
                    RuntimeDocument rtdoc = TtlCompiler.Compile(document, compileScope,
                        parseContext, null);
                    if (compileScope.CompileErrors.Count > 0)
                    {
                        compileScope.Dispose();
                        rtdoc?.Dispose();
                        var result = new TtlCompileResult(false, document, parseContext);
                        result.Errors.AddRange(compileScope.CompileErrors);
                        return result;
                    }
                    if (!simulate)
                    {
                        compileScope.Compile();
                        if (compileScope.CompileErrors.Count > 0)
                        {
                            compileScope.Dispose();
                            rtdoc?.Dispose();
                            var result = new TtlCompileResult(false, document, parseContext);
                            result.Errors.AddRange(compileScope.CompileErrors);
                            return result;
                        }
                        _context = compileScope;
                        _document = document;
                        _runtimeDocument = rtdoc;
                        _processStrategy = rtdoc?.Strategy;
                    }
                    else
                    {
                        compileScope.Dispose();
                        rtdoc.Dispose();
                    }
                    return new TtlCompileResult(true, document, parseContext);
                }
                catch (TemplateCompileException e)
                {
                    var result = new TtlCompileResult(false, document, parseContext);
                    result.Errors.AddRange(e.Errors);
                    return result;
                }
                catch (Exception e)
                {
                    var result = new TtlCompileResult(false, document, parseContext);
                    result.Errors.Add(e.ToError(default(BlockPosition)));
                    return result;
                }
            }
            catch (TemplateParseException e)
            {
                var result = new TtlCompileResult(false, document, null);
                result.Errors.Add(e.ToError(default(BlockPosition)));
                return result;
            }
            catch (Exception e)
            {
                var result = new TtlCompileResult(false, document, null);
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
                    CompileResult = new TtlCompileResult(false, document, null);
                    CompileResult.Errors.Add(ex.ToError(default(BlockPosition)));
                }
            }
        }

        public static void Configure(Assembly startupAssembly)
        {
            AssemblyHelper.Configure(startupAssembly);
        }
    }
}