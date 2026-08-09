using System;
using System.Collections.Generic;
using System.IO;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Precompiled;

namespace Heddle.Runtime {
    public class TemplateResolver : ITemplateResolver
    {
        // Root-relative and separator-neutral. These were written with backslashes and a leading one, which made a
        // location a path only Windows reads — and only from the current drive's root, since a leading separator
        // makes Path.Combine discard the root it was given. Off Windows a backslash is an ordinary file-name
        // character, so every candidate was one long file name that File.Exists never found: the hosted arms served
        // precompiled templates and nothing else. `/` is a separator on every host, and Path.Combine joins these to
        // the root the resolver was constructed with, the way the TemplatePathType.None arm always has.
        private readonly string[] _viewPath = { "views/{1}/{0}", "views/{0}" };
        private readonly string[] _masterPath = { "views/base/{1}/{0}", "views/base/{0}" };

        private readonly string[] _partialPath =
            { "views/partial/{1}/{0}", "views/partial/{0}", "views/{1}/{0}", "views/{0}" };


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
            string relative;
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
                    options = new TemplateOptions(Path.ChangeExtension(viewName, null))
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
                    result = Create(viewName, new CompileContext(context, context.ScopeType, Path.ChangeExtension(viewName, null)) { ControllerName = controllerName });
                    searchedLocations = null;
                    return result;
                }
                searchedLocations = new[] { Path.Combine(_rootPath, viewName) };
                return null;
            case TemplatePathType.View:
                // The View arm builds its own options below and ignores the caller's context, so the registry
                // consult inside Search synthesizes the same ones (null = synthesize) — and, for the same reason,
                // the model type it declares is the untyped one the context it builds would carry, not the
                // caller's.
                path = Search(viewName, controllerName, searchType, profile, trim, null, typeof(object),
                    out searchedLocations, out result, out relative);
                if (result != null)
                    return result;
                options = HostedOptions(relative, profile, trim);
                return Create(path, new CompileContext(options) { ControllerName = controllerName });
            case TemplatePathType.PartialView:
                // The PartialView arm hands the caller's context straight to Create when it has one, so the consult
                // must run the gauntlet against exactly those options.
                path = Search(viewName, controllerName, searchType, profile, trim, context?.Options,
                    RequestModelType(context), out searchedLocations, out result, out relative);
                if (result != null)
                    return result;
                options = HostedOptions(relative, profile, trim);
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
                requestModelType: null, out searchedLocations, out cached, out _);
        }

        private string Search(string viewName, string controllerName, TemplatePathType searchType,
            OutputProfile profile, bool trim, TemplateOptions requestOptions, Type requestModelType,
            out IEnumerable<string> searchedLocations, out HeddleTemplate cached, out string relativePath)
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
            // `~/` is the host idiom for "from the root", and the locations below are already root-relative, so it
            // is dropped rather than turned into a leading separator that would root the candidate elsewhere.
            viewName = viewName.Replace("~/", "/").TrimStart('/', '\\');
            switch (searchType)
            {
                case TemplatePathType.None:
                    throw new TemplateCreateException("Search is not eligiable to non hosted views.");
                case TemplatePathType.View:
                    return Search(viewName, controllerName, _viewPath, profile, trim, requestOptions,
                        requestModelType, out searchedLocations, out cached, out relativePath);
                case TemplatePathType.PartialView:
                    return Search(viewName, controllerName, _partialPath, profile, trim, requestOptions,
                        requestModelType, out searchedLocations, out cached, out relativePath);
                case TemplatePathType.Master:
                    return Search(viewName, controllerName, _masterPath, profile, trim, requestOptions,
                        requestModelType, out searchedLocations, out cached, out relativePath);
                default:
                    throw new ArgumentOutOfRangeException(nameof(searchType));
            }
        }

        /// <summary>The hosted probe ladder — three tiers: <b>registry</b>, then cache, then
        /// disk, each in location order — mirroring the <see cref="TemplatePathType.None"/> arm, which also consults
        /// the registry ahead of both tiers. Tier order beats location order, exactly as it already
        /// did for the cache: a cached location-2 template has always won over a location-1 file on disk.</summary>
        private string Search(string viewName, string controllerName, string[] locations, OutputProfile profile, bool trim,
            TemplateOptions requestOptions, Type requestModelType, out IEnumerable<string> searchedLocations,
            out HeddleTemplate cached, out string relativePath) {
            if (viewName == null) throw new ArgumentNullException(nameof(viewName));
            if (controllerName == null) throw new ArgumentNullException(nameof(controllerName));
            if (locations == null) throw new ArgumentNullException(nameof(locations));
            List<string> searched = new List<string>();
            relativePath = null;
            foreach (var path in locations) {
                var candidate = string.Format(path, viewName, controllerName);
                // Candidate path uses the same shared TemplateKey normalization as the generator.
                if (!TemplateKey.TryNormalize(candidate, out var key))
                    continue;
                var options = requestOptions ?? HostedOptions(candidate, profile, trim);
                // A miss, or a Fallback-policy gauntlet failure (an options fingerprint built Native cannot answer
                // these arms' FullCSharp request), falls through to the unchanged cache/disk ladder.
                if (PrecompiledTemplates.TryResolve(key, options, requestModelType, out var entry)) {
                    cached = new HeddleTemplate(entry.Strategy, options.Encoder, options.RenderBudget,
                        modelType: entry.ModelType);
                    searchedLocations = null;
                    relativePath = candidate;
                    return Path.Combine(_rootPath, candidate);
                }
            }
            foreach (var path in locations) {
                var candidate = string.Format(path, viewName, controllerName);
                var fullPath = Path.Combine(_rootPath, candidate);
                if (TemplatesCache.TryGetValue(CacheKey(fullPath, profile, trim), out cached)) {
                    searchedLocations = null;
                    relativePath = candidate;
                    return fullPath;
                }
            }
            foreach (var path in locations) {
                var candidate = string.Format(path, viewName, controllerName);
                var fullPath = Path.Combine(_rootPath, candidate);
                if (File.Exists(fullPath)) {
                    cached = null;
                    searchedLocations = null;
                    relativePath = candidate;
                    return fullPath;
                }
                // The path that was probed, not the pattern it came from: the un-substituted form reported
                // `views/{1}/{0}/index.heddle` as a location nobody had looked in.
                searched.Add(fullPath);
            }
            cached = null;
            searchedLocations = searched;
            return null;
        }

        /// <summary>The effective options a hosted (<c>View</c>/<c>PartialView</c>/<c>Master</c>) arm hands to
        /// <see cref="Create"/>, including <see cref="ExpressionMode.FullCSharp"/>, so the gauntlet's fingerprint
        /// step judges a registry entry against the request that would actually be compiled.
        /// <para><paramref name="relativePath"/> is the candidate the search matched, <b>relative to the resolver
        /// root</b> — which is what <see cref="TemplateOptions.TemplateName"/> means. Keeping only its file name
        /// would drop the <c>views/{controller}/</c> segment the search itself inserted, and
        /// <see cref="TemplateOptions.FullPath"/> would then name a file nobody probed.</para></summary>
        private TemplateOptions HostedOptions(string relativePath, OutputProfile profile, bool trim) =>
            new TemplateOptions(Path.ChangeExtension(relativePath, null))
            {
                EnableFileChangeCheck = _checkFileChange,
                FileNamePostfix = Path.GetExtension(relativePath),
                RootPath = _rootPath,
                ExpressionMode = ExpressionMode.FullCSharp,
                OutputProfile = profile,
                TrimDirectiveLines = trim,
            };

        /// <summary>The model type this request would compile a template against, which is what the gauntlet's
        /// model-type step judges an ambient entry by. A caller with no context gets the same answer the dynamic path
        /// would have reached for it — <see cref="CompileContext"/>'s own untyped default — rather than "no claim",
        /// because the absence of a context is itself a decision about the model type, not an unknown.</summary>
        private static Type RequestModelType(CompileContext context) =>
            context?.RootScopeType?.Type ?? typeof(object);

        /// <summary>Registry consult for a <see cref="TemplatePathType.None"/> request. Derives the request's
        /// effective options (the caller's when present, else a synthesized view carrying this resolver's
        /// profile/trim/root/change-check) and its model type, then
        /// <see cref="PrecompiledTemplates.TryResolve(string,TemplateOptions,Type,out PrecompiledTemplateInfo)"/>.
        /// A hit returns a <see cref="HeddleTemplate"/> in precompiled-adapter mode; a miss or Fallback failure
        /// returns false.</summary>
        private bool ConsultPrecompiled(string viewName, CompileContext context, OutputProfile profile, bool trim,
            out HeddleTemplate result)
        {
            result = null;
            var options = context?.Options ?? new TemplateOptions(Path.ChangeExtension(viewName, null))
            {
                EnableFileChangeCheck = _checkFileChange,
                FileNamePostfix = Path.GetExtension(viewName),
                RootPath = _rootPath,
                OutputProfile = profile,
                TrimDirectiveLines = trim,
            };

            if (!PrecompiledTemplates.TryResolve(viewName, options, RequestModelType(context), out var entry))
                return false;

            // Carry the request's output encoder and render budget onto the precompiled-adapter render (the
            // adapter has no CompileContext to read options from at render time), and the entry's own model type,
            // which is what the adapter gates the model value on — the dynamic tier reads that from ScopeType.
            result = new HeddleTemplate(entry.Strategy, options.Encoder, options.RenderBudget, options,
                entry.ModelType);
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
