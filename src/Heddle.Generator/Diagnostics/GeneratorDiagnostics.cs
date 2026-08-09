using System;
using System.Collections.Concurrent;
using Heddle.Data;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Diagnostics
{
    /// <summary>Roslyn <see cref="DiagnosticDescriptor"/>s (HED7xxx block): projections of <see cref="HeddleDiagnosticCatalog"/>,
    /// where the shared table retains id/title/severity (keeping <c>Microsoft.CodeAnalysis</c> dependencies out),
    /// and this class contributes only the Roslyn shape. Forwarded front-end diagnostics keep their own IDs;
    /// ID-less entries are wrapped as <c>HED7012</c>/<c>HED7013</c>.</summary>
    internal static class GeneratorDiagnostics
    {
        private const string Category = "Heddle.Precompile";

        private static readonly ConcurrentDictionary<string, DiagnosticDescriptor> ForwardedDescriptors =
            new ConcurrentDictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal);

        /// <summary>Builds the Roslyn descriptor for a catalogued id. The severity mapping is the one Roslyn
        /// touchpoint the shared table cannot own — <see cref="HeddleDiagnosticSeverity"/> exists precisely so the
        /// table stays free of <c>Microsoft.CodeAnalysis</c>.</summary>
        private static DiagnosticDescriptor FromCatalog(string id)
        {
            if (!HeddleDiagnosticCatalog.TryGet(id, out var row))
                throw new InvalidOperationException("No HeddleDiagnosticCatalog row for '" + id + "'.");

            return new DiagnosticDescriptor(row.Id, row.Title, row.MessageFormat, Category, Severity(row),
                isEnabledByDefault: true);
        }

        private static DiagnosticSeverity Severity(HeddleDiagnosticInfo row) =>
            row.DefaultSeverity == HeddleDiagnosticSeverity.Warning
                ? DiagnosticSeverity.Warning
                : DiagnosticSeverity.Error;

        /// <summary>A forwarded front-end diagnostic with the front-end's HEDxxxx id and passthrough <c>"{0}"</c> format.
        /// Entries with no id fall back to <see cref="ForwardedError"/>/<see cref="ForwardedWarning"/>
        /// (<c>HED7012</c>/<c>HED7013</c>). Cached per (id, severity) because generators run in-IDE.</summary>
        public static DiagnosticDescriptor Forwarded(string id, bool isWarning)
        {
            if (string.IsNullOrEmpty(id))
                return isWarning ? ForwardedWarning : ForwardedError;

            return ForwardedDescriptors.GetOrAdd((isWarning ? "W" : "E") + id, _ =>
            {
                var title = HeddleDiagnosticCatalog.TryGet(id, out var row)
                    ? row.Title
                    : isWarning ? "Heddle template warning" : "Heddle template error";
                return new DiagnosticDescriptor(id, title, "{0}", Category,
                    isWarning ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error, isEnabledByDefault: true);
            });
        }

        /// <summary>The build-time message for a forwarded entry: the front end's own text plus the
        /// warning's <c>Fix</c> as a trailing sentence when it carries one. The build surface has the least
        /// interactive tooling of the three, so dropping the remediation text there
        /// withholds the most help from the user who needs it most.</summary>
        public static string ForwardedMessage(string message, string fix) =>
            string.IsNullOrEmpty(fix) ? message : message + " Fix: " + fix;

        /// <summary>An <c>AdditionalFiles</c> <c>.heddle</c> source could not be read at generation time.</summary>
        public static readonly DiagnosticDescriptor UnreadableFile =
            FromCatalog(HeddleDiagnosticIds.BuildUnreadableFile);

        /// <summary>Two templates in one compilation normalize to the same key (position: the second file).</summary>
        public static readonly DiagnosticDescriptor DuplicateKey =
            FromCatalog(HeddleDiagnosticIds.BuildDuplicateKey);

        /// <summary>Explicit <c>Key</c> or <c>Name</c> metadata the generator cannot use: a value that is
        /// empty/whitespace, carries a <c>.</c>/<c>..</c> segment, or normalizes to an empty string — or (the
        /// <c>Name</c> arm) an import name another template already answers to.</summary>
        public static readonly DiagnosticDescriptor InvalidKeyMetadata =
            FromCatalog(HeddleDiagnosticIds.BuildInvalidKeyMetadata);

        /// <summary>An <c>@&lt;&lt;</c> import names a template by its registration key while that template also
        /// carries a registered <c>Name</c>. Both spellings resolve — <c>Name</c> is additive — so this is advice on
        /// the preferred spelling, never a break. Reported at the <c>@&lt;&lt;{{…}}</c> block in the importer.</summary>
        public static readonly DiagnosticDescriptor NamedTemplateImportedByKey =
            FromCatalog(HeddleDiagnosticIds.BuildNamedTemplateImportedByKey);

        /// <summary>A static piece contains an unpaired surrogate; the u8 twin is suppressed for the template
        /// (string output unaffected).</summary>
        public static readonly DiagnosticDescriptor SurrogatePiece =
            FromCatalog(HeddleDiagnosticIds.BuildSurrogatePiece);

        /// <summary>A named extension resolves to no <c>[ExtensionName]</c> type in any referenced assembly
        /// (position: the call). Fires only for an extension-only call shape — a bodied call — so a function-compatible
        /// bare call is never misclassified (that path draws HED7014).</summary>
        public static readonly DiagnosticDescriptor ExtensionNotBindable =
            FromCatalog(HeddleDiagnosticIds.BuildExtensionNotBindable);

        /// <summary>A bound extension outside the engine assembly overrides <c>InitStart</c>/
        /// <c>CompleteInit</c> — compile-time logic the generator cannot evaluate; precompiled binding would silently
        /// skip it (position: the call).</summary>
        public static readonly DiagnosticDescriptor ExtensionOverridesHook =
            FromCatalog(HeddleDiagnosticIds.BuildExtensionOverridesHook);

        /// <summary>The <c>@model</c>/<c>::</c> type name does not resolve in the compilation or
        /// its references — a genuine typo/unresolvable symbol reported natively before the C# compiler sees the
        /// generated code (position: the directive).</summary>
        public static readonly DiagnosticDescriptor UnresolvableModelType =
            FromCatalog(HeddleDiagnosticIds.BuildUnresolvableModelType);

        /// <summary>A member path does not resolve on the model type, mirroring the runtime member
        /// tier order (position: the path). Only fires for a genuine property-not-found on a resolved, non-dynamic
        /// model — the same failure the runtime raises as HED0001, surfaced natively at the template span.</summary>
        public static readonly DiagnosticDescriptor UnresolvableMember =
            FromCatalog(HeddleDiagnosticIds.BuildUnresolvableMember);

        /// <summary>A model type or model member the engine reads by reflection and generated code may not name —
        /// <c>internal</c> in a referenced assembly. The template degrades to the dynamic tier rather than
        /// pre-compiling a name the consumer's build would reject (position: the directive, or the path).</summary>
        public static readonly DiagnosticDescriptor InaccessibleModelSymbol =
            FromCatalog(HeddleDiagnosticIds.BuildInaccessibleModelSymbol);

        public static readonly DiagnosticDescriptor OptionParseError =
            FromCatalog(HeddleDiagnosticIds.BuildOptionParseError);

        /// <summary>An <c>@&lt;&lt;</c> import path is not among the compilation's <c>.heddle</c> <c>AdditionalFiles</c>.</summary>
        public static readonly DiagnosticDescriptor ImportNotIncluded =
            FromCatalog(HeddleDiagnosticIds.BuildImportNotIncluded);

        /// <summary>The wrapper for a forwarded front-end error carrying no id of its own.</summary>
        public static readonly DiagnosticDescriptor ForwardedError =
            FromCatalog(HeddleDiagnosticIds.BuildForwardedError);

        /// <summary>The wrapper for a forwarded front-end warning carrying no id of its own.</summary>
        public static readonly DiagnosticDescriptor ForwardedWarning =
            FromCatalog(HeddleDiagnosticIds.BuildForwardedWarning);

        /// <summary>Two template keys sanitize to one generated entry-class identifier.</summary>
        public static readonly DiagnosticDescriptor DuplicateSanitizedName =
            FromCatalog(HeddleDiagnosticIds.BuildDuplicateSanitizedName);

        /// <summary>Two template keys differ only by case; ordinal keys make one shadow the other.</summary>
        public static readonly DiagnosticDescriptor CaseOnlyKeyTwin =
            FromCatalog(HeddleDiagnosticIds.BuildCaseOnlyKeyTwin);

        /// <summary>A called function name is neither a default built-in nor exported by any
        /// referenced assembly (a delegate-only registration, not representable in assembly metadata), so there is
        /// nothing for build-time discovery to bind against. The template is left un-precompiled with a
        /// fallback-marker manifest entry; it renders through the dynamic path at run time.</summary>
        public static readonly DiagnosticDescriptor UnresolvableFunction =
            FromCatalog(HeddleDiagnosticIds.BuildUnresolvableFunction);

        /// <summary>The emitter declined to precompile for a reason with no more specific channel. The
        /// emitter has always computed this reason; nothing reported it, because the generator's
        /// <c>if (Emitted) … else if (IsMarker)</c> had no final <c>else</c>. A consumer therefore had no
        /// way to learn that a template it believed precompiled was in fact rendering dynamically.</summary>
        public static readonly DiagnosticDescriptor TemplateNotPrecompiled =
            FromCatalog(HeddleDiagnosticIds.BuildTemplateNotPrecompiled);

        /// <summary>The diagnostic-property key HED7031 carries its <see cref="Emit.RefusalCategory"/> under —
        /// the machine-readable half of the refusal, beside the human-readable message, so tooling and the
        /// degrade-expectation seam can pin the CLASS of a refusal rather than a message substring.</summary>
        public const string RefusalCategoryProperty = "HeddleRefusalCategory";

        /// <summary>A branch <c>Continuation</c>/<c>Terminal</c> extension (<c>[BranchRole]</c>)
        /// does not carry <c>[ScopeChannel]</c>, so its <c>TryRead</c> of the branch state always misses at
        /// render. Additive and never fired by the built-ins, which all comply.</summary>
        public static readonly DiagnosticDescriptor BranchRoleMissingScopeChannel =
            FromCatalog(HeddleDiagnosticIds.BuildBranchRoleMissingScopeChannel);

        /// <summary>The generator twin of the dynamic tier's malformed-<c>[Prop]</c> declaration
        /// diagnostics (HED5007/HED5008/HED5009/HED5010/HED5015) — the generator does not run
        /// <c>HeddleCompiler</c>, so without this a malformed extension parameter declaration would degrade
        /// silently on the build tier. <c>{1}</c> names the specific fault (duplicate/reserved name,
        /// non-convertible default, unusable type, or an inherited-<c>[Prop]</c> re-declaration widening the
        /// inherited type). Position: the extension call site in the template.</summary>
        public static readonly DiagnosticDescriptor MalformedExtensionParameter =
            FromCatalog(HeddleDiagnosticIds.BuildMalformedExtensionParameter);

        /// <summary>A template is not under <c>HeddleTemplateRoot</c> and carries no explicit
        /// <c>Key</c> metadata, so its directory is dropped and it registers under a flattened filename key that no
        /// root-relative runtime lookup can hit. Behavior is unchanged (the flattened key still registers) — the
        /// warning makes the previously silent degrade visible, and explains a HED7002 raised by two out-of-root
        /// files sharing a filename.</summary>
        public static readonly DiagnosticDescriptor TemplateOutsideRoot =
            FromCatalog(HeddleDiagnosticIds.BuildTemplateOutsideRoot);

        /// <summary>The emitter threw while generating a template — a <b>generator defect</b>, not a
        /// template authoring error. Every intentional refusal already leaves the emitter through a return path (a
        /// HED7014 fallback-marker result, or an unsupported-construct reason), so an exception has no legitimate
        /// meaning and must surface instead of degrading silently to the dynamic path. Reported per template and at
        /// <see cref="Location.None"/>: the pass continues, so the remaining templates and the manifest still emit.</summary>
        public static readonly DiagnosticDescriptor EmitterFault =
            FromCatalog(HeddleDiagnosticIds.BuildEmitterFault);

        /// <summary>The <c>Heddle</c> assembly was not found among the compilation's referenced
        /// assemblies (aliased, embedded, or ILMerged), so the manifest's <c>engineVersion</c> is the generator's own
        /// version rather than an observed one. The runtime's engine-compatibility gate then decides whole-assembly
        /// registration on a heuristic — a warning, not an error, because precompilation is contractually additive
        /// and the value is right whenever the generator and engine version in lockstep.</summary>
        public static readonly DiagnosticDescriptor EngineVersionUnresolved =
            FromCatalog(HeddleDiagnosticIds.BuildEngineVersionUnresolved);

        /// <summary>An <c>[assembly: ExportFunctions(...)]</c> container that is not a public
        /// static class. The runtime raises a hard <c>ArgumentException</c> from
        /// <c>FunctionRegistry.RegisterFrom</c>, so the build errors rather
        /// than masking a host configuration error until first render.
        /// <c>{0}</c> is the runtime's own message for the same container, so the two tiers say the same
        /// thing. Reported once per ineligible container, at <see cref="Location.None"/>: the attribute lives in
        /// the consuming assembly, not in any template.</summary>
        public static readonly DiagnosticDescriptor IneligibleExportContainer =
            FromCatalog(HeddleDiagnosticIds.BuildIneligibleExportContainer);

        /// <summary>A type name several types answer to, which the template's <c>@using</c>
        /// imports do not settle. The runtime throws "the type name is ambigous" for the same input (on both the
        /// dotted and the short-name arm), so the build tier raises a matching error
        /// rather than binding one of the candidates and emitting typed code off a type the runtime might not
        /// choose. Position: the directive that named the type.</summary>
        public static readonly DiagnosticDescriptor AmbiguousTypeName =
            FromCatalog(HeddleDiagnosticIds.BuildAmbiguousTypeName);

        /// <summary>An <c>@profile(){{…}}</c> value that is neither <c>text</c> nor <c>html</c>.
        /// The runtime rejects the template outright (HED2001); ignoring the directive would
        /// precompile a template whose rendered output the dynamic tier would never produce — and the options
        /// fingerprint, which keeps the <em>compile-time</em> profile, cannot catch it. Position: the directive.</summary>
        public static readonly DiagnosticDescriptor UnknownOutputProfile =
            FromCatalog(HeddleDiagnosticIds.BuildUnknownOutputProfile);

        /// <summary>A call-site fill of a private region — the build-time twin of the
        /// runtime's HED5019. Reproduces the runtime's reaction to the <c>Private</c> region-fill verdict
        /// (retract the parse-emitted base-not-found error, raise once per candidate) so the two tiers report the
        /// same error at the same position. Position: the override declaration.</summary>
        public static readonly DiagnosticDescriptor RegionNotPublic =
            FromCatalog(HeddleDiagnosticIds.BuildRegionNotPublic);

        /// <summary>A function call the <b>shared</b> overload ranker proved illegal —
        /// <c>BindOutcome.Ambiguous</c> (the runtime's HED1013) or <c>BindOutcome.None</c> (HED1012) — over
        /// arguments the estimator could type. Without it the generator would compute that verdict and then
        /// report nothing, so a provably illegal template builds green and fails at first render — the same
        /// silence <see cref="IneligibleExportContainer"/> (HED7021) exists to prevent.
        /// <para><b>The side condition is load-bearing.</b> It fires only when every argument estimate is
        /// describable; an <c>Unknown</c> estimate leaves the ranker with nothing to rank, so the refusal is a
        /// generator limitation rather than a proof and still degrades silently. <c>{0}</c> is the runtime-shaped
        /// sentence naming the call and its candidate signatures, <c>{1}</c> the runtime id the build is the twin
        /// of. Position: the call in the <c>.heddle</c> file.</para></summary>
        public static readonly DiagnosticDescriptor FunctionCallNotBindable =
            FromCatalog(HeddleDiagnosticIds.BuildFunctionCallNotBindable);

        /// <summary>A template carrying both an <c>@model</c> directive and <c>ModelType</c> item metadata whose
        /// spellings resolve to different types. The runtime reads only the directive, so quietly preferring either
        /// spelling would let the two tiers type the same template differently; the build refuses instead.
        /// Positioned at the file start — the metadata has no in-file span.</summary>
        public static readonly DiagnosticDescriptor ConflictingModelTypeDeclarations =
            FromCatalog(HeddleDiagnosticIds.BuildConflictingModelTypeDeclarations);
    }
}
