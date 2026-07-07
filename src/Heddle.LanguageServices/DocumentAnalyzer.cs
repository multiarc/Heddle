using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Heddle.Data;
using Heddle.Helpers;
using Heddle.Language;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Drives the engine pipeline (parse → <c>HeddleCompiler.Compile</c> → optional Roslyn) directly (D9) and
    /// projects the side channels into an immutable <see cref="DocumentAnalysis"/> (D10). Never uses
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

        private TemplateOptions BuildTemplateOptions(Heddle.Runtime.Expressions.FunctionRegistry functions)
        {
            return new TemplateOptions
            {
                ProvideLanguageFeatures = true,
                RootPath = string.IsNullOrEmpty(_options?.RootPath)
                    ? AppContext.BaseDirectory
                    : _options.RootPath,
                OutputProfile = _options?.OutputProfile ?? OutputProfile.Text,
                ExpressionMode = _options?.ExpressionMode ?? ExpressionMode.Native,
                FileNamePostfix = _options?.FileNamePostfix ?? string.Empty,
                Functions = functions
            };
        }

        private IReadOnlyList<HeddleDiagnostic> ProjectDiagnostics(CompileContext compileContext,
            ParseContext parseContext)
        {
            var seen = new HashSet<HeddleCompileError>();
            var result = new List<HeddleDiagnostic>();

            void Add(HeddleCompileError entry)
            {
                if (entry == null || !seen.Add(entry))
                    return;
                var severity = entry is HeddleCompileWarning
                    ? HeddleDiagnosticSeverity.Warning
                    : HeddleDiagnosticSeverity.Error;
                var fix = (entry as HeddleCompileWarning)?.Fix;
                int offset = entry.Position.StartIndex;
                int length = entry.Position.Length;
                string message = entry.Error;
                string importedFrom = null;

                var origin = entry.ImportOrigin;
                if (origin != null)
                {
                    importedFrom = RenderPath(origin.Path);
                    offset = origin.Site.StartIndex;
                    length = 0;
                    message = $"imported '{importedFrom}': {entry.Error}";
                }

                result.Add(new HeddleDiagnostic(entry.DiagnosticId, message, fix, severity, offset, length,
                    importedFrom));
            }

            foreach (var error in compileContext.CompileErrors)
                Add(error);
            foreach (var warning in compileContext.CompileWarnings)
                Add(warning);
            foreach (var error in parseContext.Errors)
                Add(error);
            foreach (var warning in parseContext.Warnings)
                Add(warning);

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
                result.Add(new DefinitionInfo(definition.Name, sourcePath,
                    definition.Position.StartIndex, definition.Position.Length,
                    modelTypeName, isPinned ? modelType : null, isPinned, props, slotTypeName,
                    definition.BaseDefinition?.Name));
            }

            return result;
        }

        private IReadOnlyList<PropInfo> FlattenProps(DefinitionItem definition, ICollection<string> namespaces)
        {
            // Inheritance flattening (phase 5 D6): the most-derived declaration of each prop name wins.
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

        private string RenderPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            var root = _options?.RootPath;
            if (!string.IsNullOrEmpty(root))
            {
                try
                {
                    var full = Path.GetFullPath(path);
                    var rootFull = Path.GetFullPath(root);
                    if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                    {
                        var rel = full.Substring(rootFull.Length).TrimStart('/', '\\');
                        return rel.Replace('\\', '/');
                    }
                }
                catch
                {
                    // fall through to absolute
                }
            }

            return path.Replace('\\', '/');
        }
    }
}
