using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Heddle.Generator.Diagnostics;
using Heddle.Generator.Emit;
using Heddle.Generator.Pipeline;
using Heddle.Language;
using Heddle.Precompiled;
using Heddle.Strings.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Heddle.Generator
{
    /// <summary>
    /// The Heddle build-time pre-compilation generator (phase 7). Discovers <c>.heddle</c> <c>AdditionalFiles</c>,
    /// reads the compilation-wide options, parses each template through the shared front end (D4), surfaces template
    /// errors at their <c>.heddle</c> span, emits a per-template <c>{SanitizedName}.g.cs</c> structural body through
    /// <see cref="TemplateEmitter"/> for every supported template, and emits the two-layer discovery metadata (the
    /// <c>[HeddleCompiledTemplates]</c> attribute + typed manifest, D6). A template using a construct the emitter does
    /// not yet cover is left un-precompiled — no entry, the render takes the byte-identical dynamic path.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class HeddleTemplateGenerator : IIncrementalGenerator
    {
        private sealed class TemplateFile
        {
            public TemplateFile(AdditionalText text, string content, string keyMetadata, bool readable, string readError)
            {
                Text = text;
                Content = content;
                KeyMetadata = keyMetadata;
                Readable = readable;
                ReadError = readError;
            }

            public AdditionalText Text { get; }
            public string Content { get; }
            public string KeyMetadata { get; }

            /// <summary>False when the <c>AdditionalFiles</c> source could not be read/decoded (HED7001).</summary>
            public bool Readable { get; }
            public string ReadError { get; }
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var templates = context.AdditionalTextsProvider
                .Where(t => t.Path.EndsWith(".heddle", StringComparison.OrdinalIgnoreCase))
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select((pair, ct) =>
                {
                    var options = pair.Right.GetOptions(pair.Left);
                    options.TryGetValue("build_metadata.AdditionalFiles.Key", out var key);
                    string content = string.Empty;
                    bool readable = true;
                    string readError = null;
                    try
                    {
                        var text = pair.Left.GetText(ct);
                        if (text == null)
                        {
                            readable = false;
                            readError = "the file content is unavailable";
                        }
                        else
                        {
                            content = text.ToString();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        readable = false;
                        readError = ex.Message;
                    }

                    return new TemplateFile(pair.Left, content, key, readable, readError);
                })
                .Collect();

            var globalConfig = context.AnalyzerConfigOptionsProvider
                .Select((provider, ct) =>
                {
                    var errors = new List<ConfigReader.OptionError>();
                    var config = ConfigReader.Read(provider.GlobalOptions, errors);
                    return new ConfigResult(config, errors);
                });

            // Milestone 1 uses the compilation's symbol metadata to type member paths (value- vs reference-typed
            // hops decide the null-safety form) — symbol inspection of the compilation's own/referenced types, which
            // is available at build (D3's "reflection is impossible" is about System.Reflection over the not-yet-built
            // assembly, not ISymbol). The symbol-diagnostics stage (HED7007/7008) is the deferred milestone-2 add.
            var combined = templates.Combine(globalConfig).Combine(context.CompilationProvider);
            context.RegisterSourceOutput(combined, static (spc, data) =>
                Emit(spc, data.Left.Left, data.Left.Right, data.Right));
        }

        private sealed class ConfigResult
        {
            public ConfigResult(GlobalConfig config, List<ConfigReader.OptionError> errors)
            {
                Config = config;
                Errors = errors;
            }

            public GlobalConfig Config { get; }
            public List<ConfigReader.OptionError> Errors { get; }
        }

        private static void Emit(SourceProductionContext spc, ImmutableArray<TemplateFile> templates,
            ConfigResult configResult, Compilation compilation)
        {
            var engineVersion = ResolveEngineVersion(compilation);
            foreach (var optionError in configResult.Errors)
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.OptionParseError, Location.None,
                    optionError.Value, optionError.Property, optionError.Expected));

            var config = configResult.Config;
            var ns = string.IsNullOrEmpty(config.GeneratedNamespace) ? "Heddle.Generated" : config.GeneratedNamespace;

            if (templates.IsDefaultOrEmpty)
            {
                EmitManifest(spc, ns, engineVersion, new List<string>());
                return;
            }

            // Import map for the shared front end's ImportReader (@<< served from AdditionalFiles, D4/D16).
            var importMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                var key = DeriveKey(template, config.TemplateRoot);
                if (key != null && !importMap.ContainsKey(key))
                    importMap[key] = template.Content;
            }

            var seenKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            var sanitizedOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var manifestEntries = new List<string>();
            var usedHintNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // D21 discovery: [ExportFunctions] over the compilation's own + referenced assemblies, computed once.
            var exports = Heddle.Generator.Binding.FunctionExportResolver.Build(compilation);

            foreach (var template in templates)
            {
                // HED7001: an AdditionalFiles .heddle source the compiler could not read/decode.
                if (!template.Readable)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.UnreadableFile,
                        Location.None, template.Text.Path, template.ReadError ?? "unknown error"));
                    continue;
                }

                var key = DeriveKey(template, config.TemplateRoot);
                if (key == null)
                {
                    // HED7004: an explicit Key metadata that is empty/whitespace or normalizes to an invalid key.
                    // A path-derived key that fails normalization is left un-precompiled silently (no user-set key).
                    if (!string.IsNullOrEmpty(template.KeyMetadata))
                        spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.InvalidKeyMetadata,
                            Location.None, template.KeyMetadata, template.Text.Path));
                    continue;
                }

                // HED7002: two templates in one compilation normalize to the same key (position: the second file).
                if (seenKeys.TryGetValue(key, out var firstPath))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.DuplicateKey,
                        Location.None, key, firstPath, template.Text.Path));
                    continue;
                }

                foreach (var existing in seenKeys.Keys)
                {
                    if (!string.Equals(existing, key, StringComparison.Ordinal) &&
                        string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.CaseOnlyKeyTwin,
                            Location.None, existing, key, key));
                    }
                }
                seenKeys[key] = template.Text.Path;

                var sanitized = SanitizeName(key);
                if (sanitizedOwners.TryGetValue(sanitized, out var owner) && !string.Equals(owner, key, StringComparison.Ordinal))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.DuplicateSanitizedName,
                        Location.None, owner, key, sanitized));
                    continue;
                }
                sanitizedOwners[sanitized] = key;

                var parsed = ParseAndReport(spc, template, importMap, out var cleanDocument, out var hadErrors);
                if (parsed == null || hadErrors)
                    continue;

                try
                {
                    var emitter = new TemplateEmitter(key, sanitized, ns, cleanDocument, template.Content, parsed, config, compilation, exports, template.Text.Path);
                    var result = emitter.Emit(ComputeContentHash(template.Content));

                    // Emitter-produced Roslyn diagnostics (HED7005 surrogate, HED7006/HED7015 extension binding),
                    // reported in every result branch at their .heddle span.
                    if (result.Diagnostics != null)
                    {
                        var text = template.Text.GetText();
                        foreach (var d in result.Diagnostics)
                            spc.ReportDiagnostic(Diagnostic.Create(d.Descriptor,
                                ToLocation(template.Text, text, d.Position), d.Args));
                    }

                    if (result.Emitted)
                    {
                        var hint = sanitized + ".g.cs";
                        if (usedHintNames.Add(hint))
                        {
                            spc.AddSource(hint, SourceText.From(result.Source, Encoding.UTF8));
                            manifestEntries.Add(result.ManifestEntry);
                        }
                    }
                    else if (result.IsMarker)
                    {
                        // OQ1 remainder (D21): a delegate-only function makes this template un-precompilable. Report
                        // one HED7014 warning per unresolvable name at its .heddle span and record a fallback-marker
                        // manifest entry (no .g.cs) — the runtime gauntlet short-circuits the marker to the dynamic path.
                        var sourceText = template.Text.GetText();
                        foreach (var fn in result.UnresolvableFunctions)
                        {
                            var location = ToLocation(template.Text, sourceText, fn.Position);
                            spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.UnresolvableFunction,
                                location, fn.Name));
                        }

                        manifestEntries.Add(result.ManifestEntry);
                    }
                    // A template the emitter does not yet cover (result.UnsupportedReason) is simply left
                    // un-precompiled: no entry, no source — the render takes the byte-identical dynamic path.
                }
                catch (Exception)
                {
                    // Emitter defect on this template: degrade to the dynamic path rather than break the build.
                }
            }

            EmitManifest(spc, ns, engineVersion, manifestEntries);
        }

        private static ParseContext ParseAndReport(SourceProductionContext spc, TemplateFile template,
            Dictionary<string, string> importMap, out string cleanDocument, out bool hadErrors)
        {
            cleanDocument = template.Content;
            hadErrors = false;

            // HED7011: an @<< import path not among the compilation's .heddle AdditionalFiles. The reader records
            // each miss (raw path as written); after the parse we report one diagnostic per distinct missing import
            // at its @<<{{…}} block in this template.
            var missingImports = new List<string>();
            var settings = new ParserSettings
            {
                RootPath = string.Empty,
                ProvideLanguageFeatures = false,
                ImportReader = importPath =>
                {
                    if (TemplateKey.TryNormalize(importPath, out var k) && importMap.TryGetValue(k, out var content))
                        return content;
                    if (!missingImports.Contains(importPath))
                        missingImports.Add(importPath);
                    return string.Empty;
                }
            };

            ParseContext parseContext;
            try
            {
                parseContext = DocumentParser.Parse(template.Content, settings, out cleanDocument);
            }
            catch (Exception ex)
            {
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.ForwardedError,
                    Location.None, "Internal parse error: " + ex.Message));
                hadErrors = true;
                return null;
            }

            var sourceText = template.Text.GetText();

            foreach (var missing in missingImports)
            {
                hadErrors = true;
                var position = FindImportBlock(template.Content, missing);
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.ImportNotIncluded,
                    ToLocation(template.Text, sourceText, position), missing));
            }
            foreach (var error in parseContext.Errors)
            {
                hadErrors = true;
                var location = ToLocation(template.Text, sourceText, error.Position);
                var descriptor = error.DiagnosticId != null
                    ? new DiagnosticDescriptor(error.DiagnosticId, "Heddle template error",
                        "{0}", "Heddle.Precompile", DiagnosticSeverity.Error, true)
                    : GeneratorDiagnostics.ForwardedError;
                spc.ReportDiagnostic(Diagnostic.Create(descriptor, location, error.Error));
            }

            foreach (var warning in parseContext.Warnings)
            {
                var location = ToLocation(template.Text, sourceText, warning.Position);
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.ForwardedWarning, location,
                    warning.Error));
            }

            return parseContext;
        }

        /// <summary>Finds the <c>@&lt;&lt;{{rawPath}}</c> import block in <paramref name="content"/> for the missing
        /// path, returning its span; a zero-width span at the document start when it cannot be located (e.g. the miss
        /// came from a nested imported file rather than this template).</summary>
        private static BlockPosition FindImportBlock(string content, string rawPath)
        {
            int search = 0;
            while (true)
            {
                var at = content.IndexOf("@<<", search, StringComparison.Ordinal);
                if (at < 0)
                    break;
                var open = content.IndexOf("{{", at, StringComparison.Ordinal);
                if (open < 0)
                    break;
                var close = content.IndexOf("}}", open, StringComparison.Ordinal);
                if (close < 0)
                    break;
                var inner = content.Substring(open + 2, close - (open + 2)).Trim();
                if (string.Equals(inner, rawPath.Trim(), StringComparison.Ordinal))
                    return new BlockPosition(at, close + 2 - at);
                search = close + 2;
            }

            return new BlockPosition(0, 0);
        }

        private static Location ToLocation(AdditionalText text, SourceText sourceText, BlockPosition position)
        {
            if (sourceText == null)
                return Location.None;
            var start = Math.Max(0, Math.Min(position.StartIndex, sourceText.Length));
            var end = Math.Max(start, Math.Min(position.StartIndex + Math.Max(0, position.Length), sourceText.Length));
            var span = TextSpan.FromBounds(start, end);
            var lineSpan = sourceText.Lines.GetLinePositionSpan(span);
            return Location.Create(text.Path, span, lineSpan);
        }

        private static void EmitManifest(SourceProductionContext spc, string ns, string engineVersion,
            List<string> manifestEntries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#pragma warning disable");
            sb.AppendLine("[assembly: global::Heddle.Precompiled.HeddleCompiledTemplates(");
            sb.AppendLine($"    manifestType:  typeof(global::{ns}.__HeddleManifest),");
            sb.AppendLine("    schemaVersion: 2,");
            sb.AppendLine($"    engineVersion: \"{engineVersion}\")]");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    internal sealed class __HeddleManifest : global::Heddle.Precompiled.IHeddleTemplateManifest");
            sb.AppendLine("    {");
            sb.AppendLine("        public global::System.Collections.Generic.IReadOnlyList<global::Heddle.Precompiled.PrecompiledTemplateInfo> GetTemplates()");
            sb.AppendLine("        {");
            if (manifestEntries.Count == 0)
            {
                sb.AppendLine("            return global::System.Array.Empty<global::Heddle.Precompiled.PrecompiledTemplateInfo>();");
            }
            else
            {
                sb.AppendLine("            return new global::Heddle.Precompiled.PrecompiledTemplateInfo[]");
                sb.AppendLine("            {");
                foreach (var entry in manifestEntries)
                {
                    foreach (var line in entry.Split('\n'))
                        sb.AppendLine("                " + line);
                    sb.AppendLine("                ,");
                }
                sb.AppendLine("            };");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            spc.AddSource("__HeddleManifest.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static string ComputeContentHash(string content)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>D11 naming: the key's segments PascalCased, identifier-invalid characters mapped to <c>_</c>,
        /// joined by <c>_</c>, extension dropped. <c>views/home/index.heddle</c> → <c>Views_Home_Index</c>.</summary>
        internal static string SanitizeName(string key)
        {
            var lastSlash = key.LastIndexOf('/');
            var dir = lastSlash >= 0 ? key.Substring(0, lastSlash) : string.Empty;
            var file = lastSlash >= 0 ? key.Substring(lastSlash + 1) : key;
            var dot = file.LastIndexOf('.');
            if (dot > 0)
                file = file.Substring(0, dot);

            var segments = new List<string>();
            if (dir.Length != 0)
                segments.AddRange(dir.Split('/'));
            segments.Add(file);

            var parts = new List<string>();
            foreach (var seg in segments)
            {
                if (seg.Length == 0)
                    continue;
                var sb = new StringBuilder(seg.Length);
                for (int i = 0; i < seg.Length; i++)
                {
                    var c = seg[i];
                    bool valid = c == '_' || char.IsLetter(c) || (i > 0 && char.IsDigit(c));
                    if (i == 0 && char.IsDigit(c))
                    {
                        sb.Append('_').Append(c);
                    }
                    else if (valid)
                    {
                        sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
                    }
                    else
                    {
                        sb.Append('_');
                    }
                }

                // Capitalize the first letter even when the first char was mapped to '_'.
                var s = sb.ToString();
                parts.Add(s);
            }

            var result = string.Join("_", parts);
            return result.Length == 0 ? "_" : result;
        }

        private static string DeriveKey(TemplateFile template, string templateRoot)
        {
            var raw = !string.IsNullOrEmpty(template.KeyMetadata)
                ? template.KeyMetadata
                : Relative(template.Text.Path, templateRoot);
            return TemplateKey.TryNormalize(raw, out var key) ? key : null;
        }

        private static string Relative(string path, string root)
        {
            if (string.IsNullOrEmpty(root))
                return System.IO.Path.GetFileName(path);
            var normalizedRoot = root.Replace('\\', '/').TrimEnd('/');
            var normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(normalizedRoot.Length + 1);
            return System.IO.Path.GetFileName(path);
        }

        private static string ResolveEngineVersion(Compilation compilation)
        {
            foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (string.Equals(reference.Identity.Name, "Heddle", StringComparison.Ordinal))
                {
                    var v = reference.Identity.Version;
                    return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", v.Major, v.Minor, v.Build);
                }
            }

            return "1.0.0";
        }
    }
}
