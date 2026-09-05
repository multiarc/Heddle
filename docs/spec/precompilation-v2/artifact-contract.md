# The compiled form — artifact contract

Back to the [entry document](README.md). This is the one contract the build host (`heddle compile`, phase 2) and the
runtime loader (phase 1) must agree on byte for byte; phase 3's site table is validated against it. It pins what the
two sides need to agree on — container, sections, encodings, identity forms, site ids, determinism — and leaves record
layouts inside a section to the implementer. Encodings are pinned, not left to the writer's convenience, because an
artifact is read by every later runtime of the same major ([PD4](../../plan/precompilation-v2/decisions.md#pd4--artifact-compatibility):
`PrecompiledSchema.IsEngineCompatible` admits an artifact to any same-major runtime that is not older). Within a
major the form changes only additively — a new optional section or a new trailing field, read as its default when
absent — with a stored pre-change fixture proving it; a breaking change bumps
`PrecompiledSchema.CompiledFormSchemaVersion` and older artifacts are refused
([breaking-windows rule 7](../common/breaking-windows.md#policy-applies-to-every-window)).

## AC-1 — Carriage

The artifact is one embedded resource per consumer assembly, logical name `Heddle.CompiledForm`, holding every template
the assembly precompiles. It is reached through the marker attribute; the attribute type and its constructor shape are
the stable outermost contract:

```csharp
[assembly: Heddle.Precompiled.HeddleCompiledTemplates(
    typeof(Heddle.Generated.HeddleArtifact), /* schemaVersion */ 4, /* engineVersion */ "3.0.0")]
```

`ManifestType` is the generated `HeddleArtifact` class (namespace `$(HeddleGeneratedNamespace)`), which implements
`IHeddleCompiledArtifact.OpenArtifact()` by returning `GetManifestResourceStream("Heddle.CompiledForm")` on its own
assembly. `SchemaVersion` is `PrecompiledSchema.CompiledFormSchemaVersion`; `EngineVersion` is
`PrecompiledSchema.FormatEngineVersion` of the `Heddle.dll` the host compiled with. *Rationale:* one resource per
assembly is one registration, one digest and one site table; reaching it through the marker's `ManifestType` keeps
the attribute constructor the only thing `Register` reads before the schema gate.

## AC-2 — Encodings

- Integers: unsigned LEB128 varints for every count, index and length; signed values as zig-zag LEB128; `int64`
  literals likewise; `double` as 8 bytes IEEE little-endian; `decimal` as its four `int` parts little-endian;
  `bool` as one byte `0`/`1`. *Rationale:* varints keep the artifact small for the index-dense sections and need no
  alignment logic on `netstandard2.0`.
- Strings: every string in the artifact is an index into the string table (AC-3); a string-table entry is a
  UTF-16LE code-unit sequence prefixed by its code-unit count. *Rationale:* any .NET string — a static piece
  containing an unpaired surrogate included — round-trips exactly, so the string sink renders the piece as-is and
  the runtime's own `Encoding.UTF8.GetBytes` at materialization produces the UTF-8 sink's bytes.
- Positions: `BlockPosition` as `(StartIndex, Length)` varints, offsets into the template's decoded text in UTF-16
  code units (the `LineIndex` rule).
- Nullable references: a presence byte followed by the value when present.
- Section table: fixed-size entries `(sectionId: u16 LE, offset: u32 LE, length: u32 LE)`.

## AC-3 — Container and sections

```
Artifact := Magic "HCF3" (4 bytes) | schemaVersion u32 LE | sectionCount u32 LE | SectionTable | Sections…
```

*Rationale:* a section table lets a later same-major runtime skip an additive section it does not know and lets the
loader decode row-level sections at registration without touching document bodies.

| Id | Section | Content |
| --- | --- | --- |
| 1 | `Header` | Engine version string; builder version string (`Heddle.Build`); assembly-wide options: `ExpressionMode`, `TrimDirectiveLines`, default `OutputProfile`; template count; the artifact digest field (AC-6). |
| 2 | `Strings` | The string table (AC-2). |
| 3 | `Types` | Type references (AC-4). |
| 4 | `Extensions` | Extension identities: registry name, type identity (AC-4 nominal form), `PropLayout.Fingerprint` string or absent. |
| 5 | `Functions` | Function binding rows: name, bound target type identity or *late-bound*, overload count — the `PrecompiledFunctionBinding` triple. |
| 6 | `Members` | Member paths (AC-5): start type, segments, and per hop the identity the binding gate compares. |
| 7 | `Expressions` | Native-expression syntax trees: the closed `ExprNode` vocabulary (`LiteralNode`, `ThisNode`, `PathNode`, `IndexNode`, `CallNode`, `UnaryNode`, `BinaryNode`, `TernaryNode`, `MethodCallNode`) with `ExprOperator` and positions; literal values typed as in AC-2; a tree records its scope types (model, chained, root) as type refs and whether it contains a deferred call. |
| 8 | `CSharp` | Embedded C# sites: source text, `@using` namespaces, model/chained/root type refs, position. |
| 9 | `Documents` | Shaped documents and their element lists (AC-7), each with its parse facts (AC-7a). |
| 10 | `Definitions` | Definition and region layouts per template: name, base/override chain, `DefinitionItem.ModelType` spelling and its resolved type ref, `ParameterTemplate` text, prop declarations with `PropSlot` index/type/default, slot type, region declarations and fills, positions. |
| 11 | `Templates` | One row per template (AC-8). |
| 12 | `Sites` | The site table (AC-6). |

An unknown section id is skipped by length; a required section missing is a malformed artifact and `Register`
throws `PrecompiledRegistrationException`.

## AC-4 — Type identity

A type reference is one of:

- **Named**: `fullName` (CLR metadata full name, nested types joined with `+`, no type arguments — `AqnFormatter`'s
  form), `assemblySimpleName`, and a `framework` flag.
- **Constructed generic**: a Named definition plus an ordered list of type-ref arguments.
- **Array**: element type ref plus rank.
- **Dynamic**: the engine's `ExType.Dynamic` — a template typed `:: dynamic`; distinct from `System.Object`.

The nominal string used wherever an identity is compared (extension bindings, function targets, model
type details) is exactly `ReflectionTypeIdentity.AqnSansVersion` — `Ns.Outer+Inner, AssemblySimpleName`.

**The `framework` set ([PD6](../../plan/precompilation-v2/decisions.md#pd6--build-host)).** The writer sets
`framework` on a type whose assembly satisfies all of: it is located under the host runtime's directory
(`RuntimeEnvironment.GetRuntimeDirectory()`) or is listed in the host's trusted-platform assemblies
(`AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`); it is not a member of the implementation image set the
host was given; and it is not one of the host's own assemblies (`Heddle`, `Heddle.Language`,
`Antlr4.Runtime.Standard`, `Microsoft.CodeAnalysis*`). Its `assemblySimpleName` is advisory. Every other type is
non-framework and its assembly name is authoritative.

**Resolution versus comparison.** Two operations exist and the gate uses them in a fixed order:

- *Resolve by name* produces a `Type`. A `framework` ref resolves by `fullName` in `typeof(object).Assembly`, then
  across assemblies loaded into the default load context, by full name alone. A non-framework ref resolves by
  `assemblySimpleName` then `fullName` over the registered model assemblies and the observable loaded set (D11); a
  same-named type in another assembly is not a match. Only the refs that must become `Type`s are resolved by name:
  a template row's root model type, a definition's resolved `:: T` model type, and the scope types of sites at
  materialization.
- *Compare by identity* produces a verdict without resolving. Every type reached through the live member graph is
  compared to the recorded identity and never resolved by name, so the gate does not depend on which assemblies have
  loaded beyond the root model type's, and a framework type reached through the graph is compared, never looked up.
  Two comparison forms exist: an extension's live type, a function target's declaring type and the root model type
  compare by the **nominal form** (`AqnSansVersion`); a member hop's declaring type and member type compare by the
  **type-ref form** — the full type reference above, structurally: a Named ref by `fullName` and (for non-framework)
  `assemblySimpleName`, a constructed generic by its definition and every argument recursively, an array by element
  and rank, `Nullable<T>` as the constructed generic it is — so `List<A>` → `List<B>` or `int?` → `long?` between
  build and load is a mismatch, not a match.

A resolution that fails or a comparison that differs is a binding-gate failure (`MemberBindingMismatch`), never a
throw.

## AC-5 — Member identity

A member path is `(startType, segments[])` plus, per hop, `(declaringType, memberName, memberType)` — the identity
`MemberPathResolver.TryResolve` bound at build. At load the same resolver runs from the resolved start type; the path
binds when every hop resolves to a member whose declaring type and member type compare equal in the type-ref form
(AC-4).
Any other outcome — unresolved, a different declaring type, a different member type, an accessibility change — is
`MemberBindingMismatch` with detail `Member '<start>.<a>.<b>': manifest=<identity> live=<identity|unresolved>`. A hop
the engine classifies `DynamicHop` records no identity and binds through the engine's dynamic parameter at load (a
declared class under strict mode, [phase 3](phase-3-generated-sites.md)).

## AC-6 — Site ids and the artifact digest

Every delegate-bearing site — a member accessor, a native expression, an embedded C# expression, a late-bound call, a
refusal-class site — is a row in `Sites`: `(templateIndex, siteOrdinal, kind, payloadRef)`. **Site ordinal** is the
site's zero-based position in a depth-first, document-order walk of its template's root document (chains
right-to-left as the compiler visits them, then each item's parameter, then its body document, then its caller
content). A **site id** is the triple (template `ContentHash`, template row index, `siteOrdinal`). *Rationale:* the
content hash ties an id to the text, the row index separates two items with identical text under different
`ModelType` metadata or `OutputProfile`, and the ordinal is stable under the fixed walk.

The `Header` carries the **artifact digest**: lowercase-hex SHA-256 of the artifact bytes with the digest field
zeroed. Phase 3's generated site table records the digest of the artifact it was printed from; the loader consults a
site table only when the digests match, so a table can never serve a site of a different compilation.

## AC-7 — Documents

A document is `(shapedText, needsLocals, parseFacts, elements[])`; an element is either a static piece (string
index) or a chain. A chain is an ordered list of items; an item is `(extensionRef, position, returnType, parameter,
body?, callerContent?, props?)` where `parameter` is one of: none, constant (typed literal), model path (member ref),
root path, dynamic path (segments), chain (nested chain), native expression (expression ref, with props-slot usage
flagged), C# expression (site ref), late-bound call (expression ref), refusal site (source text + compile context:
model/chained/root type refs, namespaces, position, refusal class). A body records its raw text, shaped text, the
data and chained types the hook handed the body compile (as type refs) and, when the engine compiled it to
processors, a document ref — the three post-states of `AbstractExtension.InitSubTemplate`. Props are the frozen
prototype (typed literals, `null` for a dynamic slot), and per dynamic slot the slot index, its expression ref and the
conversion target type ref. Definition call sites reference their `Definitions` row and carry the caller-content
document and slot-mode flag.

### AC-7a — Parse facts

Per document, the facts of the `ParseContext` the text path handed to `TemplateFactory.Create`, `InitStart`,
`CompleteInit` and every compile the document triggers: the document's `Offset`; whether it is a definition context
(`ParseContext.InDefintionContext`); the names of the definitions visible from it (the `DefinitionsBlock` keys, with
refs into `Definitions`); and, per item, the `ParameterTemplate` text. The loader synthesizes a `ParseContext`
from these so `DefenitionExists`/`GetDefenition` (which select `HED1001` versus `HED1002` and drive
`DefinitionBaseExtension`), `OutExtension`'s slot derivation and `PartialExtension`'s child scheduling behave as on
the text path.

## AC-8 — Template rows

`(key, registeredName?, contentHash, modelType, modelTypeIsAmbient, isDynamic, entryPointTypeName,
imports[(key, contentHash)], optionsFingerprint(profile, mode, trim), extensionRefs[], functionRows[],
rootDocumentRef, definitionsRef, siteCount, refusalSites[(siteOrdinal, class, detail, position)])`. `key` and
`registeredName` are `TemplateKey`-normalized; `contentHash` is `ContentHash.HashText` of the decoded text;
`entryPointTypeName` is the typed wrapper's full name in the artifact's own assembly (`Heddle.Generated.Views_Home_Index`),
which the loader resolves with `Assembly.GetType` on the registering assembly; `optionsFingerprint` is per template
because `OutputProfile` is per item ([PD3](../../plan/precompilation-v2/decisions.md#pd3--msbuild-surface));
`refusalSites` is the public `PrecompiledTemplateInfo.RefusalSites` collection, so a host and the corpus gate
enumerate declared exceptions without materializing.

## AC-9 — Determinism

Serializing the same compiled template twice yields identical bytes; serialize → load → serialize yields identical
bytes. Rules that make it so: string-table entries in first-use order of a fixed walk (rows in ordinal key order,
then each row's sections in table order); type refs interned by identity in first-use order; no timestamps, GUIDs,
absolute paths or host-specific values anywhere in the artifact; `Header` version strings are the only environment
facts and are the compile's declared inputs.

## AC-10 — What the artifact does not carry

`TemplateOptions` beyond the fingerprint (encoder, render budget, `MaxRecursionCount`, functions registry, root path,
staleness policy are the host's — a materialized definition reads the request's `MaxRecursionCount` exactly as a
text compile does); extension hook state (hooks run at load); compiled delegates or IL; generated
C# text; the UTF-8 pre-encoding of pieces (the runtime's `NormalStrategy`/`DocumentStrategy` build it at
materialization exactly as the text path does).
