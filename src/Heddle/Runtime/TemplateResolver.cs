using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Precompiled;

namespace Heddle.Runtime {
    public class TemplateResolver : ITemplateResolver
    {
        private readonly string[] _viewPath = { @"\views\{1}\{0}", @"\views\{0}" };
        private readonly string[] _masterPath = { @"\views\base\{1}\{0}", @"\views\base\{0}" };

        private readonly string[] _partialPath = {@"\views\partial\{1}\{0}", @"\views\partial\{0}", @"\views\{1}\{0}", @"\views\{0}"};


        private Dictionary<string, HeddleTemplate> TemplatesCache { get; }

        private readonly string _rootPath;
        private readonly bool _checkFileChange;
        private readonly OutputProfile _defaultProfile;
        private readonly bool _trimDirectiveLines;

        public TemplateResolver(string rootPath, bool checkFileChange = false)
            : this(rootPath, checkFileChange, OutputProfile.Html)
        {
        }

        /// <summary>
        /// Creates a resolver whose templates compile under the given default output profile; a per-call
        /// <see cref="CompileContext"/>'s options override it. The two-parameter constructor uses the 2.0
        /// default <see cref="OutputProfile.Html"/>.
        /// </summary>
        public TemplateResolver(string rootPath, bool checkFileChange, OutputProfile defaultProfile)
            : this(rootPath, checkFileChange, defaultProfile, true)
        {
        }

        /// <summary>
        /// Creates a resolver whose resolver-built templates compile under the given default profile and
        /// directive-line trimming setting; a per-call <see cref="CompileContext"/>'s options override both.
        /// The shorter constructors use the 2.0 defaults <c>(Html, true)</c>.
        /// </summary>
        public TemplateResolver(string rootPath, bool checkFileChange, OutputProfile defaultProfile,
            bool trimDirectiveLines) {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException();
            _checkFileChange = checkFileChange;
            _rootPath = Path.GetDirectoryName(rootPath);
            _defaultProfile = defaultProfile;
            _trimDirectiveLines = trimDirectiveLines;
            TemplatesCache = new Dictionary<string, HeddleTemplate>(StringComparer.OrdinalIgnoreCase);
        }

        // The cache is keyed by full path AND output profile AND trimming so one resolver can serve the same
        // file under any combination without a collision. Enum ToString() has fixed
        // casing, so the OrdinalIgnoreCase comparer is harmless on the suffix.
        private static string CacheKey(string fullPath, OutputProfile profile, bool trimDirectiveLines) =>
            fullPath + "|" + profile + (trimDirectiveLines ? "|trim" : string.Empty);

        public HeddleTemplate GetTemplate(string viewName, string controllerName, out IEnumerable<string> searchedLocations, CompileContext context = null, TemplatePathType searchType = TemplatePathType.None) {
            if (viewName == null) throw new ArgumentNullException(nameof(viewName));
            if (controllerName == null) throw new ArgumentNullException(nameof(controllerName));
            HeddleTemplate result;
            TemplateOptions options;
            string path;
            // The effective profile/trimming for this operation: the caller's context options win, else the
            // resolver default. Probe and write always agree because Create keys on
            // the same values.
            OutputProfile profile = context?.Options.OutputProfile ?? _defaultProfile;
            bool trim = context?.Options.TrimDirectiveLines ?? _trimDirectiveLines;
            switch (searchType) {
            case TemplatePathType.None:
                // Registry-first: consult before the cache probe and file check so a registered
                // manifest is looked up, not parsed and compiled. A miss (or a Fallback-policy gauntlet failure)
                // falls through to the unchanged dynamic path; with zero manifests this is one volatile read.
                if (ConsultPrecompiled(viewName, context, profile, trim, out result)) {
                    searchedLocations = null;
                    return result;
                }
                if (TemplatesCache.TryGetValue(CacheKey(Path.Combine(_rootPath, viewName), profile, trim), out result)) {
                    searchedLocations = null;
                    return result;
                }
                if (File.Exists(Path.Combine(_rootPath, viewName))) {
                    options = new TemplateOptions(Path.GetFileNameWithoutExtension(viewName))
                    {
                        EnableFileChangeCheck = _checkFileChange,
                        FileNamePostfix = Path.GetExtension(viewName),
                        RootPath = _rootPath,
                        OutputProfile = profile,
                        TrimDirectiveLines = trim,
                    };
                    if (context == null)
                    {
                        context = new CompileContext(options)
                        {
                            ControllerName = controllerName
                        };
                        searchedLocations = null;
                        return Create(viewName, context);
                    }
                    result = Create(viewName, new CompileContext(context, context.ScopeType, Path.GetFileNameWithoutExtension(viewName)) { ControllerName = controllerName });
                    searchedLocations = null;
                    return result;
                }
                searchedLocations = new[] { Path.Combine(_rootPath, viewName) };
                return null;
            case TemplatePathType.View:
                // The View arm builds its own options below and ignores the caller's context, so the registry
                // consult inside Search synthesizes the same ones (null = synthesize).
                path = Search(viewName, controllerName, searchType, profile, trim, null, out searchedLocations, out result);
                if (result != null)
                    return result;
                options = new TemplateOptions(Path.GetFileNameWithoutExtension(path))
                {
                    EnableFileChangeCheck = _checkFileChange,
                    FileNamePostfix = Path.GetExtension(path),
                    RootPath = _rootPath,
                    ExpressionMode = ExpressionMode.FullCSharp,
                    OutputProfile = profile,
                    TrimDirectiveLines = trim
                };
                return Create(path, new CompileContext(options) { ControllerName = controllerName });
            case TemplatePathType.PartialView:
                // The PartialView arm hands the caller's context straight to Create when it has one, so the consult
                // must run the gauntlet against exactly those options.
                path = Search(viewName, controllerName, searchType, profile, trim, context?.Options,
                    out searchedLocations, out result);
                if (result != null)
                    return result;
                options = new TemplateOptions(Path.GetFileNameWithoutExtension(path))
                {
                    EnableFileChangeCheck = _checkFileChange,
                    FileNamePostfix = Path.GetExtension(path),
                    RootPath = _rootPath,
                    ExpressionMode = ExpressionMode.FullCSharp,
                    OutputProfile = profile,
                    TrimDirectiveLines = trim
                };
                if (context != null) {
                    context.ControllerName = controllerName;
                    return Create(path, context);
                }
                return Create(path, new CompileContext(options) { ControllerName = controllerName });
            case TemplatePathType.Master:
                throw new TemplateCreateException("You cannot get master template using GetTemplate, please see Search method.");
            default:
                throw new ArgumentOutOfRangeException(nameof(searchType));
            }
        }

