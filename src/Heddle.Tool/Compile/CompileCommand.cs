using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Heddle.Data;
using Heddle.Native;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.Runtime.Expressions;

namespace Heddle.Tool.Compile
{
    /// <summary>One template file read for the stamp and the compile.</summary>
    internal sealed class TemplateInput
    {
        internal TemplateItem Item;
        internal string FullPath;
        internal string Text;
        internal string ContentHash;
        internal string Key;
        internal string RegisteredName;
    }

    /// <summary>The <c>heddle compile</c> host: compiles every template through the real engine with
    /// deferred function binding over the consumer's implementation images, merges the per-template
    /// records into one artifact, and prints the generated source. Diagnostics go to stdout in the
    /// canonical MSBuild format; exit codes are 0 (no errors), 1 (errors reported), 2
    /// (usage/response-file error) and 3 (host fault before any template compiled).</summary>
    internal static class CompileCommand
    {
        internal static int Run(CompileRequest request, TextWriter stdout, TextWriter stderr)
        {
            var diagnostics = new DiagnosticWriter(stdout);
            if (string.IsNullOrEmpty(request.Project))
                return Usage(stderr, "Missing required option '--project'.");
            if (request.Probe == null && request.StubsOnly == null)
            {
                if (string.IsNullOrEmpty(request.ArtifactOut))
                    return Usage(stderr, "Missing required option '--artifact-out'.");
                if (string.IsNullOrEmpty(request.SourceOut))
                    return Usage(stderr, "Missing required option '--source-out'.");
                if (string.IsNullOrEmpty(request.Stamp))
                    return Usage(stderr, "Missing required option '--stamp'.");
            }

            request.Root = string.IsNullOrEmpty(request.Root)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(request.Root);

            if (!HeddleBuildOptions.TryReadEnum(request.OutputProfile, HeddleBuildOptions.DefaultOutputProfile,
                out OutputProfile outputProfile))
                return OptionError(request, diagnostics,
                    HeddleBuildOptions.OutputProfileProperty, request.OutputProfile,
                    HeddleBuildOptions.ExpectedValues<OutputProfile>());
            if (!HeddleBuildOptions.TryReadEnum(request.ExpressionMode, HeddleBuildOptions.DefaultExpressionMode,
                out ExpressionMode expressionMode))
                return OptionError(request, diagnostics,
                    HeddleBuildOptions.ExpressionModeProperty, request.ExpressionMode,
                    HeddleBuildOptions.ExpectedValues<ExpressionMode>());
            if (!HeddleBuildOptions.TryReadBool(request.TrimDirectiveLines,
                HeddleBuildOptions.DefaultTrimDirectiveLines, out bool trim))
                return OptionError(request, diagnostics,
                    HeddleBuildOptions.TrimDirectiveLinesProperty, request.TrimDirectiveLines,
                    HeddleBuildOptions.ExpectedBool);
            if (!HeddleBuildOptions.TryReadPositiveInt(request.MaxRecursionCount,
                HeddleBuildOptions.DefaultMaxRecursionCount, out int maxRecursion))
                return OptionError(request, diagnostics,
                    HeddleBuildOptions.MaxRecursionCountProperty, request.MaxRecursionCount,
                    HeddleBuildOptions.ExpectedPositiveInt);
            string generatedNamespace = HeddleBuildOptions.ReadString(request.GeneratedNamespace,
                "Heddle.Generated");
            string buildVersion = request.BuildVersion ?? string.Empty;

            var hostEngine = typeof(HeddleTemplate).Assembly.GetName().Version;
            if (!string.IsNullOrEmpty(request.EngineReference))
            {
                Version referenced;
                try
                {
                    referenced = AssemblyName.GetAssemblyName(request.EngineReference).Version;
                }
                catch (Exception ex) when (ex is IOException || ex is BadImageFormatException ||
                    ex is UnauthorizedAccessException || ex is FileNotFoundException)
                {
                    // The engine reference is unreadable: the version lock cannot be checked, and the
                    // artifact must not be stamped with an unverified engine.
                    diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildEngineVersionMismatch,
                        "Heddle.Build " + buildVersion + " compiles with Heddle " +
                        FormatVersion(hostEngine) + " but the engine reference '" +
                        request.EngineReference + "' could not be read: " + ex.Message + ".");
                    return 1;
                }

                if (referenced != hostEngine)
                {
                    diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildEngineVersionMismatch,
                        "Heddle.Build " + buildVersion + " compiles with Heddle " +
                        FormatVersion(hostEngine) + " but the project references Heddle " +
                        FormatVersion(referenced) +
                        "; reference the same version of both packages.");
                    return 1;
                }
            }

            var templates = new List<TemplateInput>();
            foreach (var item in request.Templates)
            {
                var input = ReadInput(request, item.Path, diagnostics);
                if (input == null)
                    return 1;
                input.Item = item;
                templates.Add(input);
            }

            foreach (var import in request.ImportOnly)
            {
                var item = new TemplateItem
                {
                    Path = import.Path,
                    Key = import.Key,
                    Name = import.Name,
                    IsImportOnly = true
                };
                var input = ReadInput(request, import.Path, diagnostics);
                if (input == null)
                    return 1;
                input.Item = item;
                templates.Add(input);
            }

