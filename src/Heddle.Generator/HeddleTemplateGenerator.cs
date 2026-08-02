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
    /// Discovers <c>.heddle</c> <c>AdditionalFiles</c>, parses each through the shared front end, emits
    /// precompiled <c>{SanitizedName}.g.cs</c> files and discovery metadata via <see cref="TemplateEmitter"/>.
    /// Unsupported constructs are left un-precompiled and take the dynamic path.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class HeddleTemplateGenerator : IIncrementalGenerator
    {
        /// <summary>Test-only fault injection: invoked with the template path just before the parse so the
        /// parse-fault error path can be exercised without a real defect. Never assigned by the generator — the
        /// field is <c>internal</c> and only the white-box test project sets it.</summary>
        internal static Action<string> ParseFaultInjector;

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

            /// <summary>An <b>additional</b> spelling for <c>@&lt;&lt;</c> imports — <b>not</b> an override
            /// of <see cref="KeyMetadata"/>. The template keeps its key and gains this name; both spellings resolve.
            /// Normalizes via the same <c>TemplateKey</c> rule as keys, in the same import-path namespace.</summary>
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

            // Uses ISymbol access (available at build time, unlike Reflection over not-yet-built assembly)
            // to type member paths and decide null-safety form (value- vs reference-typed hops).
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

            // Build import map in two passes, order is load-bearing: keys first so names cannot displace them,
            // making Name additive rather than an override.
            var importMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                var key = DeriveKey(template, config.TemplateRoot);
                if (key != null && !importMap.ContainsKey(key))
                    importMap[key] = template.Content;
            }

            // The spellings a `<HeddleTemplate>` item explicitly REGISTERED, as opposed to the ones its file path
            // happens to produce. These live in template-key space by construction and are the documented way to
            // import a template under a name that is not its path, so the key grammar may be applied to them; a
            // bare path spelling gets no such licence, because renaming it would bind a file the engine never reads.
            var registeredSpellings = new HashSet<string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                if (string.IsNullOrEmpty(template.KeyMetadata))
                    continue;
                var explicitKey = DeriveKey(template, config.TemplateRoot);
                // Only a key that actually RENAMES the template is a registration. A `Key` restating what the file
                // path already produces registers nothing, and treating it as licence would re-admit the key grammar
                // for every template that carries the metadata at all.
                if (explicitKey != null &&
                    !string.Equals(explicitKey, DerivePathKey(template, config.TemplateRoot, out _),
                        StringComparison.Ordinal))
                    registeredSpellings.Add(explicitKey);
            }

            // Maps template key to its registered name for HED7028 advisory (non-preferred import spellings).
            var aliasOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var nameByKeySpelling = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                var alias = DeriveName(template, out _);
                if (alias == null)
                    continue;

                var key = DeriveKey(template, config.TemplateRoot);

                // Name == key: nothing to add (template already answers to this spelling).
                if (string.Equals(alias, key, StringComparison.Ordinal))
                    continue;

                // Spelling is taken; alias is dropped to prevent override, HED7004 is reported.
                if (importMap.ContainsKey(alias))
                    continue;

                importMap[alias] = template.Content;
                aliasOwners[alias] = template.Text.Path;
                registeredSpellings.Add(alias);
                if (key != null)
                    nameByKeySpelling[key] = alias;
            }

            var seenKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            var sanitizedOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            // Every key some other template actually imported, and the standalone-pass errors waiting on that
            // answer. A fragment that is only well-formed inside an importer fails its own standalone pass, and in
            // production the engine never runs that pass — it only ever reaches the compiler already expanded into
            // the document that imports it. Reporting those errors would red a build over a state no run of the
            // application can reach, so they are held until the whole item set has been parsed and the import graph
            // is known. The template is still dropped from precompilation; only the diagnostic is withheld.
            var importedKeys = new HashSet<string>(StringComparer.Ordinal);
            var deferredStandaloneErrors = new List<KeyValuePair<string, Diagnostic>>();
            var manifestEntries = new List<string>();
            var usedHintNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var exports = Heddle.Generator.Binding.FunctionExportResolver.Build(compilation);

            // HED7021: an [ExportFunctions] container the runtime's RegisterFrom would throw on.
            // HED7021: reported at Location.None (attribute is in consuming assembly, not template)
            // with Error severity to fail the build on host configuration mistakes.
            foreach (var container in exports.IneligibleContainers)
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.IneligibleExportContainer,
                    Location.None, container.Reason));

            // HED7016: branch Continuation/Terminal lacking [ScopeChannel] (empty for engine-only compilations).
            foreach (var driftType in ExtensionBinder.Build(compilation).DriftTypes)
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.BranchRoleMissingScopeChannel,
                    Location.None, driftType));

            foreach (var template in templates)
            {
                // HED7001: unreadable file.
                if (!template.Readable)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.UnreadableFile,
                        Location.None, template.Text.Path, template.ReadError ?? "unknown error"));
                    continue;
                }

                // Key/name derivation runs before Precompile gate so errors on opted-out items are still reported.
                var key = DeriveKey(template, config.TemplateRoot, out var outOfRoot, out var keyFault);
                if (key == null)
                {
                    // HED7004: report only if Key was explicit (user set it); path-derived failures are silent.
                    if (keyFault != null)
                        spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.InvalidKeyMetadata,
                            Location.None, template.Text.Path, keyFault));
                    continue;
                }

                // HED7004: broken Name is reported but does not un-precompile (Name is additive; key still registers).
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

                // Only registered names travel to manifest (keeps runtime index in sync with build-time import map).
                var registeredNameForManifest = nameRegistered ? registeredName : null;

                // Precompile="false": file is already in import map (imports resolve), contributes no entry/manifest.
                // Still parsed advisoryOnly for HED7028; missing imports inside opted-out files don't error.
                if (!template.Precompile)
                {
                    ParseAndReport(spc, template, importMap, nameByKeySpelling, out _, out _, advisoryOnly: true,
                        importedKeys: importedKeys, registeredNames: registeredSpellings, selfKey: key);
                    continue;
                }

                // HED7018: template outside root; directory silently vanished from flattened key.
                if (outOfRoot)
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.TemplateOutsideRoot, Location.None,
                        template.Text.Path,
                        string.IsNullOrEmpty(config.TemplateRoot) ? "<unset>" : config.TemplateRoot, key));

                // HED7002 / HED7003: check only keys, not names (names have separate HED7004 collision rule).
                // HED7002: two templates normalize to the same key.
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

                var deferred = new List<Diagnostic>();
                var parsed = ParseAndReport(spc, template, importMap, nameByKeySpelling,
                    out var cleanDocument, out var hadErrors, advisoryOnly: false,
                    importedKeys: importedKeys, deferredErrors: deferred, registeredNames: registeredSpellings,
                    selfKey: key);
                foreach (var pending in deferred)
                    deferredStandaloneErrors.Add(new KeyValuePair<string, Diagnostic>(key, pending));
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

                    // Emitter diagnostics (HED7006, HED7015). An error here is as unreachable for an import-only
                    // fragment as a parse error is — a bodied call to a definition the importer supplies is one of
                    // the shapes that only fails standalone — so errors join the same held channel.
                    if (result.Diagnostics != null)
                    {
                        var text = template.Text.GetText();
                        foreach (var d in result.Diagnostics)
                        {
                            var emitted = Diagnostic.Create(d.Descriptor,
                                ToLocation(template.Text, text, d.Position), d.Args);
                            if (emitted.Severity == DiagnosticSeverity.Error)
                                deferredStandaloneErrors.Add(new KeyValuePair<string, Diagnostic>(key, emitted));
                            else
                                spc.ReportDiagnostic(emitted);
                        }
                    }

                    // Forward candidate errors the emitter did not retract (gated on completed body build).
                    // When emitter degraded for unrelated reason, it never visited call sites and has no verdict.
                    if (result.RetractedCandidateErrors != null)
                    {
                        var candidateText = template.Text.GetText();
                        foreach (var candidate in parsed.RegionFillCandidates)
                        {
                            if (candidate.Error == null || result.RetractedCandidateErrors.Contains(candidate.Error))
                                continue;
                            deferredStandaloneErrors.Add(new KeyValuePair<string, Diagnostic>(key,
                                Diagnostic.Create(
                                    GeneratorDiagnostics.Forwarded(candidate.Error.DiagnosticId, isWarning: false),
                                    ToLocation(template.Text, candidateText, candidate.Error.Position),
                                    candidate.Error.Error)));
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
                        // HED7014: delegate-only functions are un-precompilable; fallback marker routes to dynamic path.
                        var sourceText = template.Text.GetText();
                        foreach (var fn in result.UnresolvableFunctions)
                        {
                            var location = ToLocation(template.Text, sourceText, fn.Position);
                            spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.UnresolvableFunction,
                                location, fn.Name));
                        }

                        manifestEntries.Add(result.ManifestEntry);
                    }
                    else
                    {
                        // The decline that used to be silent. Emitted=false with no fallback marker means the
                        // emitter had a reason and nothing asked for it: no source, no manifest row, no
                        // diagnostic. A consumer could believe a template was precompiled while it rendered
                        // dynamically on every request, and the only way to find out was to notice the
                        // missing manifest entry.
                        //
                        // A warning, not an error: falling back is a supported mode and existing builds must
                        // keep working. A project for which precompilation is a requirement rather than an
                        // optimisation makes it fatal with the mechanism MSBuild already has —
                        // <WarningsAsErrors>HED7031</WarningsAsErrors> — rather than a second Heddle-specific
                        // switch that would have to be threaded through the whole build-option surface.
                        spc.ReportDiagnostic(Diagnostic.Create(
                            GeneratorDiagnostics.TemplateNotPrecompiled,
                            ToLocation(template.Text, SafeText(template.Text), default),
                            result.UnsupportedReason ?? "no reason recorded by the emitter"));
                    }
                }
                catch (Exception ex)
                {
                    // Report per template (not rethrow): rethrow downgrades to warning and discards contribution;
                    // error diagnostic reds build and continues, isolating defective template.
                    // Positioned at the template's start where the text can still be read: reporting at Location.None
                    // put the path in the message only, leaving nothing for the IDE's error list to navigate to.
                    spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.EmitterFault,
                        ToLocation(template.Text, SafeText(template.Text), default),
                        template.Text.Path, ex.GetType().Name, ex.Message));
                }
            }

            // The import graph is complete now, so a held error can be judged: report it only where nothing in the
            // compilation imports the template it came from, which is the only case a consumer could ever hit.
            foreach (var pending in deferredStandaloneErrors)
                if (!importedKeys.Contains(pending.Key))
                    spc.ReportDiagnostic(pending.Value);

            EmitManifest(spc, ns, engineVersion, manifestEntries);
        }

        /// <summary>Parses the template and reports template diagnostics.
        /// When <paramref name="advisoryOnly"/> is true (<c>Precompile="false"</c> mode),
        /// the parse runs to advise on imports (HED7028) but missing-import errors and other parse channels stay silent.
        /// </summary>
        /// <param name="importedKeys">Collects every key this template's imports resolved to, so the caller can
        /// tell an import-only fragment from a template nobody imports.</param>
        /// <param name="deferredErrors">When supplied, forwarded <b>errors</b> are buffered here instead of being
        /// reported, so the caller can withhold them for a template something else imports. Warnings and the
        /// missing-import channel are unaffected — those are reachable whatever the import graph says.</param>
        private static ParseContext ParseAndReport(SourceProductionContext spc, TemplateFile template,
            Dictionary<string, string> importMap, Dictionary<string, string> nameByKeySpelling,
            out string cleanDocument, out bool hadErrors, bool advisoryOnly = false,
            HashSet<string> importedKeys = null, List<Diagnostic> deferredErrors = null,
            ICollection<string> registeredNames = null, string selfKey = null)
        {
            cleanDocument = template.Content;
            hadErrors = false;

            // HED7011: missing import paths (one diagnostic per distinct miss).
            var missingImports = new List<string>();

            // HED7028: imports resolved by key spelling of named templates (prefer name spelling).
            var nonPreferredImports = new List<KeyValuePair<string, string>>();
            // One normaliser, shared: the cycle guard has to call an import the same document the reader does.
            // Resolving imports by template key while identifying them by file path gave one document as many
            // identities as it had spellings — `views/a`, `~/views/a.heddle`, `/views/a.heddle` are all the same
            // template here — and the guard then walked their permutations before noticing the repeat.
            // The spelling is reduced the way Path.GetFullPath reduces it before the key is derived, because that is
            // what the engine's identity does with it, while a template key may contain neither a '..' nor a '.' at
            // all. Without this step `x/../lib.heddle` and `sub/./lib.heddle` — files the engine reads and renders —
            // were imports nobody had included, and the build failed over a working template. The raw spelling is
            // still what a miss is reported with, so the message names what the author wrote.
            Func<string, string> identity = importPath =>
            {
                var canonical = CanonicalizeImportPath(importPath);
                if (!SpellingSurvivesKeyDerivation(canonical) ||
                    !TemplateKey.TryNormalize(canonical, out var key))
                    return importPath;

                // The key grammar may rename the spelling only when what it arrives at is a name a
                // `<HeddleTemplate Name="...">` explicitly registered. A registered name lives in key space by
                // construction, and importing by one is the documented idiom; a path spelling does not, and
                // renaming it would bind a file the engine never reads.
                return KeyNamesTheSameFile(canonical, key) ||
                       (registeredNames != null && registeredNames.Contains(key))
                    ? key
                    : importPath;
            };

            var settings = new ParserSettings
            {
                RootPath = string.Empty,
                ProvideLanguageFeatures = false,
                ImportIdentifier = identity,
                ImportReader = importPath =>
                {
                    var k = identity(importPath);
                    if (importMap.TryGetValue(k, out var content))
                    {
                        // A template importing ITSELF is not somebody else's library, and its errors — a
                        // composition cycle above all — are reached by every build and every render. Only an
                        // import from another document makes a file one that never compiles standalone in
                        // production.
                        if (importedKeys != null && !string.Equals(k, selfKey, StringComparison.Ordinal))
                            importedKeys.Add(k);
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
                ParseFaultInjector?.Invoke(template.Text.Path);
                parseContext = DocumentParser.Parse(template.Content, settings, out cleanDocument);
            }
            catch (Exception ex)
            {
                // Positioned at the template's start, like the emitter's last-resort handler: Location.None left
                // the IDE's error list with nothing to navigate to.
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.ForwardedError,
                    ToLocation(template.Text, SafeText(template.Text), default),
                    "Internal parse error: " + ex.Message));
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

            // HED7028: advisory regardless of other errors (import resolved, advice is valid).
            foreach (var advised in nonPreferredImports)
            {
                spc.ReportDiagnostic(Diagnostic.Create(GeneratorDiagnostics.NamedTemplateImportedByKey,
                    ToLocation(template.Text, sourceText, FindImportBlock(template.Content, advised.Key)),
                    advised.Key, advised.Value));
            }

            if (advisoryOnly)
                return null;

            // Tentative base-not-found errors on region-fill candidates: don't report here, dynamic tier will raise.
            var candidateErrors = new HashSet<Heddle.Data.HeddleCompileError>();
            foreach (var candidate in parseContext.RegionFillCandidates)
                candidateErrors.Add(candidate.Error);

            // Errors an importer could have satisfied — a base definition this file does not declare — are held
            // rather than drained when the caller asked for that. They are the errors that exist only because the
            // build tier compiles an import-only fragment on its own, and the caller reports them only if nothing
            // in the compilation imports it. Everything else, a syntax error above all, fails the same way in
            // either setting and is drained as before.
            var heldErrors = deferredErrors == null
                ? null
                : new HashSet<Heddle.Data.HeddleCompileError>(parseContext.ImporterSatisfiableErrors);
            if (heldErrors != null)
            {
                foreach (var held in heldErrors)
                {
                    if (candidateErrors.Contains(held))
                        continue;
                    deferredErrors.Add(Diagnostic.Create(
                        GeneratorDiagnostics.Forwarded(held.DiagnosticId, isWarning: false),
                        ToLocation(template.Text, sourceText, held.Position), held.Error));
                }
            }

            // Drain policy is in HeddleDiagnosticProjection; generator pre-filter above, location mapping below.
            foreach (var entry in HeddleDiagnosticProjection.Drain(parseContext,
                         e => !candidateErrors.Contains(e) && (heldErrors == null || !heldErrors.Contains(e))))
            {
                if (!entry.IsWarning)
                    hadErrors = true;
                var location = ToLocation(template.Text, sourceText,
                    new BlockPosition(entry.Offset, entry.Length));
                spc.ReportDiagnostic(Diagnostic.Create(
                    GeneratorDiagnostics.Forwarded(entry.Id, entry.IsWarning), location,
                    GeneratorDiagnostics.ForwardedMessage(entry.Message, entry.Fix)));
            }

            // A held error still drops the template from precompilation — only the diagnostic waits.
            if (heldErrors != null && deferredErrors.Count > 0)
                hadErrors = true;

            return parseContext;
        }

        /// <summary>The characters that separate one path segment from the next, asked of the platform rather than
        /// assumed. On Windows both are separators; everywhere else a backslash is an ordinary file-name character,
        /// which is what <c>Path.GetFullPath</c> does with it and therefore what the engine does with it.</summary>
        private static readonly char[] PathSeparators =
            { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };

        /// <summary>
        /// Reduces an import spelling to the one form the engine reads it as. The engine resolves the same spelling
        /// through <c>Path.GetFullPath</c> over the resolver root, which drops a <c>.</c> segment and a run of
        /// repeated separators and cancels a <c>..</c> against the segment before it — so <c>sub/./../lib.heddle</c>,
        /// <c>sub/lib.heddle/.</c> and <c>lib.heddle</c> are one file on disk and have to be one key here.
        /// <para>Which characters separate segments is the platform's answer and is taken from <c>Path</c>: on
        /// Windows <c>x\..\lib.heddle</c> names <c>lib.heddle</c> and off Windows it names a file whose name contains
        /// backslashes, and the build tier has to agree with the run tier on whichever host it is running on.</para>
        /// <para>A trailing separator survives, because <c>GetFullPath</c> keeps it and the read then fails on a
        /// directory: <c>lib.heddle/</c> is refused by both tiers where <c>lib.heddle/.</c> is read by both. A
        /// <c>..</c> with nothing left to cancel against is kept for a related reason — it reaches outside the root,
        /// where the engine finds no file either, so key derivation refuses the spelling instead of quietly resolving
        /// it to something inside.</para>
        /// </summary>
        /// <summary>
        /// Whether the shared key normalizer would read a character of <paramref name="canonical"/> as a segment
        /// separator that this platform does not.
        /// <para><c>TemplateKey</c> unifies <c>\</c> to <c>/</c> on every host, and that is its contract rather than
        /// an oversight: a key is <c>/</c>-joined and a host may spell a registry lookup <c>Views\Home\Index</c>. An
        /// import spelling is not a key — it is resolved against disk, and off Windows a backslash is an ordinary
        /// file-name character that <c>Path.GetFullPath</c>, which is what the engine resolves the same spelling
        /// through, keeps. Handed over regardless, <c>sub\lib.heddle</c> became the key <c>sub/lib.heddle</c> and
        /// bound a file the engine never reads, with no diagnostic on either side. Refusing to derive a key leaves
        /// the raw spelling as the identity, which names nothing in the import map — the verdict the engine reaches
        /// by finding no file.</para>
        /// </summary>
        private static bool SpellingSurvivesKeyDerivation(string canonical) =>
            string.IsNullOrEmpty(canonical) ||
            canonical.IndexOf('\\') < 0 ||
            System.Array.IndexOf(PathSeparators, '\\') >= 0;

        /// <summary>
        /// Whether the derived key still names the file the import spelling names.
        /// <para>An import is not a template key. The documented contract for <c>@&lt;&lt;</c> is that the spelling is
        /// taken verbatim and resolved with <c>Path.Combine(RootPath, spelling)</c>, "an absolute path wins" — which
        /// is exactly what the engine does. The key grammar is a different grammar for a different job: it strips a
        /// leading <c>~/</c>, strips a leading <c>/</c>, and appends <c>.heddle</c> to an extension-less final
        /// segment. Each of those three renames the file. <c>/lib.heddle</c> is an <b>absolute</b> path to
        /// <c>Path.Combine</c> and the root-relative <c>lib.heddle</c> to the key grammar; they are not the same
        /// file, and binding the second while the engine reads the first is a silently different answer rather than
        /// a convenience.</para>
        /// <para>The canonical spelling has already had <c>.</c>, <c>..</c> and repeated separators reduced out of
        /// it the way <c>Path.GetFullPath</c> reduces them, so any <i>remaining</i> difference between it and the key
        /// is one of the three renames. Comparing the two is therefore the whole test, and it needs no second copy
        /// of the key grammar to stay in step with the first.</para>
        /// </summary>
        private static bool KeyNamesTheSameFile(string canonical, string key) =>
            string.Equals(canonical, key, StringComparison.Ordinal);

        private static string CanonicalizeImportPath(string importPath)
        {
            if (string.IsNullOrEmpty(importPath))
                return importPath;

            var segments = importPath.Split(PathSeparators);
            var kept = new List<string>(segments.Length);
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment == ".")
                    continue;
                if (segment.Length == 0 && i != 0 && i != segments.Length - 1)
                    continue;

                if (segment == ".." && kept.Count != 0 && kept[kept.Count - 1] != "..")
                {
                    kept.RemoveAt(kept.Count - 1);
                    continue;
                }

                kept.Add(segment);
            }

            return string.Join("/", kept);
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

        /// <summary>Reads a template's text without letting the read itself become a second fault — this runs on the
        /// path that is already handling one.</summary>
        private static SourceText SafeText(AdditionalText text)
        {
            try
            {
                return text.GetText();
            }
            catch
            {
                return null;
            }
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

        /// <summary>Derives an identifier from the key: the key's segments PascalCased, identifier-invalid characters mapped to <c>_</c>,
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

                var s = sb.ToString();
                parts.Add(s);
            }

            var result = string.Join("_", parts);
            return result.Length == 0 ? "_" : result;
        }

        private static string DeriveKey(TemplateFile template, string templateRoot) =>
            DeriveKey(template, templateRoot, out _, out _);

        /// <summary>The file path for emitted <c>#line</c> directives: the template's file path (not key).
        /// Returns absolute path when outside root; root-relative (marked by <see cref="Emit.TemplateEmitter"/>)
        /// when inside root.
        /// </summary>
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

        /// <summary>Derives template key: explicit Key metadata, or path relative to HeddleTemplateRoot,
        /// or flattened filename for out-of-root templates. Sets <paramref name="outOfRoot"/> and <paramref name="fault"/>
        /// appropriately; returns null if normalizer rejects the value.
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

            return DerivePathKey(template, templateRoot, out outOfRoot);
        }

        /// <summary>The key the template's <b>file path</b> produces, ignoring any <c>Key</c> metadata. Stated once
        /// so "did the metadata actually rename this template" has a single answer.</summary>
        private static string DerivePathKey(TemplateFile template, string templateRoot, out bool outOfRoot)
        {
            outOfRoot = false;
            if (TemplateKey.TryMakeRelative(template.Text.Path, templateRoot, out var rooted))
                return rooted;

            outOfRoot = true;
            return TemplateKey.TryNormalize(FileNameOf(template.Text.Path), out var flat)
                ? flat
                : null;
        }

        /// <summary>The last path segment, spelled by hand: .NET Framework's <c>Path.GetFileName</c> VALIDATES
        /// path characters and throws on <c>"</c> and control characters — ordinary file-name characters on Linux
        /// and macOS that .NET Core's implementation accepts, and that
        /// <c>ATemplatePathWithNoLineDirectiveSpellingLosesTheMappingNotTheBuild</c> feeds through here. The
        /// generator has no file system, so the HOST's path-character rules must not decide whether a template
        /// gets a key.</summary>
        private static string FileNameOf(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            var separator = path.LastIndexOfAny(new[] { '/', '\\' });
            return separator < 0 ? path : path.Substring(separator + 1);
        }

        /// <summary>Derives optional registered name for additional import spelling (same TemplateKey normalization as keys).
        /// Returns null if no Name or normalizer rejects it; sets <paramref name="fault"/> for HED7004 reporting.
        /// </summary>
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

        /// <summary>Returns Heddle assembly version for manifest, or own version if Heddle is not visible (HED7019).</summary>
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