        public string Search(string viewName, string controllerName, TemplatePathType searchType,
            out IEnumerable<string> searchedLocations, out HeddleTemplate cached)
        {
            return Search(viewName, controllerName, searchType, _defaultProfile, _trimDirectiveLines, null,
                out searchedLocations, out cached);
        }

        private string Search(string viewName, string controllerName, TemplatePathType searchType,
            OutputProfile profile, bool trim, TemplateOptions requestOptions,
            out IEnumerable<string> searchedLocations, out HeddleTemplate cached)
        {
            if (viewName == null) throw new ArgumentNullException(nameof(viewName));
            if (controllerName == null) throw new ArgumentNullException(nameof(controllerName));
            // Path normalization here differs deliberately from TemplateKey grammar; must stay in sync with registry consult.
            if (!Path.HasExtension(viewName))
            {
                viewName += TemplateKey.TemplateExtension;
            }
            if (viewName.Contains(".."))
                throw new ArgumentException("The view path cannot contain parent directory specifier ..");
            viewName = viewName.Replace("~/", "/").Replace('/', '\\');
            switch (searchType)
            {
                case TemplatePathType.None:
                    throw new TemplateCreateException("Search is not eligiable to non hosted views.");
                case TemplatePathType.View:
                    return Search(viewName, controllerName, _viewPath, profile, trim, requestOptions, out searchedLocations, out cached);
                case TemplatePathType.PartialView:
                    return Search(viewName, controllerName, _partialPath, profile, trim, requestOptions, out searchedLocations, out cached);
                case TemplatePathType.Master:
                    return Search(viewName, controllerName, _masterPath, profile, trim, requestOptions, out searchedLocations, out cached);
                default:
                    throw new ArgumentOutOfRangeException(nameof(searchType));
            }
        }