            if (!DeriveKeys(request, templates, diagnostics))
                return 1;

            // The artifact's rows, the stubs and the stamp all follow ordinal key order, so
            // item or response-file order cannot change a byte or the digest.
            templates.Sort((a, b) => string.CompareOrdinal(a.Key ?? string.Empty, b.Key ?? string.Empty));

            // The stubs pass writes wrapper stubs with no engine compile, no image load, no artifact,
            // source or stamp — so a following real build finds its outputs out of date.
            if (request.StubsOnly != null)
            {
                var stubs = new List<SourceEmitter.StubTemplate>();
                foreach (var template in templates)
                {
                    if (template.Item.IsImportOnly)
                        continue;
                    stubs.Add(new SourceEmitter.StubTemplate
                    {
                        Sanitized = SanitizeName.ForKey(template.Key),
                        ModelTypeName = StubModelName(template)
                    });
                }

                SourceEmitter.WriteStubs(request.StubsOnly, generatedNamespace, stubs);
                return 0;
            }

            bool hasErrors = false;
            using (var images = new ImageLoadContext())
            {
                foreach (var reference in request.References)
                {
                    try
                    {
                        images.LoadImage(reference);
                    }
                    catch (ImageLoadException ex)
                    {
                        diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildImplementationImageNotLoaded,
                            "Implementation assembly '" + reference +
                            "' could not be loaded: " + ex.Message +
                            ". Templates naming its types cannot be compiled; fix the reference or " +
                            "exclude the templates with Precompile=\"false\".");
                        hasErrors = true;
                    }
                }

                if (hasErrors)
                    return 1;

