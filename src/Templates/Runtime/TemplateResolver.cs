using System;
using System.Collections.Generic;
using System.IO;
using Templates.Data;
using Templates.Exceptions;

namespace Templates.Runtime {
    public class TemplateResolver : ITemplateResolver
    {
        private readonly string[] _viewPath = { @"\views\{1}\{0}", @"\views\{0}" };
        private readonly string[] _masterPath = { @"\views\base\{1}\{0}", @"\views\base\{0}" };

        private readonly string[] _partialPath = {@"\views\partial\{1}\{0}", @"\views\partial\{0}", @"\views\{1}\{0}", @"\views\{0}"};

        private const string FileExtension = ".thtml";

        private Dictionary<string, TtlTemplate> TemplatesCache { get; }

        private readonly string _rootPath;
        private readonly bool _checkFileChange;

        public TemplateResolver(string rootPath, bool checkFileChange = false) {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException();
            _checkFileChange = checkFileChange;
            _rootPath = Path.GetDirectoryName(rootPath);
            TemplatesCache = new Dictionary<string, TtlTemplate>(StringComparer.OrdinalIgnoreCase);
        }

        public TtlTemplate GetTemplate(string viewName, string controllerName, out IEnumerable<string> searchedLocations, CompileContext context = null, TemplatePathType searchType = TemplatePathType.None) {
            if (viewName == null) throw new ArgumentNullException(nameof(viewName));
            if (controllerName == null) throw new ArgumentNullException(nameof(controllerName));
            TtlTemplate result;
            TemplateOptions options;
            string path;
            switch (searchType) {
            case TemplatePathType.None:
                if (TemplatesCache.TryGetValue(Path.Combine(_rootPath, viewName), out result)) {
                    searchedLocations = null;
                    return result;
                }
                if (File.Exists(Path.Combine(_rootPath, viewName))) {
                    options = new TemplateOptions(Path.GetFileNameWithoutExtension(viewName))
                    {
                        EnableFileChangeCheck = _checkFileChange,
                        FileNamePostfix = Path.GetExtension(viewName),
                        RootPath = _rootPath,
                    };
                    if (context == null)
                    {
                        context = new CompileContext(options) {ControllerName = controllerName};
                        searchedLocations = null;
                        return Create(viewName, context);
                    }
                    result = Create(viewName, new CompileContext(context, context.ModelType, Path.GetFileNameWithoutExtension(viewName)) { ControllerName = controllerName });
                    searchedLocations = null;
                    return result;
                }
                searchedLocations = new[] { Path.Combine(_rootPath, viewName) };
                return null;
            case TemplatePathType.View:
                path = Search(viewName, controllerName, searchType, out searchedLocations, out result);
                if (result != null)
                    return result;
                options = new TemplateOptions(Path.GetFileNameWithoutExtension(path))
                {
                    EnableFileChangeCheck = _checkFileChange,
                    FileNamePostfix = Path.GetExtension(path),
                    RootPath = _rootPath,
                    AllowCSharp = true
                };
                return Create(path, new CompileContext(options) { ControllerName = controllerName });
            case TemplatePathType.PartialView:
                path = Search(viewName, controllerName, searchType, out searchedLocations, out result);
                if (result != null)
                    return result;
                options = new TemplateOptions(Path.GetFileNameWithoutExtension(path))
                {
                    EnableFileChangeCheck = _checkFileChange,
                    FileNamePostfix = Path.GetExtension(path),
                    RootPath = _rootPath,
                    AllowCSharp = true
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
            out IEnumerable<string> searchedLocations, out TtlTemplate cached)
        {
            if (viewName == null) throw new ArgumentNullException(nameof(viewName));
            if (controllerName == null) throw new ArgumentNullException(nameof(controllerName));
            if (!Path.HasExtension(viewName))
            {
                viewName = viewName + FileExtension;
            }
            if (viewName.Contains(".."))
                throw new ArgumentException("The view path cannot contain parent directory specifier ..");
            viewName = viewName.Replace("~/", "/").Replace('/', '\\');
            switch (searchType)
            {
                case TemplatePathType.None:
                    throw new TemplateCreateException("Search is not eligiable to non hosted views.");
                case TemplatePathType.View:
                    return Search(viewName, controllerName, _viewPath, out searchedLocations, out cached);
                case TemplatePathType.PartialView:
                    return Search(viewName, controllerName, _partialPath, out searchedLocations, out cached);
                case TemplatePathType.Master:
                    return Search(viewName, controllerName, _masterPath, out searchedLocations, out cached);
                default:
                    throw new ArgumentOutOfRangeException(nameof(searchType));
            }
        }

        private string Search(string viewName, string controllerName, string[] locations, out IEnumerable<string> searchedLocations, out TtlTemplate cached) {
            if (viewName == null) throw new ArgumentNullException(nameof(viewName));
            if (controllerName == null) throw new ArgumentNullException(nameof(controllerName));
            if (locations == null) throw new ArgumentNullException(nameof(locations));
            List<string> searched = new List<string>();
            foreach (var path in locations) {
                var relativePath = string.Format(path, viewName, controllerName);
                var fullPath = Path.Combine(_rootPath, relativePath);
                if (TemplatesCache.TryGetValue(fullPath, out cached)) {
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

        public TtlTemplate Create(string viewName, CompileContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var result = new TtlTemplate(context);
            result.OnFileDeleted += OnDeleted;
            result.OnFileRenamed += OnRenamed;
            TemplatesCache.Add(Path.Combine(_rootPath, viewName), result);
            return result;
        }

        public void RemoveFromCache(TtlTemplate template) {
            if (template != null) {
                foreach (var pair in TemplatesCache) {
                    if (pair.Value == template) {
                        TemplatesCache.Remove(pair.Key);
                    }
                }
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            if (e.ChangeType == WatcherChangeTypes.Renamed) {
                var template = sender as TtlTemplate;
                RemoveFromCache(template);
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e) {
            if (e.ChangeType == WatcherChangeTypes.Deleted) {
                var template = sender as TtlTemplate;
                RemoveFromCache(template);
            }
        }
    }
}
