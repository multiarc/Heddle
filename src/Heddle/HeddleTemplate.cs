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
    /// Use this class to operate with engine, parse source template, make replace with data and generate result string.
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
        // Phase 4 D1 disposal state: plain ints accessed only through Interlocked/Volatile (a `volatile` field
        // passed by ref to Interlocked raises CS0420, and the atomics already provide the fences). Never mix a
        // plain ++/--/read with these fields.
        // 0 = live, 1 = disposal requested (fences new renders out; set once via Interlocked.Exchange).
        private int _disposeAfterComplete;
        // Active render count; mutated only via Interlocked.Increment/Decrement, read via Volatile.Read.
        private int _runners;
        // One-shot CAS guard: the actual resource teardown runs exactly once across the explicit-Dispose,
        // deferred-last-exit, and finalizer paths.
        private int _teardownDone;
        // Phase 1 D5: serializes every publisher (watcher callback / Recompile / first compile) through the store
        // block's capture-superseded → field-writes → enqueue → drain sequence, and guards every drain site.
        // Renders NEVER take this lock — they read the volatile snapshot only (off the render hot path).
        private readonly object _publishGate = new object();
        // Phase 1 D5: superseded runtime documents awaiting release. null for every template that never reloads
        // (zero cost); created and enqueued only under _publishGate; the volatile null-check lets ExitRender
        // skip the lock entirely on the common non-reloading path.
        private volatile ConcurrentQueue<RuntimeDocument> _supersededDocs;
        // Phase 1 D1/D3: recompile inputs captured at the first file compile so a watcher reload reconstructs a
        // from-scratch-equivalent fresh context. _watchModelType is captured PRE-compile (the original ctor
        // RootScopeType) because a root @model reassigns RootScopeType in place during the compile; OutputProfile
        // is deliberately NOT captured — re-derived from _watchOptions + the freshly-parsed @profile each reload.
        private string _watchFileName;
        private TemplateOptions _watchOptions;
        private ExType _watchModelType;
        private string _watchControllerName;
        // B2: the effective output encoder (TemplateOptions.Encoder). Resolved once at compile (dynamic tier) or from
        // the precompiled-adapter ctor, then stamped on each render's sink. null = the legacy WebUtility path.
        private System.Text.Encodings.Web.TextEncoder _encoder;
        // C1: the effective render budget (TemplateOptions.RenderBudget). Resolved once at compile (dynamic tier) or
        // carried on the precompiled-adapter ctor. null = unlimited: the budget wrapper is never created (zero cost).
        private RenderBudget _renderBudget;
        // P4-Q2: the effective TemplateOptions.ValidateModelType (opt-in Release model-type guard). Resolved once
        // at compile on the store path; read (never written) on the render path. DEBUG always validates.
        private bool _validateModelType;
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

        // Phase 7 D7 — precompiled adapter mode: a resolver hit binds the entry's generated root strategy directly,
        // with no parse and no Roslyn compile. CompileResult reports success so host code cannot tell the difference;
        // the strategy self-manages any ScopeLocals frame (WithLocalsFrame is baked into the root, D5).
        // Read by the model-type guard in Render — always in DEBUG, and in Release by the opt-in
        // TemplateOptions.ValidateModelType guard (P4-Q2). A precompiled strategy carries no CompileContext
        // ScopeType to check against, so the guard is skipped on the precompiled path in both configurations.
        private readonly bool _precompiled;

        internal HeddleTemplate(IProcessStrategy precompiledStrategy,
            System.Text.Encodings.Web.TextEncoder encoder = null, RenderBudget renderBudget = null)
        {
            if (precompiledStrategy == null)
                throw new ArgumentNullException(nameof(precompiledStrategy));
            _processStrategy = precompiledStrategy;
            _encoder = encoder;   // B2: carry the request's encoder onto the precompiled-adapter render (resolver path)
            _renderBudget = renderBudget;   // C1: carry the request's budget onto the precompiled-adapter render
            _precompiled = true;
            CompileResult = new HeddleCompileResult(true, null, null);
        }

        public bool Empty => _runtimeDocument?.Empty ?? true;
        public bool Compiled => _runtimeDocument != null;

        public CompileContext Context => _context.CompileContext;

        #region IDisposable Members

        // Phase 4 D1: non-blocking, idempotent dispose that is safe to call concurrently with active renders.
        // Setting the flag first orders against EnterRender's increment-then-re-read: any render still executing
        // observed the flag clear after its increment, so that increment precedes the runners read below and
        // Dispose defers teardown to the last ExitRender. No spin, no wait, no drain.
        public void Dispose()
        {
            Interlocked.Exchange(ref _disposeAfterComplete, 1); // fence out new renders; idempotent
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
                // Phase 1 D5: when the last render exits, release any superseded documents a reload parked.
                // The volatile null-check keeps non-reloading templates lock-free on this path.
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
                return; // already torn down — idempotent across Dispose / deferred exit / finalizer
            // Phase 1 D5: the gate makes teardown disposal and the store block's post-Teardown guard mutually
            // exclusive — a late watcher callback either observes the dispose flag and discards its fresh
            // artifact, or publishes fully before this disposal runs (never a leak, never a resurrection).
            lock (_publishGate)
            {
                _watcher?.Dispose();
                _runtimeDocument?.Dispose();
                // Drain-dispose the remaining superseded docs. Teardown never runs while a render executes
                // (Phase 4 proof), so DrainLocked's _runners gate reads 0 here and the queue empties.
                DrainLocked();
            }
            GC.SuppressFinalize(this);
        }

        // Phase 1 D5(b): dispose queued superseded documents iff no render can hold one. Caller holds _publishGate,
        // so no concurrent publisher can append while the loop runs (the drain-TOCTOU closure).
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
        /// Generates result string (source template replaced with data). Data non-serialized
        /// </summary>
        /// <param name="data">Input object</param>
        /// <param name="callerData"></param>
        /// <param name="chained"></param>
        /// <returns>Generated string</returns>
        public string Generate(object data, object chained = null, object callerData = null)
        {
            // The string path wraps the shared render core (phase 8 D11/WI3) with what only it needs: a seeded
            // ScopeRenderer, the adaptive high-water read/update, and ToString/Clear. The core is bit-identical to the
            // pre-phase inline body; sink renders never read or update the high-water state (D4).
#if NET8_0_OR_GREATER
            var renderer = new ScopeRenderer(_maxLength);
#else
            var renderer = new ScopeRenderer(_maxElementCount);
#endif
            renderer.SetOutputEncoder(_encoder);   // B2: stamp the effective encoder on the sink
            // C1: wrap in the budget seam only when a budget is configured — the null path keeps the bare sink (no
            // wrapper, no allocation). ToString/Clear/high-water still read the underlying ScopeRenderer directly.
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
        /// Renders into a <see cref="TextWriter"/> with no full-output string materialization (phase 8 D3). The caller
        /// owns the writer: nothing is flushed or disposed. Same compile-guard exceptions as the string path;
        /// <see cref="ArgumentNullException"/> when <paramref name="writer"/> is null; a null model remains legal.
        /// </summary>
        public void Generate(object data, TextWriter writer, object chained = null, object callerData = null)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            var renderer = new TextWriterScopeRenderer(writer);
            renderer.SetOutputEncoder(_encoder);   // B2: stamp the effective encoder on the sink
            Render(data, chained, callerData, _renderBudget == null ? (IScopeRenderer)renderer : new BudgetedRenderer(renderer, _renderBudget));
        }

        /// <summary>
        /// Renders UTF-8 into an <see cref="System.Buffers.IBufferWriter{T}"/> of <see cref="byte"/> (e.g. a
        /// <c>PipeWriter</c>) with no full-output materialization (phase 8 D3). The caller owns the writer and any
        /// <c>FlushAsync</c>: nothing is flushed, completed, or disposed. Same compile-guard exceptions as the string
        /// path; <see cref="ArgumentNullException"/> when <paramref name="writer"/> is null; a null model remains legal.
        /// </summary>
        public void Generate(object data, System.Buffers.IBufferWriter<byte> writer, object chained = null,
            object callerData = null)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            var renderer = new Utf8ScopeRenderer(writer);
            renderer.SetOutputEncoder(_encoder);   // B2: stamp the effective encoder on the sink
            Render(data, chained, callerData, _renderBudget == null ? (IScopeRenderer)renderer : new BudgetedRenderer(renderer, _renderBudget));
        }

        /// <summary>
        /// The shared render core (phase 8 D11): compile guards, the model-type check (always in DEBUG; in Release
        /// when <see cref="TemplateOptions.ValidateModelType"/> is opted in), the dispose guard, the
        /// runner/dispose bookkeeping, root <see cref="Scope"/> construction over the supplied renderer (including
        /// phase 3's root-frame provisioning), and <c>_processStrategy.Render</c>. Three callers: the string
        /// <see cref="Generate(object,object,object)"/>, the two sink overloads, and <c>PartialExtension.RenderData</c>
        /// (streaming partials, D11).
        /// </summary>
        internal void Render(object data, object chained, object callerData, IScopeRenderer renderer)
        {
            // Liveness guard only (was this template ever compiled?) — a plain volatile read, not the swap snapshot.
            if (_processStrategy == null)
            {
                if (CompileResult == null)
                    throw new TemplateInitException("Compile first");
                throw new TemplateCompileException(CompileResult.ErrorList);
            }

            EnterRender();
            try
            {
                // Phase 1 D5: the document snapshot is taken AFTER EnterRender's increment so the superseded-doc
                // drain's _runners gate provably covers every render that could hold it. One volatile read of the
                // document yields a coherent (Strategy, NeedsLocals) pair; _context is read once so the model-type
                // guard and the render share one coherent context.
                var doc = _runtimeDocument;
                var ctx = _context;
                var strategy = doc != null ? doc.Strategy : _processStrategy; // precompiled: doc == null, strategy is the field
                var validateModelType = _validateModelType;
#if DEBUG
                validateModelType = true; // DEBUG always validates (historical guard); Release honors the opt-in
#endif
                if (validateModelType && !_precompiled && data != null && !ctx.ScopeType.Type.IsType(data))
                {
                    throw new TemplateProcessingException
                        (string.Format
                            (CultureInfo.InvariantCulture, "Type mismatch. Need {0} but got {1}",
                                ctx.ScopeType.Type?.FullName ?? ctx.ScopeType.ToString(),
                                data.GetType().FullName));
                }
                var scope = new Scope(data, callerData, data, chained, renderer, null,
                    (doc?.NeedsLocals ?? false) ? new ScopeLocals() : null);
                strategy.Render(scope);
            }
            finally
            {
                ExitRender();
            }
        }

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

            string document = null;
            try
            {
                _reader = new FileReader(context.Options);
                document = _reader.ReadEntireFile();
                // Phase 1 D3: capture the ORIGINAL root model type BEFORE the first compile — a root @model
                // directive reassigns RootScopeType in place during the compile (ModelExtension), and a reload
                // must re-derive the directive from the freshly-parsed source, not carry the flipped type.
                if (context.Options.EnableFileChangeCheck)
                    _watchModelType = context.RootScopeType;
                CompileResult = Compile(new CompileScope(context), document);
                if (context.Options.EnableFileChangeCheck)
                {
                    // Phase 1 D1: the watch filter derives from the SAME name the reader reads
                    // (RootPath / (TemplateName + FileNamePostfix)) so the two can never diverge again.
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
                    // Phase 1 D2: arm the watcher (it raises nothing until enabled). Armed even when the first
                    // compile soft-failed (template errors) so an edit-to-fix save recovers the template; a
                    // thrown read failure lands in the catch below and never reaches this statement.
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

        // Engine-internal compile with an explicit context. Used by the C# code-generation meta-templates,
        // which emit raw C# source and must render under OutputProfile.Text with directive-line trimming off,
        // independent of the engine's public output defaults.
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
            // Isolate the dry run (P4-Q1): reconstruct from the caller's options + root model type (+ ControllerName),
            // and mirror the caller's *current* ScopeType and OutputProfile so the probe compiles the same context
            // state a real Compile(context) would — even if the caller mutated ScopeType (so it diverges from
            // RootScopeType) or flipped OutputProfile after construction. The caller's own CompileContext is never
            // mutated (Compiled / DelayedTemplates / CompileErrors), so a subsequent real Compile of that context
            // finalizes from scratch.
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
                    bool stored = false;   // true once the artifact is owned elsewhere (published to fields) → the finally must not dispose it
                    try
                    {
                        if (compileScope.CompileErrors.Count > 0)
                        {
                            var result = new HeddleCompileResult(false, document, parseContext);
                            result.Errors.AddRange(compileScope.CompileErrors);
                            return result;                       // finally disposes compileScope + rtdoc
                        }
                        compileScope.Compile();                  // P4-Q1 parity: CompleteInit + FullCSharp Roslyn, in dry runs too — MAY THROW
                        if (compileScope.CompileErrors.Count > 0)
                        {
                            var result = new HeddleCompileResult(false, document, parseContext);
                            result.Errors.AddRange(compileScope.CompileErrors);
                            return result;                       // finally disposes
                        }
                        if (!simulate)
                        {
                            // Phase 1 D5(a): serialize all publishers (watcher callbacks, Recompile, first
                            // compile) so the field group publishes coherently and each superseded document is
                            // enqueued exactly once. Renders never take this lock.
                            lock (_publishGate)
                            {
                                if (Volatile.Read(ref _disposeAfterComplete) != 0)
                                {
                                    // Dispose()/Teardown() already ran (or is committed): do not publish onto a
                                    // dead template — release the fresh artifact instead of leaking it.
                                    compileScope.Dispose();
                                    rtdoc?.Dispose();
                                    stored = true;               // the finally must NOT dispose again
                                    return new HeddleCompileResult(true, document, parseContext);
                                }
                                var superseded = _runtimeDocument;   // the document about to be replaced (null on first compile)
                                _context = compileScope;
                                _document = optimizedDocument;
                                _runtimeDocument = rtdoc;            // volatile publish — document first
                                _processStrategy = rtdoc?.Strategy;  // volatile publish — then strategy
                                _encoder = compileScope.CompileContext.Options.Encoder;   // B2: resolve the effective encoder once
                                _renderBudget = compileScope.CompileContext.Options.RenderBudget;   // C1: resolve the budget once
                                _validateModelType = compileScope.CompileContext.Options.ValidateModelType;   // P4-Q2: resolve the opt-in guard once
                                if (superseded != null)
                                {
                                    _supersededDocs = _supersededDocs ?? new ConcurrentQueue<RuntimeDocument>();
                                    _supersededDocs.Enqueue(superseded);
                                }
                                DrainLocked();                       // dispose queued docs iff no render can hold them
                                stored = true;                       // retained by the template — do NOT dispose in finally
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

        // Phase 1 D4: sender is the template on every public event (never the internal watcher). Delete keeps the
        // last-good document renderable (no reload); a rename recompiles only when it lands ON the watched name
        // (the atomic-save idiom); a create/re-create raises OnFileChanged (no OnFileCreated exists) and reloads.
        private void FileDeleted(object sender, FileSystemEventArgs e)
        {
            OnFileDeleted?.Invoke(this, e);     // keep the last-good document renderable; do not reload (P1-Q2)
        }

        private void FileRenamed(object sender, RenamedEventArgs e)
        {
            OnFileRenamed?.Invoke(this, e);
            if (string.Equals(Path.GetFileName(e.FullPath), _watchFileName, StringComparison.OrdinalIgnoreCase))
                Reload();                       // atomic-save: the rename landed the new content ON the target
        }

        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(this, e);
            Reload();
        }

        private void FileCreated(object sender, FileSystemEventArgs e)
        {
            OnFileChanged?.Invoke(this, e);     // no OnFileCreated in the public surface; a (re)create is a content change
            Reload();
        }

        // Phase 1 D3 + D5: recompile the watched file into a FRESH context reconstructed from the captured
        // original options + pre-@model root model type (+ ControllerName, which the public ctor drops), exactly
        // as a from-scratch compile would. OutputProfile is re-derived from _watchOptions + the freshly-parsed
        // @profile directive — never carried across a reload. An empty/whitespace read is a mid-write-truncation
        // no-op that keeps last-good published; a failed compile or read keeps last-good and surfaces on
        // CompileResult.
        private void Reload()
        {
            string document = null;
            try
            {
                document = _reader.ReadEntireFile();
                if (!string.IsNullOrWhiteSpace(document))
                    CompileResult = Compile(new CompileScope(
                        new CompileContext(_watchOptions, _watchModelType)
                        {
                            ControllerName = _watchControllerName,
                        }), document);
                // else: empty/whitespace save — no-op, last-good stays published (D4 empty-content policy)
            }
            catch (Exception ex)
            {
                CompileResult = new HeddleCompileResult(false, document, null);
                CompileResult.Errors.Add(ex.ToError(default(BlockPosition)));   // last-good stays published
            }
        }

        public static void Configure(Assembly startupAssembly)
        {
            AssemblyHelper.Configure(startupAssembly);
        }
    }
}