                var functions = new FunctionRegistry();
                foreach (var image in images.Images)
                {
                    try
                    {
                        HeddleTemplate.Register(image);
                        functions.RegisterFrom(image);
                    }
                    catch (ArgumentException ex)
                    {
                        diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildIneligibleExportContainer,
                            Format(HeddleDiagnosticIds.BuildIneligibleExportContainer, ex.Message));
                        return 1;
                    }
                }

                try
                {
                    AssemblyHelper.RegisterModelAssemblies(images.Images);
                }
                catch (Exception ex) when (ex is IOException || ex is BadImageFormatException)
                {
                    diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildImplementationImageNotLoaded,
                        "Implementation assemblies could not be registered as model assemblies: " +
                        ex.Message + ".");
                    return 1;
                }

                if (request.Probe != null)
                {
                    Probe.Run(request, templates, images, generatedNamespace);
                    return 0;
                }

                string digest = Stamp.Compute(buildVersion, request, templates, request.ImportOnly);
                if (string.Equals(Stamp.Read(request.Stamp), digest, StringComparison.Ordinal) &&
                    File.Exists(request.ArtifactOut) && File.Exists(request.SourceOut))
                {
                    stdout.WriteLine("up to date");
                    return 0;
                }

                var options = new ResolvedOptions
                {
                    OutputProfile = outputProfile,
                    ExpressionMode = expressionMode,
                    TrimDirectiveLines = trim,
                    MaxRecursionCount = maxRecursion,
                    GeneratedNamespace = generatedNamespace,
                    BuildVersion = buildVersion,
                    Functions = functions
                };
                // The implementation images are never framework types, wherever they live. Carried on this
                // compile's options and handed to each template's own record, so two compiles running in
                // one process never read each other's set.
                foreach (var image in images.Images)
                    options.HostImplementationImages.Add(image.GetName().Name ?? string.Empty);
                CompileOutcome outcome = CompileAll(request, templates, images, options, diagnostics,
                    FormatVersion(hostEngine));
                if (outcome == null)
                    return 1;
                try
                {
                    WriteOutputs(request, outcome, generatedNamespace);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Every template compiled, so this is a reported error (exit 1), not the exit-3
                    // host fault reserved for failures before any template compiled.
                    diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildEmitterFault,
                        "outputs could not be written: " + ex.Message + ".");
                    return 1;
                }

                Stamp.Write(request.Stamp, digest);
                return 0;
            }
        }

        private sealed class ResolvedOptions
        {
            internal OutputProfile OutputProfile;
            internal ExpressionMode ExpressionMode;
            internal bool TrimDirectiveLines;
            internal int MaxRecursionCount;
            internal string GeneratedNamespace;
            internal string BuildVersion;
            internal FunctionRegistry Functions;
            internal readonly HashSet<string> HostImplementationImages =
                new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class CompileOutcome
        {
            internal CompiledArtifact Artifact;
            internal readonly List<SourceEmitter.EmittedTemplate> Emitted =
                new List<SourceEmitter.EmittedTemplate>();
            internal readonly List<PrintPart> PrintParts = new List<PrintPart>();
        }

        /// <summary>One template's printer input: the in-memory form record (live types, bound
        /// trees) beside the payload-table counts its rows convert to, so the printer correlates
        /// merged artifact rows back to record rows after the merge re-bases every index.</summary>
        private sealed class PrintPart
        {
            internal TemplateInput Template;
            internal FormRecord Record;
            internal CompiledArtifact Single;
            internal int Members;
            internal int Expressions;
            internal int CSharp;
            internal int TemplateIndex;
            internal int EmittedIndex;
        }

        private const string KeyShapeRule =
            "keys must be non-empty relative paths without '.' or '..' segments";

        private static TemplateInput ReadInput(CompileRequest request, string path,
            DiagnosticWriter diagnostics)
        {
            string full = Stamp.Resolve(request, path);
            string text;
            try
            {
                text = File.ReadAllText(full);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildUnreadableFile,
                    Format(HeddleDiagnosticIds.BuildUnreadableFile, full, ex.Message));
                return null;
            }

            return new TemplateInput
            {
                FullPath = full,
                Text = text,
                ContentHash = Heddle.Precompiled.ContentHash.HashText(text)
            };
        }

        private static int Usage(TextWriter stderr, string message)
        {
            stderr.WriteLine("heddle: " + message);
            return 2;
        }

        private static int OptionError(CompileRequest request, DiagnosticWriter diagnostics,
            string option, string value, string expected)
        {
            diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildOptionParseError,
                Format(HeddleDiagnosticIds.BuildOptionParseError, value, option, expected));
            return 1;
        }

        private static string Format(string id, params object[] args)
        {
            if (HeddleDiagnosticCatalog.TryGet(id, out var info) && info.MessageFormat != null)
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    info.MessageFormat, args);
            return id + ": " + string.Join(" ", args);
        }

        private static string FormatVersion(Version version) =>
            version == null ? "0.0.0" : PrecompiledSchema.FormatEngineVersion(version);

        /// <summary>Derives keys, registered names and the duplicate/case-twin/sanitized checks.
        /// Returns false when an error was reported.</summary>
        private static bool DeriveKeys(CompileRequest request, List<TemplateInput> templates,
            DiagnosticWriter diagnostics)
        {
            var seenKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            var aliasOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var sanitizedOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            bool ok = true;

            // Pass 1: keys.
            foreach (var template in templates)
            {
                string key;
                bool outOfRoot;
                if (!string.IsNullOrEmpty(template.Item.Key))
                {
                    if (!TemplateKey.TryNormalize(template.Item.Key, out key))
                    {
                        diagnostics.Error(template.FullPath,
                            HeddleDiagnosticIds.BuildInvalidKeyMetadata,
                            Format(HeddleDiagnosticIds.BuildInvalidKeyMetadata, template.FullPath,
                                "Key=\"" + template.Item.Key + "\" is not a usable key — " + KeyShapeRule));
                        ok = false;
                        continue;
                    }

                    outOfRoot = false;
                }
                else if (TemplateKey.TryMakeRelative(template.FullPath, request.Root, out key))
                {
                    outOfRoot = false;
                }
                else
                {
                    outOfRoot = true;
                    string flat = template.FullPath;
                    int separator = flat.LastIndexOfAny(new[] { '/', '\\' });
                    if (separator >= 0)
                        flat = flat.Substring(separator + 1);
                    if (!TemplateKey.TryNormalize(flat, out key))
                        continue;
                }

                if (outOfRoot)
                    diagnostics.Warning(template.FullPath, HeddleDiagnosticIds.BuildTemplateOutsideRoot,
                        Format(HeddleDiagnosticIds.BuildTemplateOutsideRoot, template.FullPath,
                            string.IsNullOrEmpty(request.Root) ? "<unset>" : request.Root, key));
                if (seenKeys.TryGetValue(key, out string firstPath))
                {
                    diagnostics.Error(template.FullPath, HeddleDiagnosticIds.BuildDuplicateKey,
                        Format(HeddleDiagnosticIds.BuildDuplicateKey, key, firstPath, template.FullPath));
                    ok = false;
                    continue;
                }

                foreach (var existing in seenKeys.Keys)
                    if (!string.Equals(existing, key, StringComparison.Ordinal) &&
                        string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))
                        diagnostics.Warning(template.FullPath,
                            HeddleDiagnosticIds.BuildCaseOnlyKeyTwin,
                            Format(HeddleDiagnosticIds.BuildCaseOnlyKeyTwin, existing, key, key));
                seenKeys[key] = template.FullPath;
                template.Key = key;
            }

            if (!ok)
                return false;

            // Pass 2: registered names (additive; a broken one is reported, not registered).
            foreach (var template in templates)
            {
                if (template.Key == null || string.IsNullOrEmpty(template.Item.Name))
                    continue;
                if (!TemplateKey.TryNormalize(template.Item.Name, out string name))
                {
                    diagnostics.Error(template.FullPath,
                        HeddleDiagnosticIds.BuildInvalidKeyMetadata,
                        Format(HeddleDiagnosticIds.BuildInvalidKeyMetadata, template.FullPath,
                            "Name=\"" + template.Item.Name + "\" is not a usable import name — " +
                            KeyShapeRule));
                    ok = false;
                    continue;
                }

                if (string.Equals(name, template.Key, StringComparison.Ordinal))
                    continue;
                if (seenKeys.ContainsKey(name) || aliasOwners.ContainsKey(name))
                {
                    diagnostics.Error(template.FullPath,
                        HeddleDiagnosticIds.BuildInvalidKeyMetadata,
                        Format(HeddleDiagnosticIds.BuildInvalidKeyMetadata, template.FullPath,
                            "Name=\"" + template.Item.Name + "\" registers the import spelling '" + name +
                            "', which another template already answers to. Registered names share the " +
                            "import-path namespace with template keys, so the name must be free."));
                    ok = false;
                    continue;
                }

                aliasOwners[name] = template.FullPath;
                template.RegisteredName = name;
            }

            if (!ok)
                return false;

            // Pass 3: sanitized entry-class names (HED7010). Two names are the generated code's own: the
            // artifact class, and Generate — the entry point callers name, which a class called Generate
            // could not declare.
            // Import-only rows emit no wrapper, so they take no sanitized name.
            foreach (var template in templates)
            {
                if (template.Key == null || template.Item.IsImportOnly)
                    continue;
                string sanitized = SanitizeName.ForKey(template.Key);
                sanitizedOwners.TryGetValue(sanitized, out string owner);
                bool reserved = string.Equals(sanitized, "HeddleArtifact", StringComparison.Ordinal) ||
                    string.Equals(sanitized, "Generate", StringComparison.Ordinal);
                if (reserved ||
                    (owner != null && !string.Equals(owner, template.Key, StringComparison.Ordinal)))
                {
                    diagnostics.Error(template.FullPath,
                        HeddleDiagnosticIds.BuildDuplicateSanitizedName,
                        Format(HeddleDiagnosticIds.BuildDuplicateSanitizedName,
                            reserved ? "the generated code itself" : owner, template.Key, sanitized));
                    ok = false;
                    continue;
                }

                sanitizedOwners[sanitized] = template.Key;
            }

            return ok;
        }

        /// <summary>The invocation's import map: every template key and validated registered
        /// name to its text. Mirrors the loader's artifact-row map (see ImportMap there); kept here
        /// because the host cannot see the engine's internals and the contract is twenty lines.</summary>
        private static Dictionary<string, string> ImportContents(CompileRequest request,
            List<TemplateInput> templates)
        {
            var contents = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                if (template.Key == null)
                    continue;
                if (!contents.ContainsKey(template.Key))
                    contents.Add(template.Key, template.Text);
                // A template answering to a registered name serves under it too, exactly as the
                // loader serves the row's registered name from the artifact.
                if (!string.IsNullOrEmpty(template.Item.Name) &&
                    TemplateKey.TryNormalize(template.Item.Name, out string alias) &&
                    !contents.ContainsKey(alias))
                    contents.Add(alias, template.Text);
            }

            foreach (var import in request.ImportOnly)
            {
                if (string.IsNullOrEmpty(import.Name) ||
                    !TemplateKey.TryNormalize(import.Name, out string name))
                    continue;
                if (contents.ContainsKey(name))
                    continue;
                try
                {
                    contents.Add(name, File.ReadAllText(Stamp.Resolve(request, import.Path)));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Unreadable import-only files already failed the run at intake.
                }
            }

            return contents;
        }

        private static Func<string, string> ImportReaderFor(CompileRequest request,
            List<TemplateInput> templates, ImportPair pair, DiagnosticWriter diagnostics)
        {
            var contents = ImportContents(request, templates);
            var namesByKey = NamesByKey(templates);
            var advised = new HashSet<string>(StringComparer.Ordinal);
            return spelling =>
            {
                // Spellings normalize before the map lookup (Banner meets Banner.heddle); the
                // disk fallback reads the raw spelling. Mirrors the engine's ImportMap.
                if (spelling != null && TemplateKey.TryNormalize(spelling, out string key) &&
                    contents.TryGetValue(key, out string content))
                {
                    // HED7028: the spelling is a template's key while that template also carries a
                    // registered Name; both resolve, the name is the advised spelling.
                    string name;
                    if (namesByKey.TryGetValue(key, out name) && advised.Add((pair.Current ?? string.Empty) + "|" + key))
                        diagnostics.Warning(pair.Current ?? request.Project,
                            HeddleDiagnosticIds.BuildNamedTemplateImportedByKey,
                            Format(HeddleDiagnosticIds.BuildNamedTemplateImportedByKey, spelling, name));
                    return content;
                }
                using (var file = File.OpenText(Path.Combine(request.Root ?? string.Empty, spelling)))
                    return file.ReadToEnd();
            };
        }

        /// <summary>Every template key whose item also carries a validated registered name, for HED7028.</summary>
        private static Dictionary<string, string> NamesByKey(List<TemplateInput> templates)
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                if (template.Key == null || string.IsNullOrEmpty(template.Item.Name))
                    continue;
                string alias;
                if (TemplateKey.TryNormalize(template.Item.Name, out alias) && alias != template.Key &&
                    !names.ContainsKey(template.Key))
                    names.Add(template.Key, template.Item.Name);
            }
            return names;
        }

        private static Func<string, string> ImportIdentifierFor(CompileRequest request,
            List<TemplateInput> templates)
        {
            var contents = ImportContents(request, templates);
            return spelling =>
            {
                if (spelling != null && TemplateKey.TryNormalize(spelling, out string key) &&
                    contents.ContainsKey(key))
                    return "import:" + key;
                try
                {
                    return Path.GetFullPath(Path.Combine(request.Root ?? string.Empty, spelling));
                }
                catch (Exception)
                {
                    return spelling;
                }
            };
        }

        private static string StubModelName(TemplateInput template)
        {
            if (!string.IsNullOrEmpty(template.Item.ModelType))
                return template.Item.ModelType;
            string scanned = Probe.ScanModelDirective(template.Text);
            return string.IsNullOrEmpty(scanned) ? "object" : scanned;
        }

        private static CompileOutcome CompileAll(CompileRequest request, List<TemplateInput> templates,
            ImageLoadContext images, ResolvedOptions options, DiagnosticWriter diagnostics,
            string engineVersion)
        {
            var outcome = new CompileOutcome();
            var parts = new List<CompiledArtifact>();
            bool failed = false;
            var imports = new ImportPair(ImportIdentifierFor(request, templates));
            imports.Reader = ImportReaderFor(request, templates, imports, diagnostics);
            foreach (var template in templates)
            {
                if (template.Key == null)
                    continue;
                // A Precompile="false" item is not compiled: its own errors stay unreported by
                // request, and its row carries only the text that @<< imports of it replay from.
                if (template.Item.IsImportOnly)
                {
                    parts.Add(ImportOnlyPart(template, options, engineVersion));
                    continue;
                }
                imports.Current = template.FullPath;
                var single = CompileOne(request, template, imports, images, options, diagnostics,
                    engineVersion, outcome, parts.Count);
                if (single == null)
                    failed = true;
                else
                    parts.Add(single);
            }

            // The pass continues past a template the host threw on (HED7020): the remaining
            // templates and their diagnostics still emit. Outputs are written only when every
            // template compiled.
            if (failed || parts.Count == 0)
                return null;
            outcome.Artifact = CompiledArtifactMerger.Merge(parts);
            PrintGeneratedSites(request, outcome, diagnostics);
            return outcome;
        }

        /// <summary>The row a <c>Precompile="false"</c> item contributes: key, registered name, content
        /// hash and raw text (one document with no elements), so every <c>@&lt;&lt;</c> that imports it
        /// replays from the artifact. No compile, no sites, no entry point; the registry skips the
        /// row and the template renders through the dynamic path.</summary>
        private static CompiledArtifact ImportOnlyPart(TemplateInput template, ResolvedOptions options,
            string engineVersion)
        {
            var part = new CompiledArtifact
            {
                Header = new CompiledHeader
                {
                    EngineVersion = engineVersion,
                    BuilderVersion = options.BuildVersion,
                    ExpressionMode = options.ExpressionMode.ToString(),
                    TrimDirectiveLines = options.TrimDirectiveLines,
                    DefaultOutputProfile = options.OutputProfile.ToString()
                }
            };
            part.Documents.Add(new CompiledDocument
            {
                RawText = template.Text ?? string.Empty,
                ParseFacts = new CompiledParseFacts()
            });
            part.Templates.Add(new CompiledTemplateRow
            {
                Key = template.Key,
                RegisteredName = template.RegisteredName,
                ContentHash = template.ContentHash ?? string.Empty,
                IsImportOnly = true,
                Options = new CompiledOptionsFingerprint
                {
                    Profile = options.OutputProfile.ToString(),
                    Mode = options.ExpressionMode.ToString(),
                    Trim = options.TrimDirectiveLines
                },
                RootDocumentRef = 0,
                SiteCount = 0
            });
            return part;
        }

        private sealed class ImportPair
        {
            internal ImportPair(Func<string, string> identifier)
            {
                Identifier = identifier;
            }

            internal Func<string, string> Reader { get; set; }

            internal Func<string, string> Identifier { get; }

            /// <summary>The template being compiled, so an import advisory is filed against the importer.</summary>
            internal string Current { get; set; }
        }

        private static CompiledArtifact CompileOne(CompileRequest request, TemplateInput template,
            ImportPair imports, ImageLoadContext images, ResolvedOptions options,
            DiagnosticWriter diagnostics, string engineVersion, CompileOutcome outcome, int templateIndex)
        {
            OutputProfile profile = options.OutputProfile;
            if (!string.IsNullOrEmpty(template.Item.OutputProfile))
            {
                if (!HeddleBuildOptions.TryReadEnum(template.Item.OutputProfile,
                    options.OutputProfile, out profile))
                {
                    diagnostics.Error(template.FullPath, HeddleDiagnosticIds.BuildOptionParseError,
                        Format(HeddleDiagnosticIds.BuildOptionParseError, template.Item.OutputProfile,
                            "OutputProfile", HeddleBuildOptions.ExpectedValues<OutputProfile>()));
                    return null;
                }
            }

            Type modelType = null;
            string directive = Probe.ScanModelDirective(template.Text);
            string spelling = !string.IsNullOrEmpty(template.Item.ModelType)
                ? template.Item.ModelType
                : directive;
            if (!string.IsNullOrEmpty(spelling))
            {
                modelType = images.ResolveModelType(spelling, Probe.ScanUsingDirectives(template.Text));
                if (modelType == null)
                {
                    diagnostics.Error(template.FullPath,
                        HeddleDiagnosticIds.BuildUnresolvableModelType,
                        Format(HeddleDiagnosticIds.BuildUnresolvableModelType, spelling));
                    return null;
                }
                // HED7032: the item's ModelType and the in-file @model directive both speak, and they
                // resolve to different types. The runtime reads only the directive.
                if (!string.IsNullOrEmpty(template.Item.ModelType) && !string.IsNullOrEmpty(directive) &&
                    !string.Equals(directive, template.Item.ModelType, StringComparison.Ordinal))
                {
                    // Resolved the way the engine resolves the directive: the template's own @using
                    // imports over the registered model assemblies.
                    Type directiveType = null;
                    try
                    {
                        directiveType = Heddle.Helpers.ReflectionHelper.ResolveType(directive,
                            Probe.ScanUsingDirectives(template.Text));
                    }
                    catch (Exception)
                    {
                        // Unresolvable or ambiguous: the engine reports that itself during the compile.
                    }
                    if (directiveType != null && directiveType != modelType)
                    {
                        diagnostics.Error(template.FullPath,
                            HeddleDiagnosticIds.BuildConflictingModelTypeDeclarations,
                            Format(HeddleDiagnosticIds.BuildConflictingModelTypeDeclarations,
                                template.Item.ModelType, directive, modelType.FullName, directiveType.FullName));
                        return null;
                    }
                }
            }

            ExType modelEx = modelType == null ? ExType.Dynamic : new ExType(modelType);
            var templateOptions = new TemplateOptions(Path.GetFileNameWithoutExtension(template.FullPath))
            {
                RootPath = request.Root,
                FileNamePostfix = ".heddle",
                OutputProfile = profile,
                EnableFileChangeCheck = false,
                ExpressionMode = options.ExpressionMode,
                TrimDirectiveLines = options.TrimDirectiveLines,
                MaxRecursionCount = options.MaxRecursionCount,
                Functions = options.Functions
            };

            var context = new CompileContext(templateOptions, modelEx);
            context.DeferUnboundFunctions = true;
            context.RecordForm = true;
            context.FormRecord.HostImplementationImages = options.HostImplementationImages;
            // Composition imports resolve against the invocation's items first: every key and
            // validated registered name serves its text, anything else reads off disk. The same
            // contract the loader honors from the artifact rows, so build and load expand alike.
            context.ImportReader = imports.Reader;
            context.ImportIdentifier = imports.Identifier;
            HeddleTemplate compiled;
            try
            {
                compiled = new HeddleTemplate(template.Text, context);
            }
            catch (Exception ex)
            {
                diagnostics.Error(template.FullPath, HeddleDiagnosticIds.BuildEmitterFault,
                    Format(HeddleDiagnosticIds.BuildEmitterFault, template.FullPath,
                        ex.GetType().Name, ex.Message));
                return null;
            }

            bool failed = false;
            foreach (var error in context.CompileErrors)
            {
                failed = true;
                string id = error.DiagnosticId ?? HeddleDiagnosticIds.BuildForwardedError;
                string message = error.DiagnosticId == null
                    ? Format(HeddleDiagnosticIds.BuildForwardedError, error.Error)
                    : error.Error;
                bool warning = error is HeddleCompileWarning;
                if (warning && error.DiagnosticId == null)
                {
                    id = HeddleDiagnosticIds.BuildForwardedWarning;
                    message = Format(HeddleDiagnosticIds.BuildForwardedWarning, error.Error);
                }

                if (error.Position.Length <= 0 && error.Position.StartIndex <= 0)
                {
                    if (warning)
                        diagnostics.Warning(template.FullPath, id, message);
                    else
                        diagnostics.Error(template.FullPath, id, message);
                }
                else if (warning)
                {
                    diagnostics.Warning(template.FullPath, template.Text, error.Position.StartIndex,
                        error.Position.Length, id, message);
                }
                else
                {
                    diagnostics.Error(template.FullPath, template.Text, error.Position.StartIndex,
                        error.Position.Length, id, message);
                }
            }

            foreach (var warning in context.CompileWarnings)
            {
                string id = warning.DiagnosticId ?? HeddleDiagnosticIds.BuildForwardedWarning;
                string message = warning.DiagnosticId == null
                    ? Format(HeddleDiagnosticIds.BuildForwardedWarning, warning.Error)
                    : warning.Error;
                if (warning.Position.Length <= 0 && warning.Position.StartIndex <= 0)
                    diagnostics.Warning(template.FullPath, id, message);
                else
                    diagnostics.Warning(template.FullPath, template.Text, warning.Position.StartIndex,
                        warning.Position.Length, id, message);
            }

            // A fault raised while the compile was being finished is caught by the engine and kept on the
            // result, not on the context's list read above. Returning on it unreported would fail the build
            // with nothing but an exit code.
            if (!compiled.CompileResult.Success)
            {
                bool reported = failed;
                foreach (var error in compiled.CompileResult.ErrorList)
                {
                    if (context.CompileErrors.Contains(error))
                        continue;
                    reported = true;
                    string detail = error.Exception != null
                        ? error.Exception.GetType().Name + ": " + error.Exception.Message
                        : error.Error;
                    diagnostics.Error(template.FullPath, HeddleDiagnosticIds.BuildEmitterFault,
                        Format(HeddleDiagnosticIds.BuildEmitterFault, template.FullPath, "compile", detail));
                }

                if (!reported)
                    diagnostics.Error(template.FullPath, HeddleDiagnosticIds.BuildEmitterFault,
                        Format(HeddleDiagnosticIds.BuildEmitterFault, template.FullPath, "compile",
                            "the engine reported failure without an error"));
            }

            if (failed || !compiled.CompileResult.Success)
                return null;

            string sanitized = SanitizeName.ForKey(template.Key);
            CompiledArtifact single;
            try
            {
                single = context.FormRecord.ToArtifact(engineVersion, options.BuildVersion, template.Key,
                    template.ContentHash, template.RegisteredName,
                    options.GeneratedNamespace + "." + sanitized, modelEx,
                    false, false, profile.ToString(), options.ExpressionMode.ToString(),
                    options.TrimDirectiveLines);
            }
            catch (Exception ex)
            {
                diagnostics.Error(template.FullPath, HeddleDiagnosticIds.BuildEmitterFault,
                    Format(HeddleDiagnosticIds.BuildEmitterFault, template.FullPath,
                        ex.GetType().Name, ex.Message));
                return null;
            }

            // Every compiled row emits its table arms, site methods and entry-point wrapper; the
            // printer runs post-merge, when every template index is final, and HED7031 reports
            // from the merged rows then. Import-only items never reach here (ImportOnlyPart).
            // The wrapper's model parameter is spelled by the printer's type table (generic arguments,
            // nested and internal types), never from the reflection name. A type the consumer cannot name — a
            // private nested model, an internal one it has no access to — leaves the wrapper taking object,
            // in its parameter and its typeof alike: the row still binds and renders.
            string modelSpelling = "object";
            bool modelIsPublic = true;
            if (modelType != null &&
                !Sites.TypeNamePrinter.TrySpellModel(modelType, request.AssemblyName, out modelSpelling,
                    out modelIsPublic, out _))
            {
                modelSpelling = "object";
                modelIsPublic = true;
            }

            var emitted = new SourceEmitter.EmittedTemplate
            {
                Key = template.Key,
                Sanitized = sanitized,
                ModelTypeName = modelSpelling,
                IsPublic = modelIsPublic,
                ContentHash = template.ContentHash ?? string.Empty,
                EmitWrapper = true
            };
            int emittedIndex = outcome.Emitted.Count;
            outcome.Emitted.Add(emitted);
            outcome.PrintParts.Add(new PrintPart
            {
                Template = template,
                Record = context.FormRecord,
                Single = single,
                Members = single.Members != null ? single.Members.Count : 0,
                Expressions = single.Expressions != null ? single.Expressions.Count : 0,
                CSharp = single.CSharpSites != null ? single.CSharpSites.Count : 0,
                TemplateIndex = templateIndex,
                EmittedIndex = emittedIndex
            });
            return single;
        }

        /// <summary>Runs the site printer over every compiled template once the merge has fixed
        /// every template index, attaches the printed sites to the emitted rows, and reports
        /// HED7031 from the merged rows.</summary>
        private static void PrintGeneratedSites(CompileRequest request, CompileOutcome outcome,
            DiagnosticWriter diagnostics)
        {
            int memberBase = 0;
            int expressionBase = 0;
            int csharpBase = 0;
            foreach (var part in outcome.PrintParts)
            {
                var input = new Sites.SitePrinter.Input
                {
                    Record = part.Record,
                    ContentHash = part.Template.ContentHash ?? string.Empty,
                    TemplateIndex = part.TemplateIndex,
                    MemberBase = memberBase,
                    ExpressionBase = expressionBase,
                    CSharpBase = csharpBase
                };
                var printed = Sites.SitePrinter.Print(input, outcome.Artifact);
                var emitted = outcome.Emitted[part.EmittedIndex];
                emitted.TemplateIndex = part.TemplateIndex;
                foreach (var site in printed.Sites)
                    emitted.Sites.Add(site);
                foreach (var decline in printed.Declines)
                    emitted.Declines.Add(decline);
                foreach (var cls in printed.CSharpClasses)
                    emitted.CSharpClasses.Add(cls);
                foreach (var ns in printed.CSharpUsings)
                    emitted.CSharpUsings.Add(ns);
                memberBase += part.Members;
                expressionBase += part.Expressions;
                csharpBase += part.CSharp;
                ReportNotPrecompiled(request, part, outcome, emitted, diagnostics);
            }
        }

        /// <summary>HED7031, Info, once per template whose row carries a refusal site, a late-bound
        /// site, a C# site carried as data, or a printer decline (n sites rebuilt at load: kinds
        /// and positions).</summary>
        private static void ReportNotPrecompiled(CompileRequest request, PrintPart part,
            CompileOutcome outcome, SourceEmitter.EmittedTemplate emitted,
            DiagnosticWriter diagnostics)
        {
            var artifact = outcome.Artifact;
            if (artifact.Templates == null || part.TemplateIndex < 0 ||
                part.TemplateIndex >= artifact.Templates.Count)
                return;
            var row = artifact.Templates[part.TemplateIndex];
            var template = part.Template;
            var refusalParts = new List<string>();
            var refusals = part.Record != null ? part.Record.Refusals : null;
            for (int i = 0; i < row.RefusalSites.Count; i++)
            {
                var site = row.RefusalSites[i];
                ToOneBased(template.Text, site.PositionStart, out int line, out int column);
                refusalParts.Add("refusal site at (" + line + "," + column + ") " + site.Class +
                    " '" + site.Detail + "'");
                ReportRefusalSite(template, site, refusals != null && i < refusals.Count ? refusals[i] : null,
                    artifact, diagnostics);
            }

            var lateBound = new List<string>();
            var seenLate = new HashSet<string>(StringComparer.Ordinal);
            foreach (int functionRef in row.FunctionRefs)
            {
                if (artifact.Functions == null || functionRef < 0 ||
                    functionRef >= artifact.Functions.Count)
                    continue;
                var function = artifact.Functions[functionRef];
                if (function.Target == null && seenLate.Add(function.Name))
                    lateBound.Add(function.Name);
            }

            int printedCSharp = 0;
            foreach (var site in emitted.Sites)
                if (site.Kind == "CSharp")
                    printedCSharp++;
            int csharp = 0;
            if (artifact.Sites != null)
                foreach (var site in artifact.Sites)
                    if (site != null && site.TemplateIndex == part.TemplateIndex &&
                        site.Kind == CompiledSiteKind.EmbeddedCSharp)
                        csharp++;
            csharp -= printedCSharp;
            if (csharp < 0)
                csharp = 0;
            if (refusalParts.Count == 0 && lateBound.Count == 0 && csharp == 0 &&
                emitted.Declines.Count == 0)
                return;
            var parts = new List<string>();
            parts.AddRange(refusalParts);
            if (lateBound.Count != 0)
                parts.Add("late-bound functions: " + string.Join(", ", lateBound));
            if (csharp != 0)
                parts.Add(csharp + " C# site" + (csharp == 1 ? "" : "s") + " carried as data");
            if (emitted.Declines.Count != 0)
            {
                var declines = new List<string>();
                bool accessibility = false;
                foreach (var decline in emitted.Declines)
                {
                    declines.Add(decline.ToString());
                    accessibility |= decline.IsAccessibility;
                }

                parts.Add(emitted.Declines.Count + " site" +
                    (emitted.Declines.Count == 1 ? "" : "s") + " rebuilt at load: " +
                    string.Join("; ", declines.ToArray()));
                if (accessibility)
                    parts.Add("generated code is compiled into your assembly and can name only public types and " +
                        "members, so those sites are built at load instead — the output is the same, but a " +
                        "strict-load (trimmed or NativeAOT) host refuses the template; to precompile them, make " +
                        "the model type and the members the template reads public, or type the template with a " +
                        "public interface or DTO");
            }

            diagnostics.Info(template.FullPath, HeddleDiagnosticIds.BuildTemplateNotPrecompiled,
                "not fully precompiled: " + string.Join("; ", parts));
        }

        /// <summary>An unbindable call typing is HED7014 (warning, at the call, naming the function); an
        /// unsupported extension is HED7033 (warning, at the call, carrying the extension's declared reason
        /// verbatim). A reflection-order value has no id of its own and rides on HED7031.</summary>
        private static void ReportRefusalSite(TemplateInput template, PrecompiledRefusalSite site,
            FormRefusalData refusal, CompiledArtifact artifact, DiagnosticWriter diagnostics)
        {
            switch (site.Class)
            {
                case PrecompiledRefusalClass.UnbindableCallTyping:
                    diagnostics.Warning(template.FullPath, template.Text, site.PositionStart,
                        site.PositionLength, HeddleDiagnosticIds.BuildUnresolvableFunction,
                        Format(HeddleDiagnosticIds.BuildUnresolvableFunction, site.Detail));
                    break;
                case PrecompiledRefusalClass.UnsupportedExtension:
                {
                    string name = refusal != null && refusal.Item != null ? refusal.Item.ExtensionName : "?";
                    string typeName = "?";
                    if (artifact.Extensions != null)
                        foreach (var extension in artifact.Extensions)
                            if (extension != null && string.Equals(extension.RegistryName, name, StringComparison.Ordinal))
                            {
                                typeName = extension.Type != null ? extension.Type.Nominal() : "?";
                                break;
                            }
                    diagnostics.Warning(template.FullPath, template.Text, site.PositionStart,
                        site.PositionLength, HeddleDiagnosticIds.BuildExtensionPrecompileUnsupported,
                        Format(HeddleDiagnosticIds.BuildExtensionPrecompileUnsupported, name, typeName, site.Detail));
                    break;
                }
            }
        }

        private static void ToOneBased(string text, int offset, out int line, out int column)
        {
            line = 1;
            column = 1;
            if (string.IsNullOrEmpty(text))
                return;
            if (offset < 0)
                offset = 0;
            if (offset > text.Length)
                offset = text.Length;
            for (int i = 0; i < offset; i++)
                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
        }

        private static void WriteOutputs(CompileRequest request, CompileOutcome outcome,
            string generatedNamespace)
        {
            string artifactDirectory = Path.GetDirectoryName(request.ArtifactOut);
            if (!string.IsNullOrEmpty(artifactDirectory))
                Directory.CreateDirectory(artifactDirectory);
            // The digest is computed by the writer before the source prints: the generated table's
            // ArtifactDigest names the exact bytes beside it, and the loader fails fast on any skew.
            byte[] image = CompiledFormWriter.Write(outcome.Artifact);
            using (var destination = File.Create(request.ArtifactOut))
                destination.Write(image, 0, image.Length);
            SourceEmitter.Write(request.SourceOut, generatedNamespace, outcome.Emitted,
                outcome.Artifact.Header != null ? outcome.Artifact.Header.EngineVersion : string.Empty,
                outcome.Artifact.DigestHex ?? string.Empty);
        }
    }
}
