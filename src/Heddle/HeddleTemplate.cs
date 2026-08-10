using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Native;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Heddle
{
    /// <summary>
    /// Parse, compile, and render a Heddle template against a data model.
    /// </summary>
    public sealed class HeddleTemplate : IHeddleTemplate
    {
        private CompileScope _context;
        public HeddleCompileResult CompileResult { get; private set; }
        private FileReader _reader;
        private FileSystemWatcher _watcher;
        private volatile RuntimeDocument _runtimeDocument;
        private volatile IProcessStrategy _processStrategy;
        private string _document;
        // Disposal state: never mix plain ++/--/read with these — use only Interlocked/Volatile.
        // 0 = live, 1 = disposal requested.
        private int _disposeAfterComplete;
        private int _runners;
        // Teardown runs exactly once across Dispose, deferred-last-exit, and finalizer paths.
        private int _teardownDone;
        // Serializes publishers (watcher callbacks, Recompile, first compile) so fields publish coherently.
        // Renders never take this lock — they read the volatile snapshot only (off the render hot path).
        private readonly object _publishGate = new object();
        // Superseded documents awaiting release; null for non-reloading templates (zero cost).
        // Volatile null-check lets ExitRender skip the lock on the common path.
        private volatile ConcurrentQueue<RuntimeDocument> _supersededDocs;
        // _watchModelType is captured PRE-compile (before a root @model directive flips it);
        // OutputProfile is deliberately NOT captured — re-derived from the freshly-parsed @profile each reload.
        private string _watchFileName;
        private TemplateOptions _watchOptions;
        private ExType _watchModelType;
        private string _watchControllerName;
        // Resolved once at compile (or from precompiled-adapter ctor); null = legacy WebUtility path.
        private System.Text.Encodings.Web.TextEncoder _encoder;
        // Resolved once at compile (or from precompiled-adapter ctor); null = unlimited.
        private RenderBudget _renderBudget;
#if NET8_0_OR_GREATER
         private volatile int _maxLength;
#else
        private volatile int _maxElementCount;
#endif

        public HeddleTemplate(TemplateOptions options) : this(new CompileContext(options))
        {
        }

        public HeddleTemplate(TemplateOptions options, ExType modelType) : this(
            new CompileContext(options,
                modelType))
        {
        }

        public HeddleTemplate(CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            CompileResult = Compile(context);
        }

        public HeddleTemplate()
        {
        }

        public HeddleTemplate(string document, CompileContext context = null)
        {
            CompileResult = Compile(new CompileScope(context ?? new CompileContext()), document);
        }

        // Precompiled adapter mode: resolvers bind a strategy directly (no parse/compile). Not readonly: the
        // precompiled child seam below puts an instance the engine's own compile path created into this mode.
        private bool _precompiled;

        // The request options a precompiled-adapter render runs under. Only late-bound function sites read them
        // (through PrecompiledRuntime's ambient), and only the adapter needs to carry them: every other
        // precompiled entry point IS PrecompiledRuntime, which establishes the ambient itself.
        private readonly TemplateOptions _precompiledOptions;

        /// <summary>The type the bound strategy's generated code was compiled against — the manifest's
        /// <c>ModelType</c> — or null where there is nothing to check: an untyped entry (whose recorded type is
        /// <c>object</c>, which admits every value) or a hand-written manifest that declines to name one. Held
        /// pre-reduced so the render gate costs one null compare on the untyped path.</summary>
        private readonly Type _precompiledModelType;

        internal HeddleTemplate(IProcessStrategy precompiledStrategy,
            System.Text.Encodings.Web.TextEncoder encoder = null, RenderBudget renderBudget = null,
            TemplateOptions options = null, Type modelType = null)
        {
            if (precompiledStrategy == null)
                throw new ArgumentNullException(nameof(precompiledStrategy));
            _processStrategy = precompiledStrategy;
            _encoder = encoder;
            _renderBudget = renderBudget;
            _precompiledOptions = options;
            _precompiledModelType = modelType == typeof(object) ? null : modelType;
            _precompiled = true;
            CompileResult = new HeddleCompileResult(true, null, null);
        }

        public bool Empty => _runtimeDocument?.Empty ?? true;
        public bool Compiled => _runtimeDocument != null;

        public CompileContext Context => _context.CompileContext;

        #region IDisposable Members

        // Non-blocking, idempotent dispose that is safe to call concurrently with active renders.
        // Setting the flag first orders against EnterRender's increment-then-re-read.
        public void Dispose()
        {
            Interlocked.Exchange(ref _disposeAfterComplete, 1);
            if (Volatile.Read(ref _runners) == 0)
                Teardown();
        }

        ~HeddleTemplate()
        {
            Teardown();
        }

        private void EnterRender()
        {
            if (Volatile.Read(ref _disposeAfterComplete) != 0)
                throw new ObjectDisposedException($"{GetType()} Disposed");
            Interlocked.Increment(ref _runners);
            // Dispose() may have set the flag between the read and the increment; if so, back out.
            if (Volatile.Read(ref _disposeAfterComplete) != 0)
            {
                ExitRender();
                throw new ObjectDisposedException($"{GetType()} Disposed");
            }
        }

        private void ExitRender()
        {
            if (Interlocked.Decrement(ref _runners) == 0)
            {
                // Volatile null-check keeps non-reloading templates lock-free on this path.
                if (_supersededDocs != null)
                    lock (_publishGate)
                    {
                        DrainLocked();
                    }
                if (Volatile.Read(ref _disposeAfterComplete) != 0)
                    Teardown();
            }
        }

        private void Teardown()
        {
            if (Interlocked.CompareExchange(ref _teardownDone, 1, 0) != 0)
                return; // already torn down — idempotent
            // Dispose watcher OUTSIDE _publishGate: on .NET Framework, nesting deadlocks (netfx-only).
            _watcher?.Dispose();
            lock (_publishGate)
            {
                _runtimeDocument?.Dispose();
                DrainLocked();
            }
            GC.SuppressFinalize(this);
        }

        // Dispose queued documents iff no render can hold one (caller holds _publishGate).
        private void DrainLocked()
        {
            var q = _supersededDocs;
            if (q == null || Volatile.Read(ref _runners) != 0)
                return; // a render may still hold a superseded doc; a later publish / ExitRender / Teardown drains
            while (q.TryDequeue(out var doc))
                doc?.Dispose();
        }


        #endregion

        /// <summary>
        /// Render the template against data and return the result as a string.
        /// </summary>
        /// <param name="data">The root model object.</param>
        /// <param name="chained">Optional chained context.</param>
        /// <param name="callerData">Optional caller context.</param>
        /// <returns>The rendered output.</returns>
        public string Generate(object data, object chained = null, object callerData = null)
        {
            // String path wraps render core with high-water tracking; sink renders skip that.
#if NET8_0_OR_GREATER
            var renderer = new ScopeRenderer(_maxLength);
#else
            var renderer = new ScopeRenderer(_maxElementCount);
#endif
            renderer.SetOutputEncoder(_encoder);
            // Wrap in the budget seam only when configured — null path keeps the bare sink (zero cost).
            Render(data, chained, callerData, _renderBudget == null ? (IScopeRenderer)renderer : new BudgetedRenderer(renderer, _renderBudget));
#if NET8_0_OR_GREATER
            var newMax = Math.Max(_maxLength, renderer.TotalLength);
            if (newMax > _maxLength)
            {
                newMax = (int)Math.Min((long)newMax * 110 / 100, int.MaxValue / 2); //10% length extra margin
                _maxLength = newMax;
            }
#else
            var newMax = Math.Max(_maxElementCount, renderer.TotalCount);
            if (newMax > _maxElementCount)
            {
                newMax = newMax * 110 / 100; //10% count extra margin
                _maxElementCount = newMax;
            }
#endif
            var result = renderer.ToString();
            renderer.Clear();
            return result;
        }

        /// <summary>
        /// Renders into a <see cref="TextWriter"/> with no full-output string materialization. The caller
        /// owns the writer: nothing is flushed or disposed. Same compile-guard exceptions as the string path;
        /// <see cref="ArgumentNullException"/> when <paramref name="writer"/> is null; a null model remains legal.
        /// </summary>
        public void Generate(object data, TextWriter writer, object chained = null, object callerData = null)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            var renderer = new TextWriterScopeRenderer(writer);
            renderer.SetOutputEncoder(_encoder);
            Render(data, chained, callerData, _renderBudget == null ? (IScopeRenderer)renderer : new BudgetedRenderer(renderer, _renderBudget));
        }

        /// <summary>
        /// Renders UTF-8 into an <see cref="System.Buffers.IBufferWriter{T}"/> of <see cref="byte"/> (e.g. a
        /// <c>PipeWriter</c>) with no full-output materialization. The caller owns the writer and any
        /// <c>FlushAsync</c>: nothing is flushed, completed, or disposed. Same compile-guard exceptions as the string
        /// path; <see cref="ArgumentNullException"/> when <paramref name="writer"/> is null; a null model remains legal.
        /// </summary>
        public void Generate(object data, System.Buffers.IBufferWriter<byte> writer, object chained = null,
            object callerData = null)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            var renderer = new Utf8ScopeRenderer(writer);
            renderer.SetOutputEncoder(_encoder);
            Render(data, chained, callerData, _renderBudget == null ? (IScopeRenderer)renderer : new BudgetedRenderer(renderer, _renderBudget));
        }

        /// <summary>
        /// Shared render core: compile/dispose guards, model-type check, runner bookkeeping, root <see cref="Scope"/> construction.
        /// </summary>
        internal void Render(object data, object chained, object callerData, IScopeRenderer renderer)
        {
            // Liveness guard only — plain volatile read, not the swap snapshot.
            if (_processStrategy == null)
            {
                if (CompileResult == null)
                    throw new TemplateInitException("Compile first");
                throw new TemplateCompileException(CompileResult.ErrorList);
            }

            EnterRender();
            try
            {
                // Snapshot taken AFTER EnterRender's increment so _runners gate covers every render that could hold it.
                var doc = _runtimeDocument;
                var ctx = _context;
                var strategy = doc != null ? doc.Strategy : _processStrategy;
                // A model the template cannot accept is refused here, as a Heddle fault. Without this the value
                // reaches the compiled accessor's cast and escapes as a raw InvalidCastException, which is not the
                // shape any other render fault has. One instance check per render — not per processor, so the
                // recursive path is untouched.
                if (!_precompiled && data != null && !ctx.ScopeType.Type.IsType(data))
                {
                    throw ModelTypeMismatch(ctx.ScopeType.Type?.FullName ?? ctx.ScopeType.ToString(), data);
                }

                // The adapter has a compile-time model type after all — the manifest records the type the generated
                // code was compiled against, and the generated body casts to exactly that type. Skipping the check
                // here made the tier that is meant to be byte-identical answer a wrong model with a raw
                // InvalidCastException where this one raises a Heddle fault, and, for a body that reads no member,
                // render a page the dynamic tier refuses outright.
                if (_precompiled && data != null && _precompiledModelType != null &&
                    !_precompiledModelType.IsType(data))
                {
                    throw ModelTypeMismatch(_precompiledModelType.FullName ?? _precompiledModelType.ToString(), data);
                }

                var scope = new Scope(data, callerData, data, chained, renderer, null,
                    (doc?.NeedsLocals ?? false) ? new ScopeLocals() : null);
                if (_precompiledOptions == null)
                {
                    strategy.Render(scope);
                }
                else
                {
                    // The adapter drives the generated strategy itself, so it — not PrecompiledRuntime — is what
                    // makes the request's registry the one a late-bound function site binds against.
                    var previousAmbient = Precompiled.PrecompiledRuntime.EnterAmbient(_precompiledOptions);
                    try
                    {
                        strategy.Render(scope);
                    }
                    finally
                    {
                        Precompiled.PrecompiledRuntime.LeaveAmbient(previousAmbient);
                    }
                }
            }
            finally
            {
                ExitRender();
            }
        }

        /// <summary>The one model-type fault, raised identically by both tiers: the dynamic path names the type its
        /// <c>@model</c> directive (or the host's context) pinned, the precompiled adapter names the type the
        /// manifest says the generated code was compiled against, and a caller cannot tell which tier answered.</summary>
        private static TemplateProcessingException ModelTypeMismatch(string needed, object data) =>
            new TemplateProcessingException(string.Format(CultureInfo.InvariantCulture,
                "Type mismatch. Need {0} but got {1}", needed, data.GetType().FullName));

        public HeddleCompileResult Recompile(ExType newModelType)
        {
            CompileResult = Compile(new CompileScope(new CompileContext(newModelType)), _document);
            return CompileResult;
        }

        public HeddleCompileResult Recompile(string newDocument, CompileContext context = null)
        {
            CompileResult = Compile(new CompileScope(context ?? new CompileContext()), newDocument);
            return CompileResult;
        }

        public HeddleCompileResult Compile(CompileContext context)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");

            // The child-template seam. Named compiles are the only ones that reach here, so this is the door a
            // [ChildTemplateHost] hook walks through when it compiles the template its body named; when a
            // precompiled call site has armed the supply, the child is bound the precompiled way instead of being
            // read off disk. Nothing is armed on the dynamic tier, so that path pays one thread-static read.
            if (context != null && Core.PrecompiledChildSupply.TryConsume(context, out var suppliedChild))
            {
                _processStrategy = suppliedChild;
                _precompiled = true;
                CompileResult = new HeddleCompileResult(true, null, null);
                return CompileResult;
            }

            string document = null;
            try
            {
                _reader = new FileReader(context.Options);
                document = _reader.ReadEntireFile();
                // Capture RootScopeType PRE-compile; a root @model directive flips it during compilation.
                if (context.Options.EnableFileChangeCheck)
                    _watchModelType = context.RootScopeType;
                CompileResult = Compile(new CompileScope(context), document);
                if (context.Options.EnableFileChangeCheck)
                {
                    // Watch filter must use the exact name the reader reads (RootPath / (TemplateName + FileNamePostfix)).
                    var watchedFile = _reader.GetFileName();
                    var directory = Path.GetDirectoryName(watchedFile);
                    _watchFileName = Path.GetFileName(watchedFile);
                    _watchOptions = context.Options;
                    _watchControllerName = context.ControllerName;
                    _watcher = new FileSystemWatcher(directory)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                        Filter = _watchFileName
                    };
                    _watcher.Changed += FileChanged;
                    _watcher.Created += FileCreated;
                    _watcher.Deleted += FileDeleted;
                    _watcher.Renamed += FileRenamed;
                    // Arm even when first compile soft-fails (template errors) so edit-to-fix saves recover the template.
                    _watcher.EnableRaisingEvents = true;
                }
            }
            catch (Exception e)
            {
                CompileResult = new HeddleCompileResult(false, document, null);
                CompileResult.Errors.Add(e.ToError(default(BlockPosition)));
            }
            return CompileResult;
        }

        public HeddleCompileResult Compile(string document, ExType modelType = null)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            return Compile(new CompileScope(new CompileContext(modelType)), document);
        }

        // Engine-internal compile: used by C# code-generation meta-templates for raw source output.
        internal HeddleCompileResult Compile(string document, CompileContext context)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            return Compile(new CompileScope(context), document);
        }

        public HeddleCompileResult TryCompilation(CompileContext context)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            var reader = new FileReader(context.Options);
            var document = reader.ReadEntireFile();
            // Mirror caller's mutated ScopeType/OutputProfile; caller's CompileContext never mutates, so real Compile finalizes from scratch.
            var probe = new CompileContext(context.Options, context.RootScopeType)
            {
                ScopeType = context.ScopeType,          // mirror a caller-mutated model type; setter leaves the already-non-null RootScopeType intact
                OutputProfile = context.OutputProfile,  // mirror a caller-flipped @profile default
                ControllerName = context.ControllerName,
            };
            return Compile(new CompileScope(probe), document, true);
        }

        public HeddleCompileResult TryCompilation(string document, TemplateOptions options = null, ExType modelType = null)
        {
            if (_runtimeDocument != null)
                throw new TemplateInitException("Template already compiled.");
            return Compile(new CompileScope(new CompileContext(options ?? new TemplateOptions(),
                modelType)), document, true);
        }

        private HeddleCompileResult Compile(CompileScope compileScope, string document, bool simulate = false)
        {
            try
            {
                var parseContext = DocumentParser.Parse(document, compileScope.CompileContext, out var optimizedDocument);
                try
                {
                    RuntimeDocument rtdoc = HeddleCompiler.Compile(optimizedDocument, compileScope,
                        parseContext, null);
                    bool stored = false;   // true once published to fields; finally must not dispose
                    try
                    {
                        if (compileScope.CompileErrors.Count > 0)
                        {
                            var result = new HeddleCompileResult(false, document, parseContext);
                            result.Errors.AddRange(compileScope.CompileErrors);
                            return result;
                        }
                        compileScope.Compile();   // MAY THROW
                        if (compileScope.CompileErrors.Count > 0)
                        {
                            var result = new HeddleCompileResult(false, document, parseContext);
                            result.Errors.AddRange(compileScope.CompileErrors);
                            return result;
                        }
                        if (!simulate)
                        {
                            // Serialize publishers (watcher callbacks, Recompile, first compile) so fields publish coherently.
                            // Renders never take this lock.
                            lock (_publishGate)
                            {
                                if (Volatile.Read(ref _disposeAfterComplete) != 0)
                                {
                                    // Dispose()/Teardown() already ran: do not publish onto a dead template.
                                    compileScope.Dispose();
                                    rtdoc?.Dispose();
                                    stored = true;
                                    return new HeddleCompileResult(true, document, parseContext);
                                }
                                var superseded = _runtimeDocument;
                                _context = compileScope;
                                _document = optimizedDocument;
                                _runtimeDocument = rtdoc;
                                _processStrategy = rtdoc?.Strategy;
                                _encoder = compileScope.CompileContext.Options.Encoder;
                                _renderBudget = compileScope.CompileContext.Options.RenderBudget;
                                if (superseded != null)
                                {
                                    _supersededDocs = _supersededDocs ?? new ConcurrentQueue<RuntimeDocument>();
                                    _supersededDocs.Enqueue(superseded);
                                }
                                DrainLocked();
                                stored = true;
                            }
                        }
                        return new HeddleCompileResult(true, document, parseContext);
                    }
                    finally
                    {
                        if (!stored)
                        {
                            compileScope.Dispose();
                            rtdoc?.Dispose();
                        }
                    }
                }
                catch (TemplateCompileException e)
                {
                    var result = new HeddleCompileResult(false, document, parseContext);
                    result.Errors.AddRange(e.Errors);
                    return result;
                }
                catch (Exception e)
                {
                    var result = new HeddleCompileResult(false, document, parseContext);
                    result.Errors.Add(e.ToError(default(BlockPosition)));
                    return result;
                }
            }
            catch (TemplateParseException e)
            {
                var result = new HeddleCompileResult(false, document, null);
                result.Errors.Add(e.ToError(default(BlockPosition)));
                return result;
            }
            catch (Exception e)
            {
                var result = new HeddleCompileResult(false, document, null);
                result.Errors.Add(e.ToError(default(BlockPosition)));
                return result;
            }
        }

        public event FileSystemEventHandler OnFileDeleted;

        public event RenamedEventHandler OnFileRenamed;

        public event FileSystemEventHandler OnFileChanged;

        // Public events use template as sender (not the watcher); delete keeps last-good (no reload);
        // rename/create/recreate recompiles; no public OnFileCreated.
        private void FileDeleted(object sender, FileSystemEventArgs e)
        {
            OnFileDeleted?.Invoke(this, e);
        }

        private void FileRenamed(object sender, RenamedEventArgs e)
        {
            OnFileRenamed?.Invoke(this, e);
            if (string.Equals(Path.GetFileName(e.FullPath), _watchFileName, StringComparison.OrdinalIgnoreCase))
                Reload();
        }

        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(this, e);
            Reload();
        }

        private void FileCreated(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(this, e);
            Reload();
        }

        // Recompile from fresh context (captured options + pre-@model type); OutputProfile re-derives.
        private void Reload()
        {
            string document = null;
            try
            {
                document = ReadForReload();
                if (!string.IsNullOrWhiteSpace(document))
                    CompileResult = Compile(new CompileScope(
                        new CompileContext(_watchOptions, _watchModelType)
                        {
                            ControllerName = _watchControllerName,
                        }), document);
                // Empty/whitespace save — no-op, last-good stays published.
            }
            catch (Exception ex)
            {
                CompileResult = new HeddleCompileResult(false, document, null);
                CompileResult.Errors.Add(ex.ToError(default(BlockPosition)));
            }
        }

        // Watcher event is one-shot: transient sharing violations must be retried briefly before giving up.
        private string ReadForReload()
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return _reader.ReadEntireFile();
                }
                catch (Exception e) when (attempt < 10 && e.InnerException is IOException)
                {
                    Thread.Sleep(25);
                }
            }
        }

        /// <summary>
        /// Makes an assembly's types visible to engine type resolution and offers its assembly-level
        /// <c>[ExportExtensions]</c> to the extension registry. The engine loads nothing on its own, so an assembly
        /// providing extensions or <c>@model</c> types that the host has not loaded from disk must be registered here.
        /// Idempotent per assembly; repeatable, so a host may register in whatever order it establishes precedence.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is null.</exception>
        /// <exception cref="Exceptions.TemplateOverrideException">Two unrelated types claim one extension name.</exception>
        public static void Register(Assembly assembly)
        {
            AssemblyHelper.Register(assembly);
        }

        /// <summary>Registers the host's startup assembly. Equivalent to <see cref="Register"/>.</summary>
        public static void Configure(Assembly startupAssembly)
        {
            AssemblyHelper.Register(startupAssembly);
        }
    }
}