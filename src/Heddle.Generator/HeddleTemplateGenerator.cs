using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    /// The Heddle build-time pre-compilation generator. Discovers <c>.heddle</c> <c>AdditionalFiles</c>,
    /// reads the compilation-wide options, parses each template through the shared front end, surfaces template
    /// errors at their <c>.heddle</c> span, emits a per-template <c>{SanitizedName}.g.cs</c> structural body through
    /// <see cref="TemplateEmitter"/> for every supported template, and emits the two-layer discovery metadata (the
    /// <c>[HeddleCompiledTemplates]</c> attribute + typed manifest). A template using a construct the emitter does
    /// not yet cover is left un-precompiled — no entry, the render takes the byte-identical dynamic path.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class HeddleTemplateGenerator : IIncrementalGenerator
    {
        private sealed class TemplateFile
        {
            public TemplateFile(AdditionalText text, string content, string keyMetadata, string nameMetadata,
                bool precompile, bool readable, string readError)
            {
                Text = text;
                Content = content;
                KeyMetadata = keyMetadata;
                NameMetadata = nameMetadata;
                Precompile = precompile;
                Readable = readable;
                ReadError = readError;
            }

            public AdditionalText Text { get; }
            public string Content { get; }

            /// <summary>The <c>Key</c> item metadata: the item's explicit registration key.</summary>
            public string KeyMetadata { get; }

            /// <summary>The <c>Name</c> item metadata: an <b>additional</b> spelling the
            /// <c>@&lt;&lt;</c> import map answers to — <b>not</b> an override of <see cref="KeyMetadata"/>. The
            /// template keeps its path-derived (or explicit <c>Key</c>) registration key and *gains* this name, so
            /// both spellings resolve and nothing that resolved before stops resolving. It normalizes through the same
            /// <c>TemplateKey</c> rule as a key, because it occupies the same import-path namespace.
            /// <para>An earlier implementation made this an override (one setting, two spellings) and that was
            /// wrong: it silently broke every existing <c>@&lt;&lt;</c> that named the file. Importing a named template
            /// by its key draws the HED7028 advisory instead.</para></summary>
            public string NameMetadata { get; }

            /// <summary>The <c>Precompile</c> item metadata. <c>false</c> is the per-item
            /// opt-out: the file still serves <c>@&lt;&lt;</c> imports, but emits no entry point and no manifest
            /// entry. Absent, empty, or unparsable metadata means <c>true</c> — today's behavior.</summary>
            public bool Precompile { get; }

            /// <summary>False when the <c>AdditionalFiles</c> source could not be read/decoded (HED7001).</summary>
            public bool Readable { get; }
            public string ReadError { get; }
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var templates = context.AdditionalTextsProvider
                .Where(t => TemplateKey.HasTemplateExtension(t.Path))
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select((pair, ct) =>
                {
                    var options = pair.Right.GetOptions(pair.Left);
                    options.TryGetValue("build_metadata.AdditionalFiles.Key", out var key);
                    options.TryGetValue("build_metadata.AdditionalFiles.Name", out var name);
                    options.TryGetValue("build_metadata.AdditionalFiles.Precompile", out var precompileMetadata);
                    var precompile = !(bool.TryParse(precompileMetadata, out var optIn) && !optIn);
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

                    return new TemplateFile(pair.Left, content, key, name, precompile, readable, readError);
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
            // is available at build. Unlike System.Reflection over the not-yet-built assembly, ISymbol access is
            // possible here. The symbol-diagnostics stage (HED7007/7008) is the deferred milestone-2 add.
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
            var engineVersion = ResolveEngineVersion(spc, compilation);
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

            // Import map for the shared front end's ImportReader (@<< served from AdditionalFiles).
            //
            // Two passes, and the order is load-bearing. Pass 1 registers every template's *key* — the
            // spelling that has always resolved. Pass 2 adds the optional `Name` alias on top. Because keys go first,
            // a registered name can never displace a real key spelling, which is what makes `Name` additive rather
            // than an override: whatever resolved before still resolves, and the name resolves as well.
            var importMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                var key = DeriveKey(template, config.TemplateRoot);
                if (key != null && !importMap.ContainsKey(key))
                    importMap[key] = template.Content;
            }

            // The spellings a registered name is reachable by, and the advisory index. `nameByKeySpelling` maps a
            // named template's KEY onto its registered NAME: an @<< that resolves through such an entry has picked
            // the resolvable-but-non-preferred spelling, which is exactly HED7028's population.
            var aliasOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var nameByKeySpelling = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                var alias = DeriveName(template, out _);
                if (alias == null)
                    continue;

                var key = DeriveKey(template, config.TemplateRoot);

                // Name == key: the name asks for the spelling the template already answers to. Nothing is added and
                // nothing is advised — the "preferred" spelling and the path spelling are the same string.
                if (string.Equals(alias, key, StringComparison.Ordinal))
                    continue;

                // The spelling is taken by another template's key or another template's name. Registering it would be
                // the override this correction exists to remove, so the alias is dropped and loop 2 reports HED7004.
                if (importMap.ContainsKey(alias))
                    continue;

                importMap[alias] = template.Content;
                aliasOwners[alias] = template.Text.Path;
                if (key != null)
                    nameByKeySpelling[key] = alias;
            }

            var seenKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            var sanitizedOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var manifestEntries = new List<string>();
            var usedHintNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // [ExportFunctions] attributes discovery from the compilation's own + referenced assemblies, computed once.
            var exports = Heddle.Generator.Binding.FunctionExportResolver.Build(compilation);

            // HED7021: an [ExportFunctions] container the runtime's RegisterFrom would throw on.
            // Once per compilation, at Location.None — the attribute lives in the consuming assembly, not in a
            // template — and at Error severity, because the runtime errors and the build must not mask a host
            // configuration mistake until first render.
            foreach (var container in exports.IneligibleContainers)
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.IneligibleExportContainer,
                    Location.None, container.Reason));

            // Report HED7016 once per compilation for any branch Continuation/Terminal that
            // lacks [ScopeChannel]. Empty for engine-only compilations.
            foreach (var driftType in ExtensionBinder.Build(compilation).DriftTypes)
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.BranchRoleMissingScopeChannel,
                    Location.None, driftType));

            foreach (var template in templates)
            {
                // HED7001: an AdditionalFiles .heddle source the compiler could not read/decode.
                if (!template.Readable)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.UnreadableFile,
                        Location.None, template.Text.Path, template.ReadError ?? "unknown error"));
                    continue;
                }

                // Key and name derivation, and their diagnostics, run before the Precompile gate.
                // This ensures errors on opt-out items are reported even though they don't contribute
                // entries or manifest rows.
                var key = DeriveKey(template, config.TemplateRoot, out var outOfRoot, out var keyFault);
                if (key == null)
                {
                    // HED7004: explicit `Key` metadata the generator cannot use — a value the normalizer rejects. A
                    // path-derived key that fails normalization is left un-precompiled silently (the user set no key,
                    // so there is nothing to report back at them).
                    if (keyFault != null)
                        spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.InvalidKeyMetadata,
                            Location.None, template.Text.Path, keyFault));
                    continue;
                }

                // HED7004, the `Name` arm. A name that cannot be registered is reported here but does NOT
                // un-precompile the template: `Name` is additive, so a broken addition costs the addition and nothing
                // else — the key still registers and every existing import still resolves. Two faults reach this:
                // a value the normalizer refuses, and a spelling another template's key or name already owns.
                var registeredName = DeriveName(template, out var nameFault);
                var nameRegistered = false;
                if (nameFault != null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.InvalidKeyMetadata,
                        Location.None, template.Text.Path, nameFault));
                }
                else if (registeredName == null ||
                    string.Equals(registeredName, key, StringComparison.Ordinal))
                {
                }
                else if (aliasOwners.TryGetValue(registeredName, out var aliasOwner) &&
                    string.Equals(aliasOwner, template.Text.Path, StringComparison.Ordinal))
                {
                    nameRegistered = true;
                }
                else
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.InvalidKeyMetadata,
                        Location.None, template.Text.Path,
                        "Name=\"" + template.NameMetadata + "\" registers the import spelling '" + registeredName +
                        "', which another template already answers to. Registered names share the import-path " +
                        "namespace with template keys, so the name must be free."));
                }

                // Only a name that actually registered travels to the manifest, so the runtime name index and
                // the build-time import map hold the same set of spellings for the same templates.
                var registeredNameForManifest = nameRegistered ? registeredName : null;

                // Precompile="false" is the per-item opt-out. The file is already in the import map built
                // above, so every @<< that references it still resolves (no HED7011 on importers) — it simply
                // contributes no entry point and no manifest entry, which is what `Remove` could never express.
                //
                // The file still participates in the import graph and is parsed to advise on its
                // own imports. This allows HED7028 to be raised even for opted-out files. A missing import inside
                // an opted-out file is not an error (see ParseAndReport's `advisoryOnly` parameter).
                if (!template.Precompile)
                {
                    ParseAndReport(spc, template, importMap, nameByKeySpelling, out _, out _, advisoryOnly: true);
                    continue;
                }

                // HED7018: the template is not under HeddleTemplateRoot and carries no explicit key metadata, so
                // its directory silently vanished from the key. Behavior is unchanged — the flattened key still
                // registers — but the condition is now visible, and it explains any HED7002 that follows.
                if (outOfRoot)
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.TemplateOutsideRoot, Location.None,
                        template.Text.Path,
                        string.IsNullOrEmpty(config.TemplateRoot) ? "<unset>" : config.TemplateRoot, key));

                // HED7002 / HED7003: both checks are over registration KEYS only, which is the
                // population they had before `Name` was wired. A registered name is not a key — it registers no
                // manifest row, sanitizes to no entry class and is never looked up by the runtime registry — so it
                // cannot duplicate one, and case-shadowing among names is not a registry hazard. The alias namespace
                // has its own collision rule, above, reported at HED7004 against the name that could not be taken.
                //
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

                var parsed = ParseAndReport(spc, template, importMap, nameByKeySpelling,
                    out var cleanDocument, out var hadErrors);
                if (parsed == null || hadErrors)
                    continue;

                try
                {
                    // The `#line` file is the template's own path, not its registration key: for a path-derived key
                    // the two strings are identical (so nothing existing moves), but an explicit Key names a
                    // registration and not a file, and emitting it here pointed every mapped span at a path that does
                    // not exist. `rootRelative` carries the relativity marking through to the generated file's header.
                    var lineFile = LineDirectiveFile(template, config.TemplateRoot, out var lineFileIsRootRelative);
                    var emitter = new TemplateEmitter(key, sanitized, ns, cleanDocument, template.Content, parsed, config, compilation, exports, template.Text.Path, lineFile, lineFileIsRootRelative,
                        // The name goes onto the manifest row only if it actually registered. A name that lost
                        // its spelling (reported at HED7004 above) must not reach the runtime index, or the two tiers
                        // would disagree about which template answers to it.
                        registeredName: registeredNameForManifest);
                    var result = emitter.Emit(ContentHash.HashText(template.Content));

                    // Emitter-produced Roslyn diagnostics (HED7005 surrogate, HED7006/HED7015 extension binding),
                    // reported in every result branch at their .heddle span.
                    if (result.Diagnostics != null)
                    {
                        var text = template.Text.GetText();
                        foreach (var d in result.Diagnostics)
                            spc.ReportDiagnostic(Diagnostic.Create(d.Descriptor,
                                ToLocation(template.Text, text, d.Position), d.Args));
                    }

                    // Tentative base-not-found errors on region-fill candidates are filtered out of
                    // the parse-channel drain above, because whether they should stand is only known once the
                    // emitter has matched the call sites. Now that the emit has run, forward every candidate error
                    // it did NOT retract — the exact set the dynamic compile leaves in its error list for the same
                    // template. Gated on a completed body build: when the emitter degraded for an unrelated reason
                    // it never visited the call sites, so it has no verdict to report and the dynamic tier stays
                    // the one that raises.
                    if (result.RetractedCandidateErrors != null)
                    {
                        var candidateText = template.Text.GetText();
                        foreach (var candidate in parsed.RegionFillCandidates)
                        {
                            if (candidate.Error == null || result.RetractedCandidateErrors.Contains(candidate.Error))
                                continue;
                            spc.ReportDiagnostic(Diagnostic.Create(
                                GeneratorDiagnostics.Forwarded(candidate.Error.DiagnosticId, isWarning: false),
                                ToLocation(template.Text, candidateText, candidate.Error.Position),
                                candidate.Error.Error));
                        }
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
                        // A delegate-only function makes this template un-precompilable. Report
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
                }
                catch (Exception ex)
                {
                    // Exceptions from the emitter are defects that must surface.
                    // Intentional refusals already leave through result.IsMarker or result.UnsupportedReason,
                    // so an exception indicates a bug.
                    //
                    // Report per template rather than rethrow: letting an exception escape downgrades the
                    // failure to the compiler's CS8785 warning and discards the generator's entire contribution.
                    // An error diagnostic reds the build while the pass continues, so one defective template
                    // neither hides itself nor un-precompiles the rest.
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.EmitterFault, Location.None,
                        template.Text.Path, ex.GetType().Name, ex.Message));
                }
            }

            EmitManifest(spc, ns, engineVersion, manifestEntries);
        }

        /// <summary>Parses the template through the shared front end and reports what the build tier owes the author.
        /// <para><paramref name="advisoryOnly"/> is the <c>Precompile="false"</c> mode: the parse runs so the
        /// import reader can raise HED7028 for this file's own imports, and <b>nothing else is reported</b>. The
        /// missing-import error and the drained parse channels stay silent for an opted-out item, deliberately and
        /// narrowly: the purpose is validation of the item's metadata and advice on its imports. Turning every opted-out
        /// file's template errors into build errors would be a different and much larger change — it would
        /// red previously-green builds over templates that, by the author's explicit instruction, this build does not
        /// compile. Those faults are unchanged, not forgiven: the moment a precompiled template imports the file, the
        /// importer's own parse pulls the same content through the same channels and raises them.</para></summary>
        private static ParseContext ParseAndReport(SourceProductionContext spc, TemplateFile template,
            Dictionary<string, string> importMap, Dictionary<string, string> nameByKeySpelling,
            out string cleanDocument, out bool hadErrors, bool advisoryOnly = false)
        {
            cleanDocument = template.Content;
            hadErrors = false;

            // HED7011: an @<< import path not among the compilation's .heddle AdditionalFiles. The reader records
            // each miss (raw path as written); after the parse we report one diagnostic per distinct missing import
            // at its @<<{{…}} block in this template.
            var missingImports = new List<string>();

            // HED7028: the import resolved through the key spelling of a template that also has a registered
            // name. Both spellings work — this is an advisory that the name-first spelling is the preferred one for a
            // named template, not a fault. Keyed by the raw path as written so the report lands on the block the
            // author typed; the value is the name to prefer.
            var nonPreferredImports = new List<KeyValuePair<string, string>>();
            var settings = new ParserSettings
            {
                RootPath = string.Empty,
                ProvideLanguageFeatures = false,
                ImportReader = importPath =>
                {
                    if (TemplateKey.TryNormalize(importPath, out var k) && importMap.TryGetValue(k, out var content))
                    {
                        if (nameByKeySpelling.TryGetValue(k, out var preferred) &&
                            !nonPreferredImports.Exists(p => string.Equals(p.Key, importPath, StringComparison.Ordinal)))
                        {
                            nonPreferredImports.Add(new KeyValuePair<string, string>(importPath, preferred));
                        }

                        return content;
                    }

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

            if (!advisoryOnly)
            {
                foreach (var missing in missingImports)
                {
                    hadErrors = true;
                    var position = FindImportBlock(template.Content, missing);
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.ImportNotIncluded,
                        ToLocation(template.Text, sourceText, position), missing));
                }
            }

            // HED7028: guidance only, and deliberately not gated on `hadErrors` — the import resolved, so the advice
            // is valid regardless of what else the template got wrong.
            foreach (var advised in nonPreferredImports)
            {
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.NamedTemplateImportedByKey,
                    ToLocation(template.Text, sourceText, FindImportBlock(template.Content, advised.Key)),
                    advised.Key, advised.Value));
            }

            if (advisoryOnly)
                return null;

            // A base-not-found error captured on a region-fill candidate is tentative — a matched public fill
            // retracts it on the dynamic tier, and the emitter decides matched vs unmatched during Emit. Never report
            // it as a build error here; an unmatched candidate un-precompiles the template silently and the DYNAMIC
            // tier raises the error.
            var candidateErrors = new HashSet<Heddle.Data.HeddleCompileError>();
            foreach (var candidate in parseContext.RegionFillCandidates)
                candidateErrors.Add(candidate.Error);

            // One drain rule, shared with the language server and the runtime's CompileResult. The
            // generator's own policy is the retract pre-filter above and the Roslyn location mapping below;
            // everything else — which channels, severity by subtype, id and Fix passthrough, reference dedupe —
            // is stated once in HeddleDiagnosticProjection. The generator runs no compile-channel stage yet, so
            // it drains the parse channels; when it gains one, channel-completeness comes for free.
            foreach (var entry in HeddleDiagnosticProjection.Drain(parseContext, e => !candidateErrors.Contains(e)))
            {
                if (!entry.IsWarning)
                    hadErrors = true;
                var location = ToLocation(template.Text, sourceText,
                    new BlockPosition(entry.Offset, entry.Length));
                spc.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.Forwarded(entry.Id, entry.IsWarning), location,
                    GeneratorDiagnostics.ForwardedMessage(entry.Message, entry.Fix)));
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
            sb.AppendLine($"    schemaVersion: {PrecompiledSchema.CurrentSchemaVersion},");
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

        private static string DeriveKey(TemplateFile template, string templateRoot) =>
            DeriveKey(template, templateRoot, out _, out _);

        /// <summary>The file name the emitted <c>#line</c> directives carry. Always the template's <b>file</b>, never
        /// its registration key: for a path-derived key the two strings are identical, but an explicit <c>Key</c> names
        /// a registration and not a file, and emitting it pointed every mapped span at a path that does not exist.
        /// <para>Q8.27, the absolute-vs-relative form. <b>Outside the root</b> there is no anchor to be relative to, so
        /// the template's own <see cref="AdditionalText.Path"/> is emitted verbatim — absolute in a real build, which is
        /// what a <c>#line</c> is for. This replaces the old bare-filename fallback, which named no openable file and
        /// collided across directories. <b>Inside the root</b> the form stays root-relative and is <em>labelled</em> as
        /// such by <see cref="Emit.TemplateEmitter"/>'s header line: an absolute path here would be correct for one
        /// machine and would put that machine's layout into every checked-in generated-source golden and Verify
        /// snapshot, making the pinned artifacts unreproducible. Relative-and-marked is the honest trade; the marker is
        /// what lets a reader tell which of the two forms a given <c>#line</c> is in.</para></summary>
        private static string LineDirectiveFile(TemplateFile template, string templateRoot, out bool rootRelative)
        {
            if (TemplateKey.TryMakeRelative(template.Text.Path, templateRoot, out var rooted))
            {
                rootRelative = true;
                return rooted;
            }

            rootRelative = false;
            return template.Text.Path;
        }

        /// <summary>The rejection text for an explicit key metadata value the shared normalizer refuses. Stated once
        /// so the <c>Key</c> and <c>Name</c> arms cannot describe the same rule differently.</summary>
        private const string KeyShapeRule =
            "keys must be non-empty relative paths without '.' or '..' segments";

        /// <summary>The key↔path derivation, on the shared <see cref="TemplateKey"/> rules (phase 5 D2). Explicit
        /// <c>Key</c> metadata wins; otherwise the path is made relative to <c>HeddleTemplateRoot</c>. A template
        /// outside the root keeps the historical flattened-filename key — removing it would un-precompile projects
        /// that rely on flat lookups — but sets <paramref name="outOfRoot"/> so the caller can report HED7018 (D3);
        /// the silent directory-drop is the bug (05 F3).
        /// <para>Q8.25: <c>Name</c> is deliberately <b>not</b> consulted here. Q8.12's first implementation treated it
        /// as a second spelling of <c>Key</c>, which made a named template unreachable by its path; the correction is
        /// that a name is an <em>additional</em> import spelling (see <see cref="DeriveName"/>) and the key derivation
        /// is exactly what it was before <c>Name</c> was wired. Empty <c>Key</c> is absent, not malformed, because
        /// MSBuild materializes unset metadata as <c>""</c> on every item; a value the normalizer refuses returns
        /// <c>null</c> with <paramref name="fault"/> set and the caller reports HED7004.</para>
        /// </summary>
        private static string DeriveKey(TemplateFile template, string templateRoot, out bool outOfRoot,
            out string fault)
        {
            outOfRoot = false;
            fault = null;

            if (!string.IsNullOrEmpty(template.KeyMetadata))
            {
                if (!TemplateKey.TryNormalize(template.KeyMetadata, out var fromKey))
                {
                    fault = "Key=\"" + template.KeyMetadata + "\" is not a usable key — " + KeyShapeRule;
                    return null;
                }

                return fromKey;
            }

            if (TemplateKey.TryMakeRelative(template.Text.Path, templateRoot, out var rooted))
                return rooted;

            outOfRoot = true;
            return TemplateKey.TryNormalize(System.IO.Path.GetFileName(template.Text.Path), out var flat)
                ? flat
                : null;
        }

        /// <summary>The optional registered <b>name</b> (Q8.25): the additional spelling the <c>@&lt;&lt;</c> import
        /// map answers to, normalized by the same <see cref="TemplateKey"/> rule as a key because it lives in the same
        /// import-path namespace. <c>null</c> when the item declares no <c>Name</c> (empty is absent, as for
        /// <c>Key</c>) or when the value is unusable, in which case <paramref name="fault"/> is set and the caller
        /// reports HED7004 — against the name only. It never affects the registration key, the manifest row, the
        /// generated entry-class identifier, or the emitted <c>#line</c> file.</summary>
        private static string DeriveName(TemplateFile template, out string fault)
        {
            fault = null;
            if (string.IsNullOrEmpty(template.NameMetadata))
                return null;

            if (TemplateKey.TryNormalize(template.NameMetadata, out var name))
                return name;

            fault = "Name=\"" + template.NameMetadata + "\" is not a usable import name — " + KeyShapeRule;
            return null;
        }

        /// <summary>The manifest's <c>engineVersion</c> (phase 5 D6). Primary source: the referenced <c>Heddle</c>
        /// assembly's own identity. When it is not visible — an extern alias, an embedded or ILMerged engine — the
        /// generator falls back to <b>its own</b> assembly version and says so (HED7019): the generator versions in
        /// lockstep with the engine, so the fallback tracks reality, where the previous hardcoded <c>"2.0.0"</c>
        /// literal would have gone stale the release after it was written while the runtime's compatibility gate
        /// decided whole-assembly registration on it (05 F5).</summary>
        private static string ResolveEngineVersion(SourceProductionContext spc, Compilation compilation)
        {
            foreach (var reference in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (string.Equals(reference.Identity.Name, "Heddle", StringComparison.Ordinal))
                    return PrecompiledSchema.FormatEngineVersion(reference.Identity.Version);
            }

            var self = PrecompiledSchema.FormatEngineVersion(
                typeof(HeddleTemplateGenerator).Assembly.GetName().Version ?? new Version(0, 0, 0));
            spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.EngineVersionUnresolved, Location.None, self));
            return self;
        }
    }
}
