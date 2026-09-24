using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Heddle.LanguageServices.Completion;
using Heddle.Runtime.Expressions;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Document manager and analysis entry point. One instance per workspace; thread-safe: public
    /// members may be called from any thread, analyses are serialized per document, results are immutable
    /// snapshots. Runs the engine pipeline directly and projects it; model typing and host-registration
    /// knowledge come from the configured assemblies (model ALC, extension scan, function exports).
    /// </summary>
    public sealed class HeddleLanguageService : IDisposable
    {
        private readonly HeddleLanguageServiceOptions _options;
        private readonly DocumentAnalyzer _analyzer;
        private readonly ModelAssemblyManager _modelManager = new ModelAssemblyManager();
        private readonly ConcurrentDictionary<string, DocumentAnalysis> _analyses =
            new ConcurrentDictionary<string, DocumentAnalysis>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);
        private readonly object _writerGate = new object();

        private FunctionRegistry _functions;
        private IReadOnlyList<System.Reflection.Assembly> _retainedHandles = Array.Empty<System.Reflection.Assembly>();
        private bool _disposed;

        public HeddleLanguageService(HeddleLanguageServiceOptions options)
        {
            _options = options ?? new HeddleLanguageServiceOptions();
            _analyzer = new DocumentAnalyzer(_options);
            InitializeWorkspace();
        }

        /// <summary>Optional sink for operational/user-actionable messages, set by the server or tests. The
        /// workspace loads while the service is being constructed, before a host can assign this, so lines logged until then are kept and delivered on assignment.</summary>
        internal Action<string> LogSink
        {
            get { return _logSink; }
            set
            {
                _logSink = value;
                if (value == null)
                    return;
                List<string> pending;
                lock (_pendingLog)
                {
                    pending = new List<string>(_pendingLog);
                    _pendingLog.Clear();
                }

                foreach (var line in pending)
                    value(line);
            }
        }

        private Action<string> _logSink;
        private readonly List<string> _pendingLog = new List<string>();

        /// <summary>The current workspace function registry (the export-scan result, or null = Default).</summary>
        internal FunctionRegistry Functions => _functions;

        /// <summary>A weak reference to the last-unloaded model context, for the collection check.</summary>
        internal WeakReference LastUnloadedModelContext => _modelManager.LastUnloaded;

        private void InitializeWorkspace()
        {
            lock (_writerGate)
                LoadWorkspace();
        }

        /// <summary>Loads the configured assemblies and publishes what they export. The exports are read from the
        /// very assemblies the models come from, so a reload rescans them: a workspace with no configured
        /// assemblies contributes none and stays on the default registry (bare-host parity).</summary>
        private void LoadWorkspace()
        {
            try
            {
                _retainedHandles = _options.AssemblyPaths != null && _options.AssemblyPaths.Count > 0
                    ? _modelManager.Load(_options.AssemblyPaths, Log)
                    : System.Array.Empty<System.Reflection.Assembly>();
                ExtensionRegistrar.Publish(this, _retainedHandles, Log);
                _functions = FunctionExportRegistrar.BuildRegistry(_retainedHandles, Log);
            }
            catch (Exception e) when (WorkspaceReflection.IsLoadFault(e))
            {
                // Whatever of the workspace did load stays; its exports are not offered. A service that cannot
                // be constructed is a server that cannot start, which no workspace content is worth.
                _functions = null;
                Log("Heddle: the workspace's exports could not be read, so none are offered: " +
                    WorkspaceReflection.Describe(e));
            }
        }

        /// <summary>Drops everything that refers into the model context, so it can unload.</summary>
        private void UnloadWorkspace(bool withdrawExports)
        {
            _analyses.Clear();
            _functions = null;
            _retainedHandles = System.Array.Empty<System.Reflection.Assembly>();
            // A reload leaves the old layer in place until the new one replaces it in a single publication.
            if (withdrawExports)
                ExtensionRegistrar.Withdraw(this);
            _modelManager.Unload();
        }

        /// <summary>Analyzes a document version; returns the immutable analysis. Cancellation is honored between
        /// pipeline stages.</summary>
        public DocumentAnalysis Analyze(string path, string text, int version,
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (text == null) throw new ArgumentNullException(nameof(text));

            var gate = _locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            gate.Wait(cancellationToken);
            try
            {
                FunctionRegistry functions;
                lock (_writerGate)
                    functions = _functions;
                var analysis = _analyzer.Analyze(path, text, version, functions, cancellationToken);
                _analyses[path] = analysis;
                return analysis;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>The latest completed analysis for the path, or null.</summary>
        public DocumentAnalysis GetAnalysis(string path)
        {
            return path != null && _analyses.TryGetValue(path, out var analysis) ? analysis : null;
        }

        /// <summary>Releases the document's analysis state.</summary>
        public void Close(string path)
        {
            if (path == null)
                return;
            _analyses.TryRemove(path, out _);
            _locks.TryRemove(path, out _);
        }

        /// <summary>Completion items for the UTF-16 offset.</summary>
        public CompletionResult GetCompletions(string path, int offset,
            CancellationToken cancellationToken = default)
        {
            var analysis = GetAnalysis(path);
            if (analysis == null)
                return CompletionResult.Empty;
            FunctionRegistry functions;
            lock (_writerGate)
                functions = _functions;

            // Completion runs against a repaired copy of the buffer so the enclosing body parses and records its
            // narrowed model type (the offset is unchanged). This analysis is synchronous and request-forced —
            // it is not cached and never republished.
            try
            {
                var (repairedText, repairedOffset) = CompletionText.Repair(analysis.Text, offset);
                DocumentAnalysis completionAnalysis;
                if (string.Equals(repairedText, analysis.Text, StringComparison.Ordinal))
                    completionAnalysis = analysis;
                else
                    completionAnalysis = _analyzer.Analyze(path, repairedText, analysis.Version, functions,
                        cancellationToken);

                return CompletionProvider.GetCompletions(completionAnalysis, repairedOffset, functions, LogOnce);
            }
            catch (Exception e) when (WorkspaceReflection.IsRecoverable(e))
            {
                LogOnce("Completion skipped: " + WorkspaceReflection.Describe(e));
                return CompletionResult.Empty;
            }
        }

        /// <summary>Hover content for the offset, or null.</summary>
        public HoverResult GetHover(string path, int offset)
        {
            var analysis = GetAnalysis(path);
            if (analysis == null)
                return null;
            FunctionRegistry functions;
            lock (_writerGate)
                functions = _functions;
            try
            {
                return HoverProvider.GetHover(analysis, offset, functions, LogOnce);
            }
            catch (Exception e) when (WorkspaceReflection.IsRecoverable(e))
            {
                LogOnce("Hover skipped: " + WorkspaceReflection.Describe(e));
                return null;
            }
        }

        /// <summary>Definition target for the offset, or null.</summary>
        public DefinitionTarget GetDefinition(string path, int offset)
        {
            var analysis = GetAnalysis(path);
            if (analysis == null)
                return null;
            try
            {
                return DefinitionProvider.GetDefinition(analysis, offset);
            }
            catch (Exception e) when (WorkspaceReflection.IsRecoverable(e))
            {
                LogOnce("Definition skipped: " + WorkspaceReflection.Describe(e));
                return null;
            }
        }

        /// <summary>Runs the model-assembly reload protocol; invalidates existing analyses — open documents must be
        /// re-analyzed by the caller.</summary>
        public void ReloadModelAssemblies()
        {
            lock (_writerGate)
            {
                UnloadWorkspace(withdrawExports: false);
                LoadWorkspace();
            }
        }

        /// <summary>A request repeats on every keystroke; what it finds missing in the workspace is said once.</summary>
        private void LogOnce(string message)
        {
            lock (_saidOnce)
            {
                if (!_saidOnce.Add(message))
                    return;
            }

            Log(message);
        }

        private readonly HashSet<string> _saidOnce = new HashSet<string>(StringComparer.Ordinal);

        private void Log(string message)
        {
            var sink = _logSink;
            if (sink != null)
            {
                sink(message);
                return;
            }

            lock (_pendingLog)
                _pendingLog.Add(message);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            lock (_writerGate)
                UnloadWorkspace(withdrawExports: true);
        }
    }
}
