# Phase 6 supplement — the diagnostic-catalog schema

Supplement to [Phase 6 — diagnostics, line mapping, and shared utility
tables](phase-6-diagnostics-utilities.md), backing decisions D4 (catalog), D3 (severity/Fix
mapping), and D12 (test suite). This is plan-level detail — the elaborating spec owns final
signatures — but everything here is decided, not open.

## Constraints the schema satisfies

- **netstandard2.0, zero dependencies, zero Roslyn.** The file compiles inside `Heddle.dll` (all
  TFMs incl. netstandard2.0) and as a linked `<Compile>` item inside the generator, which must
  not reference `Heddle.dll` and must not leak `Microsoft.CodeAnalysis` types into shared code.
  Consequence: the catalog is a static data table over `string` and one two-value enum — the
  `DiagnosticDescriptor` projection lives generator-side only.
- **The [claimed-ID registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
  is normative for which IDs exist**; the runtime's raise sites are the default authority for
  message text and severity ([plan D4](phase-6-diagnostics-utilities.md#d4--heddlediagnosticcatalog-one-shared-data-table-the-generator-projects-it-to-descriptors));
  for HED7xxx (never raised by the runtime) the authority is the shipped
  `GeneratorDiagnostics` descriptor text.
- **No second copy of message prose without a consumer.** `MessageFormat` is nullable and
  populated only where a second consumer formats through it (HED7xxx descriptors; the F2 twin
  vocabularies). Rows without it carry title + severity only.

## Shape

```csharp
namespace Heddle.Data
{
    /// <summary>Default severity of a diagnostic ID, independent of the surface reporting it.</summary>
    internal enum HeddleDiagnosticSeverity { Error, Warning }

    /// <summary>One registry row: the shared identity of a HED diagnostic.
    /// MessageFormat is null for rows whose message text is owned by a single raise site.</summary>
    internal readonly struct HeddleDiagnosticInfo
    {
        public readonly string Id;
        public readonly string Title;                      // short noun phrase, e.g. "Unresolvable member path"
        public readonly HeddleDiagnosticSeverity DefaultSeverity;
        public readonly string MessageFormat;              // composite format ({0}…{n-1}) or null
    }

    /// <summary>The id → row table. Pure data; the single code-side registry
    /// (docs-side registries are gated against it by DiagnosticIdTests).</summary>
    internal static class HeddleDiagnosticCatalog
    {
        public static bool TryGet(string id, out HeddleDiagnosticInfo info);
        public static IReadOnlyCollection<HeddleDiagnosticInfo> All { get; }   // test enumeration

        // Shared vocabulary constants (single source for both tiers — plan D4):
        public static class PropFaults
        {
            public static readonly string[] ReservedNames;   // { "out", "this" }
            // Ordered fault identifiers matching the runtime's five-step validation order
            // (PropLayout.ResolveFromExtension) that the generator's TemplateEmitter twin follows.
            public static readonly string[] FaultOrder;
        }
    }
}
```

Notes:
- `internal` + existing `InternalsVisibleTo` (tests, LSP) — nothing public ships
  (plan Back-compat).
- Backing storage is a static `Dictionary<string, HeddleDiagnosticInfo>` built in a type
  initializer from literals; no reflection, no allocation after cctor, no render-path presence at
  all (the catalog is compile/tooling-path only).
- The severity enum is deliberately two-valued: Heddle compile diagnostics are errors or warnings
  (info-level would be a registry-level D1 change, out of scope). The LSP's own
  `HeddleDiagnosticSeverity` (`src/Heddle.LanguageServices/HeddleDiagnostic.cs:5`) keeps its name
  and maps trivially — different namespace, no collision; the elaborating spec may choose to
  re-point the LSP enum at the shared one since the LSP compiles against `Heddle` internals.

## Generator-side projection

`src/Heddle.Generator/Diagnostics/GeneratorDiagnostics.cs` keeps its named descriptor fields (all
call sites stay valid) but each becomes a projection of its catalog row:

```csharp
private static DiagnosticDescriptor FromCatalog(string id) { /* TryGet + map severity */ }

public static readonly DiagnosticDescriptor UnreadableFile = FromCatalog("HED7001");
// … one line per existing descriptor; hand-written title/format/severity literals deleted.

/// <summary>Descriptor for a forwarded front-end diagnostic (plan D2/D3): the real ID with the
/// catalog's title/severity and a passthrough "{0}" format (the front end already formatted the
/// message; Fix is appended by the caller). Falls back to ForwardedError/ForwardedWarning
/// (HED7012/HED7013) when id is null or unknown to the catalog.</summary>
public static DiagnosticDescriptor Forwarded(string id, bool isWarning);
```

Severity mapping is the one Roslyn touchpoint:
`HeddleDiagnosticSeverity.Warning → DiagnosticSeverity.Warning`, `Error → Error`;
`isEnabledByDefault: true` throughout (matching every shipped descriptor); category stays
`"Heddle.Precompile"`. `Forwarded` caches descriptors per id in a static
`ConcurrentDictionary` so repeated warnings do not re-allocate descriptors per report (compile
path, but generators run in-IDE — cheap hygiene, not a perf claim).

The forwarding call sites (`HeddleTemplateGenerator.cs:312-330`) become symmetric:

```csharp
var descriptor = GeneratorDiagnostics.Forwarded(entry.Id, entry.IsWarning);   // entry: projection struct (D5)
var message = entry.Fix != null ? entry.Message + " Fix: " + entry.Fix : entry.Message;
spc.ReportDiagnostic(Diagnostic.Create(descriptor, location, message));
```

## Row inventory

Three groups, in the order they are entered:

1. **HED7xxx (17 rows, from `GeneratorDiagnostics`).** Transcribed verbatim — id, title,
   severity, and full `MessageFormat` (these rows are consumed by the descriptor projection, so
   the format column is populated). In the same work item, `HeddleDiagnosticIds` gains consts for
   the HED7xxx block (it has none today — the generator block is currently the only shipped block
   not reflectable from the constants class), which is what lets the D12 completeness test cover
   it. Adding consts is additive; no ID changes. `HED71xx` (runtime registration/fallback,
   registry rows `HED7101`–`HED7103`) rows are transcribed from their runtime raise sites like
   any other runtime ID.
2. **Runtime-raised IDs (HED0xxx–HED5xxx, HED9001).** One row per `HeddleDiagnosticIds` const:
   `Title` written fresh (short noun phrase — raise sites have no titles today; the registry
   table's Notes column and the raise-site text are the sources), `DefaultSeverity` transcribed
   from the raise-site subtype (`HeddleCompileWarning` → Warning), `MessageFormat` **null**
   except the twin groups below. Severity transcription is mechanical and gated: the
   projection-equivalence corpus (D12.5) raises representative diagnostics and asserts the
   observed subtype matches the catalog severity.
3. **Twin-vocabulary rows (format column populated).** The three F2 twin groups where message
   knowledge must exist once because two tiers word the same fault:
   - member-path resolution — HED0001 (`MemberPathResolver.cs:77-82`) / HED7008
     (`SymbolMemberResolver` via `GeneratorDiagnostics`),
   - branch-role scope channel — HED3005 (`HeddleCompiler.cs:342-350`, incl. its Fix text as part
     of the vocabulary) / HED7016,
   - malformed `[Prop]` declaration — HED5007/5008/5009/5010/5015 (`PropLayout.cs:104-160`) /
     HED7017 (`TemplateEmitter.cs:819-930`), plus the `PropFaults.ReservedNames` and
     `PropFaults.FaultOrder` constants both validators consume.
   For these, both raise sites migrate to format through the catalog row (or through the shared
   fault constants where the twins legitimately word differently around a shared core), so the
   arity gate below is enforced by execution, not convention.

## Test mechanics (instantiates plan D12.2/D12.4)

- **Completeness:** reflect `HeddleDiagnosticIds` consts (the existing
  `DiagnosticIdTests.ConstantsMatchTheDiagnosticsTableOneToOne` mechanism) ⇄
  `HeddleDiagnosticCatalog.All` — bijective on `Id`.
- **Descriptor equality:** generator-side test enumerates `GeneratorDiagnostics`' public
  descriptor fields via reflection and asserts `(Id, Title, DefaultSeverity)` equals the catalog
  row — red if anyone reintroduces a hand-built descriptor.
- **Format well-formedness:** for every non-null `MessageFormat`, placeholders are exactly
  `{0}…{n-1}` (regex scan + `string.Format` smoke call with n sentinel args) — catches both
  gaps and out-of-range indices.
- **Arity vs raise sites:** for consumed rows, the raise is exercised: the twin fixtures compile
  templates/extensions triggering each twin on both tiers; a wrong arity throws
  `FormatException` at the raise, failing the fixture. For HED7xxx, `Diagnostic.Create` with the
  projected descriptor plays the same role inside the generator integration tests.
- **Docs gates (D12.3)** read `docs/spec/common/cross-cutting-decisions.md` and
  `docs/precompilation.md` relative to the test source's `[CallerFilePath]`, scan first-cell
  `HED` table rows (both `HED7001`–`HED7016`-style ranges and single-ID rows), and assert code ⊆
  docs and docs ⊆ code per block. The parser is ~30 lines and its grammar is stated in the test's
  failure message, making the tables machine-checked without becoming machine-hostile.

## What deliberately stays out of the catalog

- **Positions and trigger conditions** — those live in each owning spec's *Diagnostics* section
  per [D1](../spec/common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx); the
  catalog is identity (id/title/severity) plus shared message knowledge, not a spec mirror.
- **`Fix` text** — a property of the raise (`HeddleCompileWarning.Fix`), not of the ID; the same
  ID may carry situation-specific fixes.
- **Message text for single-owner rows** — see D4's rationale and OQ3's revisit trigger in the
  [main plan](phase-6-diagnostics-utilities.md#open-questions).
