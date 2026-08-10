using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Precompiled;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Drives the engine pipeline (parse → <c>HeddleCompiler.Compile</c> → optional Roslyn) directly and
    /// projects the side channels into an immutable <see cref="DocumentAnalysis"/>. Never uses
    /// <c>HeddleTemplate</c> — the facade wants tokens/errors/warnings/scope map, not the render tree.
    /// </summary>
    internal sealed class DocumentAnalyzer
    {
        private static readonly Regex ImportRegex =
            new Regex(@"@<<\s*\{\{(?<path>[^}]*)\}\}", RegexOptions.Compiled);
        private static readonly Regex PartialRegex =
            new Regex(@"@partial\s*\(\s*\)\s*\{\{(?<path>[^}]*)\}\}", RegexOptions.Compiled);

        private readonly HeddleLanguageServiceOptions _options;

        internal DocumentAnalyzer(HeddleLanguageServiceOptions options)
        {
            _options = options;
        }

        internal DocumentAnalysis Analyze(string path, string text, int version,
            Heddle.Runtime.Expressions.FunctionRegistry functions, CancellationToken cancellationToken)
        {
            var templateOptions = BuildTemplateOptions(functions);
            var compileContext = new CompileContext(templateOptions, (ExType)typeof(object));
            var parseContext = DocumentParser.Parse(text, compileContext, out _);
            cancellationToken.ThrowIfCancellationRequested();

            var compileScope = new CompileScope(compileContext);
            try
            {
                var runtimeDocument = HeddleCompiler.Compile(text, compileScope, parseContext, null);
                runtimeDocument?.Dispose();
            }
            catch (Exception e)
            {
                compileContext.CompileErrors.Add(new HeddleCompileError
                {
                    Error = "Internal analysis error: " + e.Message,
                    Position = default
                });
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool csharpUsed = false;
            if (templateOptions.ExpressionMode == ExpressionMode.FullCSharp &&
                compileScope.CSharpContext.Methods.Count > 0)
            {
                try
                {
                    ContextCompilation.Compile(compileScope);
                }
                catch (Exception e)
                {
                    compileContext.CompileErrors.Add(new HeddleCompileError
                    {
                        Error = "C# tier compile error: " + e.Message,
                        Position = default
                    });
                }

                csharpUsed = true;
            }

            var namespaces = compileScope.CSharpContext.Namespaces;
            var lineMap = new LineMap(text);
            var diagnostics = ProjectDiagnostics(compileContext, parseContext);
            var definitions = ProjectDefinitions(parseContext, path, namespaces);
            var imports = ScanImports(text);
            var scopes = new ScopeMapView(compileContext.ScopeMap, compileContext.RootScopeType);

            return new DocumentAnalysis(path, version, text, lineMap,
                parseContext.Tokens.ToList(), parseContext.SkippedTokens.ToList(),
                diagnostics, definitions, imports, scopes, csharpUsed);
        }

        /// <summary>Projects workspace options to engine options. Every analysis-applicable option is carried; the
        /// editor compiles under the same options as the build. <c>ProvideLanguageFeatures</c> is hardwired as
        /// the analyzer's operating mode, not a workspace choice.</summary>
        private TemplateOptions BuildTemplateOptions(Heddle.Runtime.Expressions.FunctionRegistry functions)
        {
            return new TemplateOptions
            {
                ProvideLanguageFeatures = true,
                RootPath = string.IsNullOrEmpty(_options?.RootPath)
                    ? AppContext.BaseDirectory
                    : _options.RootPath,
                OutputProfile = _options?.OutputProfile ?? HeddleBuildOptions.DefaultOutputProfile,
                ExpressionMode = _options?.ExpressionMode ?? HeddleBuildOptions.DefaultExpressionMode,
                FileNamePostfix = _options?.FileNamePostfix ?? string.Empty,
                TrimDirectiveLines = _options?.TrimDirectiveLines ?? HeddleBuildOptions.DefaultTrimDirectiveLines,
                MaxRecursionCount = _options?.MaxRecursionCount ?? HeddleBuildOptions.DefaultMaxRecursionCount,
                Functions = functions
            };
        }

        /// <summary>Uses shared projection, then re-anchors import-origin entries to zero-width at the import site
        /// with path-prefixed messages. Channel selection and deduplication follow shared rules with build tier.</summary>
        private IReadOnlyList<HeddleDiagnostic> ProjectDiagnostics(CompileContext compileContext,
            ParseContext parseContext)
        {
            var result = new List<HeddleDiagnostic>();
            foreach (var entry in HeddleDiagnosticProjection.Drain(compileContext, parseContext))
            {
                var severity = entry.IsWarning
                    ? HeddleDiagnosticSeverity.Warning
                    : HeddleDiagnosticSeverity.Error;
                int offset = entry.Offset;
                int length = entry.Length;
                string message = entry.Message;
                string importedFrom = null;

                var origin = entry.ImportOrigin;
                if (origin != null)
                {
                    importedFrom = RenderPath(origin.Path);
                    offset = origin.Site.StartIndex;
                    length = 0;
                    message = $"imported '{importedFrom}': {entry.Message}";
                }

                result.Add(new HeddleDiagnostic(entry.Id, message, entry.Fix, severity, offset, length,
                    importedFrom));
            }

            return result;
        }

        private IReadOnlyList<DefinitionInfo> ProjectDefinitions(ParseContext parseContext, string analyzedPath,
            ICollection<string> namespaces)
        {
            var result = new List<DefinitionInfo>();
            foreach (var pair in parseContext.DefinitionsBlock.Definitions)
            {
                var definition = pair.Value;
                var sourcePath = definition.Context?.ImportOrigin?.Path ?? analyzedPath;
                var modelTypeName = definition.ModelType;
                ExType modelType = ResolveType(modelTypeName, namespaces);
                bool isPinned = modelType != null && !modelType.IsDynamic && modelType.Type != typeof(object);
                var props = FlattenProps(definition, namespaces);
                var slotTypeName = FirstSlotType(definition);
                var regions = ProjectRegions(definition, namespaces);
                result.Add(new DefinitionInfo(definition.Name, sourcePath,
                    definition.Position.StartIndex, definition.Position.Length,
                    modelTypeName, isPinned ? modelType : null, isPinned, props, slotTypeName,
                    definition.BaseDefinition?.Name, regions));
            }

            return result;
        }

        private IReadOnlyList<PropInfo> FlattenProps(DefinitionItem definition, ICollection<string> namespaces)
        {
            // Inheritance flattening: the most-derived declaration of each prop name wins.
            var byName = new Dictionary<string, PropInfo>(StringComparer.Ordinal);
            for (var d = definition; d != null; d = d.BaseDefinition)
            {
                foreach (var declaration in d.PropDeclarations)
                {
                    if (byName.ContainsKey(declaration.Name))
                        continue;
                    var type = ResolveType(declaration.TypeName, namespaces);
                    byName[declaration.Name] = new PropInfo(declaration.Name, declaration.TypeName, type,
                        !declaration.HasDefault, declaration.DefaultValue,
                        declaration.Position.StartIndex, declaration.Position.Length);
                }
            }

            return byName.Values.ToList();
        }

        /// <summary>Projects the parse-model region declarations — the LSP reads the parse model,
        /// it does not reimplement the region table.</summary>
        private IReadOnlyList<RegionInfo> ProjectRegions(DefinitionItem definition, ICollection<string> namespaces)
        {
            if (definition.Regions == null || definition.Regions.Count == 0)
                return Array.Empty<RegionInfo>();
            var result = new List<RegionInfo>(definition.Regions.Count);
            foreach (var region in definition.Regions)
            {
                result.Add(new RegionInfo(region.Name, region.IsPublic, region.TypeName,
                    ResolveType(region.TypeName, namespaces),
                    region.Position.StartIndex, region.Position.Length));
            }

            return result;
        }

        private static string FirstSlotType(DefinitionItem definition)
        {
            for (var d = definition; d != null; d = d.BaseDefinition)
            {
                if (!string.IsNullOrEmpty(d.SlotTypeName))
                    return d.SlotTypeName;
            }

            return null;
        }

        private static ExType ResolveType(string name, ICollection<string> namespaces)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "object")
                return null;
            if (name == "dynamic")
                return ExType.Dynamic;
            try
            {
                var resolved = ReflectionHelper.ResolveType(name, namespaces ?? Array.Empty<string>());
                return resolved != null ? new ExType(resolved) : null;
            }
            catch
            {
                return null;
            }
        }

        private IReadOnlyList<ImportLink> ScanImports(string text)
        {
            var links = new List<ImportLink>();
            foreach (Match match in ImportRegex.Matches(text))
                links.Add(BuildLink(ImportLinkKind.Import, match));
            foreach (Match match in PartialRegex.Matches(text))
                links.Add(BuildLink(ImportLinkKind.Partial, match));
            return links;
        }

        private ImportLink BuildLink(ImportLinkKind kind, Match match)
        {
            var raw = match.Groups["path"].Value.Trim();
            string resolved = null;
            try
            {
                var root = string.IsNullOrEmpty(_options?.RootPath) ? AppContext.BaseDirectory : _options.RootPath;
                var candidate = kind == ImportLinkKind.Partial
                    ? Path.Combine(root, raw + (_options?.FileNamePostfix ?? string.Empty))
                    : Path.Combine(root, raw);
                if (File.Exists(candidate))
                    resolved = candidate;
            }
            catch
            {
                // ignore malformed paths — ResolvedPath stays null
            }

            return new ImportLink(kind, match.Index, match.Length, raw, resolved);
        }

        private string RenderPath(string path) => RenderPath(path, _options?.RootPath);

        /// <summary>
        /// The display spelling of an import/partial origin: the template key it would have under
        /// <paramref name="root"/>, or the absolute path in <c>/</c> form when it has none. Uses
        /// <see cref="TemplateKey.TryMakeRelative"/> to avoid a prefix-strip bug where <c>StartsWith</c> matched
        /// sibling directories (<c>/root</c> vs <c>/rootx/a</c>). Paths outside root show absolute with backslashes
        /// normalized to forward slashes.
        /// </summary>
        internal static string RenderPath(string path, string root)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (!string.IsNullOrEmpty(root))
            {
                try
                {
                    if (TemplateKey.TryMakeRelative(Path.GetFullPath(path), Path.GetFullPath(root), out var key))
                        return key;
                }
                catch
                {
                    // malformed path or root — fall through to absolute
                }
            }

            return path.Replace('\\', '/');
        }
    }
}
