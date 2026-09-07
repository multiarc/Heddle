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
                    diagnostics.Error(request.Project, "HED7035",
                        "Heddle.Build " + buildVersion + " compiles with Heddle " +
                        FormatVersion(hostEngine) + " but the engine reference '" +
                        request.EngineReference + "' could not be read: " + ex.Message + ".");
                    return 1;
                }

                if (referenced != hostEngine)
                {
                    diagnostics.Error(request.Project, "HED7035",
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
                string full = Stamp.Resolve(request, item.Path);
                string text;
                try
                {
                    text = File.ReadAllText(full);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildUnreadableFile,
                        Format(HeddleDiagnosticIds.BuildUnreadableFile, full, ex.Message));
                    return 1;
                }

                templates.Add(new TemplateInput
                {
                    Item = item,
                    FullPath = full,
                    Text = text,
                    ContentHash = Heddle.Precompiled.ContentHash.HashText(text)
                });
            }

            bool importsOk = true;
            foreach (var import in request.ImportOnly)
            {
                string full = Stamp.Resolve(request, import.Path);
                try
                {
                    File.ReadAllText(full);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildUnreadableFile,
                        Format(HeddleDiagnosticIds.BuildUnreadableFile, full, ex.Message));
                    return 1;
                }

                if (!string.IsNullOrEmpty(import.Name) &&
                    !TemplateKey.TryNormalize(import.Name, out _))
                {
                    diagnostics.Error(request.Project, HeddleDiagnosticIds.BuildInvalidKeyMetadata,
                        Format(HeddleDiagnosticIds.BuildInvalidKeyMetadata, full,
                            "Name=\"" + import.Name + "\" is not a usable import name — " +
                            KeyShapeRule));
                    importsOk = false;
                }
            }

            if (!importsOk)
                return 1;

            if (!DeriveKeys(request, templates, diagnostics))
                return 1;

            // The stubs pass writes wrapper stubs with no engine compile, no image load, no artifact,
            // source or stamp — so a following real build finds its outputs out of date.
            if (request.StubsOnly != null)
            {
                var stubs = new List<SourceEmitter.StubTemplate>();
                foreach (var template in templates)
                    stubs.Add(new SourceEmitter.StubTemplate
                    {
                        Sanitized = SanitizeName.ForKey(template.Key),
                        ModelTypeName = StubModelName(template)
                    });
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
                        diagnostics.Error(request.Project, "HED7036",
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
                    diagnostics.Error(request.Project, "HED7036",
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
                var outcome = CompileAll(request, templates, images, options, diagnostics,
                    FormatVersion(hostEngine));
                if (outcome == null)
                    return 1;
                try
                {
                    WriteOutputs(request, outcome, generatedNamespace);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    stderr.WriteLine("heddle: outputs could not be written: " + ex.Message + ".");
                    return 3;
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
        }

        private sealed class CompileOutcome
        {
            internal CompiledArtifact Artifact;
            internal readonly List<SourceEmitter.EmittedTemplate> Emitted =
                new List<SourceEmitter.EmittedTemplate>();
        }

        private const string KeyShapeRule =
            "keys must be non-empty relative paths without '.' or '..' segments";

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
            }

            if (!ok)
                return false;

            // Pass 3: sanitized entry-class names (HED7010; HeddleArtifact is reserved).
            foreach (var template in templates)
            {
                if (template.Key == null)
                    continue;
                string sanitized = SanitizeName.ForKey(template.Key);
                sanitizedOwners.TryGetValue(sanitized, out string owner);
                if (string.Equals(sanitized, "HeddleArtifact", StringComparison.Ordinal) ||
                    (owner != null && !string.Equals(owner, template.Key, StringComparison.Ordinal)))
                {
                    diagnostics.Error(template.FullPath,
                        HeddleDiagnosticIds.BuildDuplicateSanitizedName,
                        Format(HeddleDiagnosticIds.BuildDuplicateSanitizedName,
                            owner ?? template.Key, template.Key, sanitized));
                    ok = false;
                    continue;
                }

                sanitizedOwners[sanitized] = template.Key;
            }

            return ok;
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
            foreach (var template in templates)
            {
                if (template.Key == null)
                    continue;
                var single = CompileOne(request, template, images, options, diagnostics, engineVersion,
                    outcome);
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
            return outcome;
        }

        private static CompiledArtifact CompileOne(CompileRequest request, TemplateInput template,
            ImageLoadContext images, ResolvedOptions options, DiagnosticWriter diagnostics,
            string engineVersion, CompileOutcome outcome)
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
            string spelling = !string.IsNullOrEmpty(template.Item.ModelType)
                ? template.Item.ModelType
                : Probe.ScanModelDirective(template.Text);
            if (!string.IsNullOrEmpty(spelling))
            {
                modelType = images.ResolveModelType(spelling);
                if (modelType == null)
                {
                    diagnostics.Error(template.FullPath,
                        HeddleDiagnosticIds.BuildUnresolvableModelType,
                        Format(HeddleDiagnosticIds.BuildUnresolvableModelType, spelling));
                    return null;
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

            if (failed || !compiled.CompileResult.Success)
                return null;

            string sanitized = SanitizeName.ForKey(template.Key);
            CompiledArtifact single;
            try
            {
                single = context.FormRecord.ToArtifact(engineVersion, options.BuildVersion, template.Key,
                    template.ContentHash, null, options.GeneratedNamespace + "." + sanitized, modelEx,
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

            ReportNotPrecompiled(request, template, single, diagnostics);
            outcome.Emitted.Add(new SourceEmitter.EmittedTemplate
            {
                Key = template.Key,
                Sanitized = sanitized,
                ModelTypeName = modelType == null
                    ? "object"
                    : "global::" + modelType.FullName.Replace('+', '.')
            });
            return single;
        }

        /// <summary>HED7031, Info, once per template whose row carries a refusal site, a late-bound
        /// site or a C# site carried as data.</summary>
        private static void ReportNotPrecompiled(CompileRequest request, TemplateInput template,
            CompiledArtifact single, DiagnosticWriter diagnostics)
        {
            if (single.Templates.Count == 0)
                return;
            var row = single.Templates[0];
            var refusalParts = new List<string>();
            foreach (var site in row.RefusalSites)
            {
                ToOneBased(template.Text, site.PositionStart, out int line, out int column);
                refusalParts.Add("refusal site at (" + line + "," + column + ") " + site.Class +
                    " '" + site.Detail + "'");
            }

            var lateBound = new List<string>();
            var seenLate = new HashSet<string>(StringComparer.Ordinal);
            foreach (int functionRef in row.FunctionRefs)
            {
                if (functionRef < 0 || functionRef >= single.Functions.Count)
                    continue;
                var function = single.Functions[functionRef];
                if (function.Target == null && seenLate.Add(function.Name))
                    lateBound.Add(function.Name);
            }

            int csharp = 0;
            foreach (var site in single.Sites)
                if (site.Kind == CompiledSiteKind.EmbeddedCSharp)
                    csharp++;
            if (refusalParts.Count == 0 && lateBound.Count == 0 && csharp == 0)
                return;
            var parts = new List<string>();
            parts.AddRange(refusalParts);
            if (lateBound.Count != 0)
                parts.Add("late-bound functions: " + string.Join(", ", lateBound));
            if (csharp != 0)
                parts.Add(csharp + " C# site" + (csharp == 1 ? "" : "s") + " carried as data");
            diagnostics.Info(template.FullPath, HeddleDiagnosticIds.BuildTemplateNotPrecompiled,
                "not fully precompiled: " + string.Join("; ", parts));
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
            using (var destination = File.Create(request.ArtifactOut))
                CompiledFormWriter.Write(outcome.Artifact, destination);
            SourceEmitter.Write(request.SourceOut, generatedNamespace, outcome.Emitted,
                outcome.Artifact.Header != null ? outcome.Artifact.Header.EngineVersion : string.Empty);
        }
    }
}