        /// <summary>The hosted probe ladder — three tiers: <b>registry</b>, then cache, then
        /// disk, each in location order — mirroring the <see cref="TemplatePathType.None"/> arm, which also consults
        /// the registry ahead of both tiers. Tier order beats location order, exactly as it already
        /// did for the cache: a cached location-2 template has always won over a location-1 file on disk.</summary>
        private string Search(string viewName, string controllerName, string[] locations, OutputProfile profile, bool trim,
            TemplateOptions requestOptions, out IEnumerable<string> searchedLocations, out HeddleTemplate cached) {
            if (viewName == null) throw new ArgumentNullException(nameof(viewName));
            if (controllerName == null) throw new ArgumentNullException(nameof(controllerName));
            if (locations == null) throw new ArgumentNullException(nameof(locations));
            List<string> searched = new List<string>();
            foreach (var path in locations) {
                var relativePath = string.Format(path, viewName, controllerName);
                // Candidate path uses the same shared TemplateKey normalization as the generator.
                if (!TemplateKey.TryNormalize(relativePath, out var key))
                    continue;
                var options = requestOptions ?? HostedOptions(Path.Combine(_rootPath, relativePath), profile, trim);
                // A miss, or a Fallback-policy gauntlet failure (an options fingerprint built Native cannot answer
                // these arms' FullCSharp request), falls through to the unchanged cache/disk ladder.
                if (PrecompiledTemplates.TryResolve(key, options, out var entry)) {
                    cached = new HeddleTemplate(entry.Strategy, options.Encoder, options.RenderBudget);
                    searchedLocations = null;
                    return Path.Combine(_rootPath, relativePath);
                }
            }
            foreach (var path in locations) {
                var relativePath = string.Format(path, viewName, controllerName);
                var fullPath = Path.Combine(_rootPath, relativePath);
                if (TemplatesCache.TryGetValue(CacheKey(fullPath, profile, trim), out cached)) {
                    searchedLocations = null;
                    return fullPath;
                }
            }
            foreach (var path in locations) {
                var relativePath = string.Format(path, viewName, controllerName);
                var fullPath = Path.Combine(_rootPath, relativePath);
                if (File.Exists(fullPath)) {
                    cached = null;
                    searchedLocations = null;
                    return fullPath;
                }
                searched.Add(Path.Combine(path, viewName));
            }
            cached = null;
            searchedLocations = searched;
            return null;
        }

        /// <summary>The effective options a hosted (<c>View</c>/<c>PartialView</c>/<c>Master</c>) arm would hand to
        /// <see cref="Create"/> — the same shape the View arm builds below, including
        /// <see cref="ExpressionMode.FullCSharp"/>, so the gauntlet's fingerprint step judges the registry entry
        /// against the request that would actually be compiled.</summary>
        private TemplateOptions HostedOptions(string fullPath, OutputProfile profile, bool trim) =>
            new TemplateOptions(Path.GetFileNameWithoutExtension(fullPath))
            {
                EnableFileChangeCheck = _checkFileChange,
                FileNamePostfix = Path.GetExtension(fullPath),
                RootPath = _rootPath,
                ExpressionMode = ExpressionMode.FullCSharp,
                OutputProfile = profile,
                TrimDirectiveLines = trim,
            };

        /// <summary>Registry consult for a <see cref="TemplatePathType.None"/> request. Derives the request's
        /// effective options (the caller's when present, else a synthesized view carrying this resolver's
        /// profile/trim/root/change-check), then <see cref="PrecompiledTemplates.TryResolve"/>. A hit returns a
        /// <see cref="HeddleTemplate"/> in precompiled-adapter mode; a miss or Fallback failure returns false.</summary>
        private bool ConsultPrecompiled(string viewName, CompileContext context, OutputProfile profile, bool trim,
            out HeddleTemplate result)
        {
            result = null;
            var options = context?.Options ?? new TemplateOptions(Path.GetFileNameWithoutExtension(viewName))
            {
                EnableFileChangeCheck = _checkFileChange,
                FileNamePostfix = Path.GetExtension(viewName),
                RootPath = _rootPath,
                OutputProfile = profile,
                TrimDirectiveLines = trim,
            };

            if (!PrecompiledTemplates.TryResolve(viewName, options, out var entry))
                return false;

            // Carry the request's output encoder and render budget onto the precompiled-adapter render (the
            // adapter has no CompileContext to read options from at render time).
            result = new HeddleTemplate(entry.Strategy, options.Encoder, options.RenderBudget);
            return true;
        }

        public HeddleTemplate Create(string viewName, CompileContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var result = new HeddleTemplate(context);
            result.OnFileDeleted += OnDeleted;
            result.OnFileRenamed += OnRenamed;
            TemplatesCache.Add(CacheKey(Path.Combine(_rootPath, viewName), context.Options.OutputProfile, context.Options.TrimDirectiveLines), result);
            return result;
        }

        public void RemoveFromCache(HeddleTemplate template) {
            if (template == null)
                return;
            // Value-based eviction: a template may be cached under more than one profile-suffixed key.
            // Collect the matching keys first — removing during the enumeration throws
            // (InvalidOperationException) on .NET Framework.
            List<string> keysToRemove = null;
            foreach (var pair in TemplatesCache) {
                if (pair.Value == template) {
                    (keysToRemove ?? (keysToRemove = new List<string>())).Add(pair.Key);
                }
            }
            if (keysToRemove != null) {
                foreach (var key in keysToRemove) {
                    TemplatesCache.Remove(key);
                }
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            if (e.ChangeType == WatcherChangeTypes.Renamed) {
                var template = sender as HeddleTemplate;
                RemoveFromCache(template);
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e) {
            if (e.ChangeType == WatcherChangeTypes.Deleted) {
                var template = sender as HeddleTemplate;
                RemoveFromCache(template);
            }
        }
    }
}
