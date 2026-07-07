# Phase 6 protocol supplement — LSP method mapping, DTOs, and distribution mechanics

Supplement to the [phase 6 specification](README.md). This document pins the LSP layer
method by method: lifecycle, position mapping, the per-capability request/response
behavior, the hand-written DTO subset, the semantic-token legend, completion context
detection with worked examples, the distribution mechanics of D19, and the
contract-test message fixtures. Decision rationale lives in the entry document
(D4–D8, D12–D19); this file is the implementer's protocol reference.

Normative protocol baseline: **LSP 3.17** ([README D5](README.md#d5--protocol-version-lsp-317-semantics-as-the-normative-baseline)).
Position encoding: `utf-16` (the mandatory default; not advertised).

## Server lifecycle

### Startup and handshake

`heddle-lsp` reads LSP over stdin/stdout (header-delimited, D6). Anything the server
logs for humans goes to the client via `window/logMessage` — **never** to stdout
(which carries the protocol) and never to stderr in normal operation. Command line:
`heddle-lsp` (serve), `heddle-lsp --version` (print the informational version and exit 0).

`initialize` behavior:

- `rootUri`/`workspaceFolders` → the workspace root; the server loads
  `.heddle-lsp.json` from it if present (D18), then applies
  `initializationOptions` (the mirror settings the VS Code client forwards) for any
  field the file did not set.
- The response advertises exactly:

```json
{
  "capabilities": {
    "positionEncoding": null,
    "textDocumentSync": { "openClose": true, "change": 1, "save": {} },
    "completionProvider": { "triggerCharacters": ["@", "(", ".", ":", ","] },
    "hoverProvider": true,
    "definitionProvider": true,
    "semanticTokensProvider": {
      "legend": {
        "tokenTypes": ["property", "function", "keyword", "operator", "macro", "comment"],
        "tokenModifiers": []
      },
      "full": true
    }
  },
  "serverInfo": { "name": "heddle-lsp", "version": "<informational version>" }
}
```

(`positionEncoding: null` above means the property is **omitted** in the real payload —
shown here only to make the pin explicit. `"change": 1` = `TextDocumentSyncKind.Full`.)

- Requests arriving before `initialize` → error `-32002` (ServerNotInitialized), per
  spec. Notifications before `initialize` other than `exit` are dropped.

### Shutdown

`shutdown` → cancel all pending analyses, dispose the `HeddleLanguageService` (which
unregisters and unloads the model ALC), reply `null`. `exit` → exit code 0 if `shutdown`
was received, 1 otherwise (spec-mandated). If the client transport closes without
`exit`, the server exits 1 after disposing.

### Cancellation

StreamJsonRpc maps `$/cancelRequest` to the `CancellationToken` parameter of the target
method automatically; every request handler takes and forwards the token into the facade
(honored between pipeline stages, D8). A cancelled request replies with error
`-32800` (RequestCancelled) — StreamJsonRpc produces this from the
`OperationCanceledException`.

## Position mapping

One conversion boundary (D11): handlers convert LSP `{line, character}` →
UTF-16 offset via `DocumentAnalysis.Lines` on entry, and offsets → positions on exit.
Rules:

- Offsets and `character` both count UTF-16 code units — no transcoding exists anywhere.
- Line splitting: `\n` terminates a line; a `\r` immediately before it belongs to the
  terminated line (matching the engine's `HeddleCompileResult` splitting).
- A position past the end of a line clamps to the line end; a line past the document
  clamps to the last line (LSP's documented lenient interpretation).
- Engine spans (`BlockPosition{StartIndex, Length}`) map to
  `Range{ start = OffsetToPosition(StartIndex), end = OffsetToPosition(StartIndex + Length) }`.
- `default(BlockPosition)` (`0:0` — the engine's no-position convention) → a zero-length
  range at the document start.

## Method-by-method behavior

Every params/result type named here is field-pinned in the [DTO subset](#dto-subset).

| Method | Kind | Params → result | Behavior |
| --- | --- | --- | --- |
| `initialize` | request | `InitializeParams` → `InitializeResult` | As above. Stores client capabilities (currently only read for future use — no branching on them in v1). |
| `initialized` | notification | empty object (no DTO — the target method takes no arguments) | No-op. |
| `shutdown` / `exit` | request / notification | none → `null` / none | As above. |
| `$/setTrace` | notification | `{ value }` — consumed opaquely, no DTO | Accepted, ignored. |
| `$/cancelRequest` | notification | `{ id }` — handled inside StreamJsonRpc, no DTO | Cancels the matching in-flight request ([Cancellation](#cancellation)). |
| `workspace/didChangeConfiguration` | notification | `DidChangeConfigurationParams` | Re-reads settings (file-wins precedence, D18); if `assemblies` changed → D14 reload (never a D23/D24 export rescan — one-shot per process; a change after the scan logs the restart-to-rescan note); if compile options changed → rebuild the workspace options template (a fresh function registry re-populated from the retained scan handles, D24), re-analyze all open documents and republish diagnostics. |
| `textDocument/didOpen` | notification | `DidOpenTextDocumentParams` | Store text + version; analyze immediately (no debounce); publish diagnostics. |
| `textDocument/didChange` | notification | `DidChangeTextDocumentParams` | Replace full text (sync kind Full — exactly one change event with no `range`); restart the 300 ms debounce; superseded in-flight analysis is cancelled (D8). |
| `textDocument/didSave` | notification | `DidSaveTextDocumentParams` | No-op (analysis keys on buffer content). |
| `textDocument/didClose` | notification | `DidCloseTextDocumentParams` | Publish an empty diagnostics array for the uri; release facade document state. |
| `textDocument/publishDiagnostics` | server → client | `PublishDiagnosticsParams` | After every completed analysis; see [Diagnostics](#diagnostics). |
| `textDocument/semanticTokens/full` | request | `SemanticTokensParams` → `SemanticTokens` | Ensure a current analysis (forcing one synchronously if stale, D8); emit per the [semantic-tokens section](#semantic-tokens). |
| `textDocument/completion` | request | `CompletionParams` → `CompletionItem[]` | Ensure current analysis; `GetCompletions(path, offset)`; map per the [completion section](#completion). Returns a plain `CompletionItem[]` (not `CompletionList` — the list is never incomplete); empty array when the context yields nothing. |
| `textDocument/hover` | request | `HoverParams` → `Hover` or `null` | `GetHover`; result → `Hover{ contents: MarkupContent(markdown), range }` or `null`. |
| `textDocument/definition` | request | `DefinitionParams` → `Location` or `null` | `GetDefinition`; single `Location` — the `Location[]` union arm is unused (D16 returns one target). |
| `window/logMessage` | server → client | `LogMessageParams` | Operational logging: model-assembly load failures, ALC collection warnings, client contract violations (D20). |
| `window/showMessage` | server → client | `ShowMessageParams` | User-actionable conditions: version mismatch, missing runtime, bad configuration (D20). |
| anything else | — | — | Not advertised; unknown requests get StreamJsonRpc's automatic `-32601` MethodNotFound; unknown notifications are ignored. |

URI handling: the server accepts `file://` URIs and converts to local paths
(`Uri.LocalPath`); non-file URIs (untitled buffers) are analyzed typelessly with
import/partial resolution disabled (no base path) — diagnostics and syntax-level
features still work.

### Error and cancellation behavior

Uniform rules — the server adds no per-method error vocabulary beyond these:

- **Before `initialize`** — every request answers `-32002` ServerNotInitialized;
  notifications other than `exit` are dropped (lifecycle section above).
- **Unknown method** — `-32601` MethodNotFound, produced by StreamJsonRpc automatically.
- **Undeserializable params** — `-32602` InvalidParams from the formatter; no custom
  handling.
- **Handler exception** — StreamJsonRpc's standard error envelope, code `-32000`
  (`JsonRpcErrorCode.InvocationError`), carrying the exception message. Document-store
  state is unaffected: handlers publish/mutate only after the facade call succeeds.
- **Cancellation** — `$/cancelRequest` maps to the handler's `CancellationToken`; a
  cancelled request replies `-32800` RequestCancelled. All four feature requests
  (`completion`, `hover`, `definition`, `semanticTokens/full`) forward the token into
  the facade, where it is honored between pipeline stages (D8); `initialize` and
  `shutdown` accept the token but complete without checking it (no long-running work).
- **Notification failures** — notifications never produce responses; a failure while
  handling one (for example an unreadable `.heddle-lsp.json` after
  `didChangeConfiguration`) is reported via `window/logMessage` and the notification is
  otherwise dropped (D20).
- **Ranged change events** — a `TextDocumentContentChangeEvent` with a non-null `range`
  (incremental event against the advertised Full sync) is a client contract violation:
  the event is skipped with a `window/logMessage` warning; full-text events in the same
  batch still apply in order (last one wins).

## Diagnostics

Projection of `DocumentAnalysis.Diagnostics` (engine `CompileErrors` +
`CompileWarnings`, D10):

| LSP `Diagnostic` field | Source |
| --- | --- |
| `range` | `Offset`/`Length` via the line map |
| `severity` | `HeddleCompileError` → 1 (Error), `HeddleCompileWarning` → 2 (Warning) |
| `code` | `DiagnosticId` (`"HED1003"`); omitted when null (legacy unassigned messages) |
| `source` | `"heddle"` |
| `message` | `Error` text; for warnings with `Fix`, `"{Error}\nFix: {Fix}"` |

One projection rule applies before publishing (facade-side, [README D25](README.md#d25--imported-file-diagnostics-re-anchor-to-the-import-site-the-importorigin-marker-gap-g6)):

- **Import-attributed entries** (`HeddleDiagnostic.ImportedFrom` non-null) publish with
  a **zero-width** `range` at the `@<<` / `@import()` / `@partial` site in the analyzed
  document and a message prefixed `imported '<path>': ` — the original `code`, severity
  and `Fix` handling are unchanged. The diagnostic appears at its true span only in the
  imported file's own analysis (open it, or follow the site's go-to-definition to the
  file).

No function-related projection rule exists: the
[OQ2 resolution](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)
dropped the `functions` declaration list and its facade-side `HED1001` suppression —
[README D24](README.md#d24--declarative-function-exports-scanned-exportfunctions-assemblies-register-into-the-real-workspace-registry-oq2-gap-g4b)'s
scanned exports register into the real workspace registry, so a false `HED1001` never
arises for them; a purely-runtime (delegate-only) host registration still draws a
real, editor-only `HED1001` (D24's documented limitation).

Not populated in v1: `codeDescription` (no per-ID docs anchors exist yet), `tags`,
`relatedInformation` (the import click-through candidate — deferred), `data`.
Publishing carries the analyzed `version` in
`PublishDiagnosticsParams.version`.

## Semantic tokens

Legend (fixed order — indexes are wire values): `property` 0, `function` 1, `keyword` 2,
`operator` 3, `macro` 4, `comment` 5. No modifiers.

Mapping per `HeddleTokenType` (all 23 values — the 20 pre-phase-1 members `Id` = 0
through `ParseError` = 19, verified against
[HeddleToken.cs](../../../src/Heddle/Language/HeddleToken.cs), plus phase 1's three
appends; "—" = **not emitted**, the TextMate layer keeps its lexical coloring per D17):

| HeddleTokenType | Emitted as | Note |
| --- | --- | --- |
| `Id` | `property` | Member-path segments, definition/extension name identifiers |
| `RootReference` | `keyword` | `::` root selector in parameters |
| `MemberSelector` | `operator` | `.` |
| `Out` | `macro` | The `@` directive sigil span |
| `SubStart` / `SubClose` | — | `{{` / `}}` |
| `CSharpToken` | — | Embedded C# — the TextMate C# include classifies it better than one flat color |
| `CSharpStart` | `keyword` | The inner `@` escaping to the C# tier |
| `DefStartName` / `DefEndName` | — | `<` / `>` |
| `DefType` | `keyword` | `::` in definition headers |
| `Delim` | `operator` | `:` |
| `DefStart` / `DefClose` | `keyword` | `@%` / `%@` |
| `Comment` | `comment` | |
| `OutParamStart` / `OutParamEnd` | — | Call parens |
| `LineTerminate` | — | |
| `DefOutputOnEnd` | `keyword` | `->` |
| `ParseError` | — | Diagnostics own errors; coloring an error span is noise |
| `Operator` (phase 1) | `operator` | |
| `Literal` (phase 1) | — | Token carries no kind (string vs number indistinguishable) — deferred with the engine-token trigger |
| `FunctionName` (phase 1) | `function` | Registry-function calls |

Additionally every `SkippedTokens` span (hidden channel: `@* … *@` comments, `@\`,
definition whitespace) is emitted as `comment` — matching the TextMate grammar's own
classification of those constructs.

Emission: collect all emitted tokens (typed tokens + skipped spans), sort by start
offset, split any token spanning a line break at line boundaries (LSP tokens are
single-line), then delta-encode per the spec:
`[deltaLine, deltaStartChar, length, tokenType, 0]`.

Worked example (asserted literally by `ProtocolContractTests`):

```heddle
@(Name)
@* hi *@
```

Tokens: `Name` = `Id`/property at line 0 chars 2–6; the comment = skipped span at line 1
chars 0–8. (`@(` and `)` are `Out`+parens → parens not emitted; `Out` covers `@` at
char 0.) Emitted, sorted: `@` (macro, line 0, char 0, len 1), `Name` (property, line 0,
char 2, len 4), comment (line 1, char 0, len 8).
`data = [0,0,1,4,0, 0,2,4,0,0, 1,0,8,5,0]`.

## Completion

### Context detection

Given the request offset, the handler asks the facade, which classifies context from the
analysis (tokens are document-ordered; the *anchor* is the last token starting before the
offset) plus the innermost scope span. Detection table, first match wins:

| # | Detection rule | Context |
| --- | --- | --- |
| 1 | Offset is inside a definition header prop list or between `@%…%@` outside a body | Directive/header — no items in v1 (header authoring aid deferred) |
| 2 | Anchor chain is `<definition-call name> (` … `,`/`(` immediately before offset (ignoring whitespace) **and** the named callee declares props | Named-argument position → prop items |
| 3 | Anchor is `MemberSelector` (`.`) with a path prefix | Member of resolved prefix type |
| 4 | Anchor is `RootReference` (`::`) inside parens | Root-model members |
| 5 | Offset is inside call parens (`OutParamStart` open, unmatched) | Expression position → members + `this` + functions + `true`/`false`/`null` + `::` |
| 6 | Anchor is `Out` (`@`) or `Delim` (`:` chain position) | Callable names → definitions + extensions + functions |
| 7 | Otherwise | No items (plain text — never guess) |

Rule 5's member items come from the **model-type set** of the innermost scope span
(D13): singleton set → that type's visible properties
(`MemberPathResolver.GetVisibleProperties` filter); multiple recorded types → the
name-based intersection; any dynamic or empty set → no member items.

### Trigger-character resolution

How each advertised trigger character (D12) lands in the detection table. The trigger
character itself is informational (`CompletionContext.triggerCharacter`) — detection
always classifies from the token stream, so explicit invocation (`triggerKind` 1,
Ctrl+Space) at the same offset resolves identically:

| Trigger | Resolution through the table (first match wins) |
| --- | --- |
| `@` | Rule 6 (callable names) — unless the offset sits in a definition header / between `@%…%@` outside a body, where rule 1 yields no items |
| `(` | Rule 2 (prop items) when the token before the paren names a props-declaring definition; otherwise rule 5 (expression position) |
| `.` | Rule 3 — members of the segment-by-segment-resolved prefix type; unresolvable prefix → empty (never a guess) |
| `:` | Rule 4 when it completes `::` inside parens (root-model members); rule 6 when it is the chain `Delim` outside parens (callable names); rule 1 (no items) inside definition headers. A named argument's `name:` colon sits inside call parens, so rules 2/5 fired already at the `(`/`,` — typing past the colon lands in rule 5 (the prop's value is an expression position) |
| `,` | Rule 2 (remaining props, already-passed filtered) when the callee declares props; otherwise rule 5 |

### Name sources (the v1 registry contents)

Extension and function completion is registry-driven at runtime (D12) — the live
`TemplateFactory` / `FunctionRegistry` contents **of the server process** are
authoritative. The server never runs host startup code, so a host's own runtime
registrations do **not** appear automatically (an earlier draft of this paragraph
claimed they would — corrected by the July 2026 gap analysis, G4): host extensions
arrive via the workspace `assemblies` scan
([README D23](README.md#d23--workspace-extension-exports-one-shot-scan-into-the-default-alc-real-registry-registration-oq2-gap-g4a)
— they enter the real `TemplateFactory`, so everything downstream is engine-true), and
declaratively exported host functions arrive via the same scan
([README D24](README.md#d24--declarative-function-exports-scanned-exportfunctions-assemblies-register-into-the-real-workspace-registry-oq2-gap-g4b)
— `FunctionRegistry.RegisterFrom` into the workspace registry, equally engine-true;
purely-runtime delegate registrations remain invisible — D24's documented limitation).
For the fixtures and the
worked examples below, the expected **bare-workspace** v1 contents are pinned here,
verified against the
current source and the phase 1–5 specs; a workspace with D23/D24 surface extends them:

- **Extensions** — `TemplateFactory.RegisteredNames()` yields 24 names at phase 6 time:
  the 19 `[ExtensionName]` names in current source (`""`, `date`, `for`, `guid`, `html`,
  `if`, `ifnot`, `import`, `int`, `list`, `model`, `money`, `out`, `param`, `partial`,
  `string`, `swap`, `time`, `using` — one attribute per extension class under
  [src/Heddle/Extensions/](../../../src/Heddle/Extensions)), plus phase 2's `raw`
  (second name on `EmptyExtension`) and `profile` (`ProfileExtension`), plus phase 3's
  `elif` + `elseif` (both on `ElifExtension`) and `else` (`ElseExtension`). Phases 4–5
  register no new names (phase 4's `range` is a function, not an extension). `""` —
  `EmptyExtension`'s unnamed alias — is filtered from completion; the other **23** are
  offered.
- **Functions** — `FunctionRegistry.Default` holds **18** names: phase 1 D13's frozen
  seventeen (`upper`, `lower`, `trim`, `len`, `contains`, `startswith`, `endswith`,
  `replace`, `substr`, `format`, `str`, `abs`, `min`, `max`, `round`, `floor`, `ceil`)
  plus phase 4's `range(start, last[, step])`. Overload counts and signatures come from
  `EnumerateOverloads()` at request time; D24-scanned function exports need no separate
  source — `RegisterFrom` makes them ordinary registry entries, enumerated by the same
  call and indistinguishable from built-ins.
- **Definitions** — never a fixed list: the document's `DefinitionInfo` set visible at
  the cursor's position — walk-order visibility with D26's surviving-entry rule
  (override layers replace; plain duplicates keep the first declaration; imports
  contribute their merged copy set — D16/D26). This is
  [OQ2](../OPEN-QUESTIONS.md#oq2--how-the-lsp-workspace-learns-about-host-registrations-from-gap-g4--resolved-july-2026)'s
  second knowledge source — declarations within the template itself, first-class per
  phase 1 D11's resolution order — ratified explicitly in [README D24](README.md#d24--declarative-function-exports-scanned-exportfunctions-assemblies-register-into-the-real-workspace-registry-oq2-gap-g4b).

### Item shapes

Facade kind = the `CompletionItemKind` value on the facade `CompletionItem`
([README — public API contract](README.md#public-api-contract)); LSP `kind` = the
numeric `CompletionItemKind` the server emits for it:

| Item class | Facade kind | LSP `kind` | `label` | `detail` | `insertText` |
| --- | --- | --- | --- | --- | --- |
| Member property | `Property` | 10 (Property) | property name | CLR type (`string`, `IReadOnlyList<Article>`) or `varies by call site` | name |
| Definition | `Definition` | 7 (Class) | definition name | header signature (`<card(style: string = "plain")> :: Article` / `abstract`) | name |
| Extension | `Extension` | 3 (Function) | extension name | `extension` (+ aliases when several names map to one class) | name |
| Registered function | `Function` | 3 (Function) | function name | first overload signature; `+ n overloads` suffix when overloaded | name |
| Prop | `Prop` | 5 (Field) | prop name | `type = default` or `type (required)` | `name: ` |
| Keyword/literal (`this`, `true`, `false`, `null`, `::`) | `Keyword` | 14 (Keyword) | keyword | — | keyword |

D24-scanned function exports are ordinary *Registered function* items (real
`MethodInfo` signatures — e.g. `string titlecase(string)`) and D23-scanned extension
exports are ordinary *Extension* items — the live registries are their source, nothing
distinguishes them from built-ins.
Items already satisfied are filtered (props already passed in the same call). No
snippets, no `additionalTextEdits`, no commit characters in v1.

### Worked examples (the executable rows of `LanguageServiceCompletionTests`)

Corpus: the [language-reference](../../language-reference.md) blog model —
`Blog { Articles: List<Article>, Title }`, `Article { Title, Summary, Author, Rating }`,
`Author { Name }` — workspace `assemblies` pointing at the corpus model build.

| # | Template (│ = cursor) | Expected |
| --- | --- | --- |
| C01 | `@list(Articles){{ @(│ }}` | `Title`, `Summary`, `Author`, `Rating` (Article members), `this`, functions, keywords — **not** `Articles` (that is Blog's) |
| C02 | `@(::│` | Blog members (`Articles`, `Title`) — root type via `::` |
| C03 | `@(Author.│` inside an Article scope | `Name` |
| C04 | `@card(Article, │` where `<card(style: string = "plain", compact: bool = false)> :: Article` | `style: ` (detail `string = "plain"`), `compact: ` (detail `bool = false`); after `style` is passed, only `compact` |
| C05 | `@│` at top level | definitions in scope (corpus: `card`), all 23 offerable extension names and all 18 default-registry function names per [name sources](#name-sources-the-v1-registry-contents) — the fixture asserts the full sets, spot-readable as `list`, `if`, `ifnot`, `raw`, `elif`, `elseif`, `else`, `partial` / `upper`, `range` |
| C06 | `@(up│` | `upper` (function, detail `string upper(string)`) ranked with Blog members matching `up*` (none) |
| C07 | Abstract `<panel>` called as `@panel(Article)` and `@panel(Menu)` where both types have `Title`, only `Article` has `Rating`; cursor `@(│` in the panel body | `Title` offered; `Rating` **not** offered; hover on `Title` with differing types lists both (`varies by call site`) |
| C08 | Same `<panel>`, zero call sites in the workspace | no member items — only definitions/extensions/functions/keywords |
| C09 | `@if(Count > 0){{ @(│ }}` on Blog | Blog members (step-back — body model is the caller's, not `bool`; the scope map recorded what the compiler threaded) |
| C10 | `:: dynamic` definition body, `@(│` | no member items (dynamic has no static surface) |

## Hover

`MarkupContent` markdown, one fenced `csharp` block for the signature line plus plain
paragraphs:

- Member path segment: ` ```csharp Article.Author : Author``` ` — inside abstract bodies
  with differing per-site types: the fence shows `varies by call site` and a bullet list
  `Article.Title : string`, `Menu.Title : LocalizedString` follows.
- Definition name: the reconstructed header signature; abstract form adds
  `abstract — bound per use site: Article, Menu` (observed call-site types from the
  scope map).
- Prop name: `style: string = "plain"` + `declared on <card>`.
- Function name: all overload signatures, one fence per overload — D24-scanned exports
  render identically (registry-driven, real `MethodInfo` signatures).
- Extension name: `extension 'list'` (+ `aliases: elif, elseif` style note where
  applicable) — D23-scanned exports render identically (registry-driven).

## Go-to-definition

Targets per D16: definition call/name → `Location{ uri: owning file, range: header span }`;
`@<<` import path → the imported file at range `0..0`; `@partial` body target → the
resolved file at `0..0`; prop named-argument name → the `PropDeclaration` span in the
declaring file. Null result → LSP `null` (client shows "no definition found").

Duplicate and override resolution follows
[README D26](README.md#d26--duplicate-definitions-go-to-definition-targets-the-surviving-registry-entry-gap-g7):
a reference targets the **surviving registry entry** at or before it — the newest legal
override layer (`<name:base>`) when layers exist, otherwise the first declaration, even
while a later plain same-name duplicate sits in the buffer drawing the engine's
duplicate error at its own site. Extension and registered-function names (built-in or
D23/D24-scanned) have no template declaration site → `null`.

## DTO subset

Hand-written `sealed record` DTOs in `src/Heddle.LanguageServer/Protocol/`, serialized by
the source-generated `LspJsonContext` (D6): camelCase policy, nulls omitted
(`JsonIgnoreCondition.WhenWritingNull`), unknown incoming properties ignored by default
STJ behavior (deliberately not `Strict`, D6), `AllowDuplicateProperties = false`. This is
the complete v1 set — the subset grows only when a capability ships. Two DTOs required by
the shapes below (`SaveOptions`, `WorkspaceFolder`) join the set; `ClientCapabilities` is
deliberately **not** a record — it is retained as the raw `JsonElement`.

The subset models **exactly the fields the server reads or writes**. Every other
property an LSP 3.17 client may legally send falls under the unknown-member ignore rule —
that is the design, not an omission; the *Dropped optionals* notes name what is
deliberately not materialized. Wire names are the camelCase forms from the
[LSP 3.17 specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/),
against which every field list below was verified (July 2026). Notation: `T?` fields are
optional per the LSP spec and omitted from outgoing payloads when null; unmarked fields
are required.

### Base-shape DTOs

| DTO | Fields (wire name: type) | Notes |
| --- | --- | --- |
| `Position` | `line: int`, `character: int` | Both count UTF-16 code units (D11); zero-based |
| `Range` | `start: Position`, `end: Position` | End is exclusive |
| `Location` | `uri: string`, `range: Range` | |
| `TextDocumentIdentifier` | `uri: string` | |
| `VersionedTextDocumentIdentifier` | `uri: string`, `version: int` | |
| `TextDocumentItem` | `uri: string`, `languageId: string`, `version: int`, `text: string` | `languageId` is stored but not branched on (`heddle` expected) |
| `TextDocumentPositionParams` | `textDocument: TextDocumentIdentifier`, `position: Position` | Shared base of the positional feature params |
| `WorkspaceFolder` | `uri: string`, `name: string` | |

### Lifecycle DTOs

| DTO | Fields | Notes |
| --- | --- | --- |
| `InitializeParams` | `processId: int?`, `rootUri: string?`, `capabilities: JsonElement`, `initializationOptions: JsonElement?`, `workspaceFolders: WorkspaceFolder[]?` | `capabilities` (`ClientCapabilities`) retained opaque. Workspace root = `workspaceFolders[0].uri` when present and non-empty, else `rootUri`; additional folders are ignored with a `window/logMessage` note (one workspace per server process, D14). Dropped optionals: `clientInfo`, `locale`, `rootPath` (deprecated), `trace`, `workDoneToken`. |
| `InitializeResult` | `capabilities: ServerCapabilities`, `serverInfo: ServerInfo` | |
| `ServerInfo` | `name: string` (`"heddle-lsp"`), `version: string` | The informational version the D20 handshake compares |
| `ServerCapabilities` | `textDocumentSync: TextDocumentSyncOptions`, `completionProvider: CompletionOptions`, `hoverProvider: bool`, `definitionProvider: bool`, `semanticTokensProvider: SemanticTokensOptions` | `positionEncoding` deliberately omitted (D5); every unadvertised capability field simply absent |
| `TextDocumentSyncOptions` | `openClose: bool` (`true`), `change: int` (`1`), `save: SaveOptions` (`{}`) | `change` is `TextDocumentSyncKind`: 0 None / 1 Full / 2 Incremental — the server always sends 1. `willSave`/`willSaveWaitUntil` omitted (not advertised) |
| `SaveOptions` | *(no fields modeled)* | Serializes as `{}`; `includeText` omitted = `false` |
| `CompletionOptions` | `triggerCharacters: string[]` (`["@", "(", ".", ":", ","]`) | `resolveProvider`, `allCommitCharacters`, `completionItem.labelDetailsSupport` omitted — items are fully materialized (D7) |
| `SemanticTokensOptions` | `legend: SemanticTokensLegend`, `full: bool` (`true`) | `range` omitted (D7) |
| `SemanticTokensLegend` | `tokenTypes: string[]`, `tokenModifiers: string[]` (`[]`) | The six-type legend of D17, in wire order |

`initialized` carries an empty params object and `exit` carries none — neither
materializes a DTO (the target methods take no arguments). `$/setTrace`'s `{ value }`
and `$/cancelRequest`'s `{ id }` are likewise DTO-less (consumed opaquely / handled
inside StreamJsonRpc).

### Document-sync DTOs

| DTO | Fields | Notes |
| --- | --- | --- |
| `DidOpenTextDocumentParams` | `textDocument: TextDocumentItem` | |
| `DidChangeTextDocumentParams` | `textDocument: VersionedTextDocumentIdentifier`, `contentChanges: TextDocumentContentChangeEvent[]` | Events apply in array order |
| `TextDocumentContentChangeEvent` | `text: string`, `range: Range?`, `rangeLength: int?` | Under the advertised Full sync the full-text variant arrives: `text` = whole document, `range`/`rangeLength` null. Non-null `range` → the contract-violation handling in [Error and cancellation behavior](#error-and-cancellation-behavior) |
| `DidCloseTextDocumentParams` | `textDocument: TextDocumentIdentifier` | |
| `DidSaveTextDocumentParams` | `textDocument: TextDocumentIdentifier` | Dropped optional: `text` (the notification is a no-op — analysis keys on buffer content) |

### Diagnostics DTOs

| DTO | Fields | Notes |
| --- | --- | --- |
| `PublishDiagnosticsParams` | `uri: string`, `version: int?`, `diagnostics: Diagnostic[]` | `version` = the analyzed document version; empty array clears (didClose) |
| `Diagnostic` | `range: Range`, `severity: int`, `code: string?`, `source: string` (`"heddle"`), `message: string` | `severity`: 1 Error / 2 Warning (3 Information / 4 Hint unused — the engine has two severities). `code` = the `HED*` ID as a string; omitted when null (the spec's `integer \| string` union — only the string arm is emitted). Not modeled, never populated in v1 (per [Diagnostics](#diagnostics)): `codeDescription`, `tags`, `relatedInformation`, `data` |

### Completion DTOs

| DTO | Fields | Notes |
| --- | --- | --- |
| `CompletionParams` | `textDocument: TextDocumentIdentifier`, `position: Position`, `context: CompletionContext?` | Dropped optionals: `workDoneToken`, `partialResultToken` (work-done progress and partial results are unsupported across the whole v1 surface) |
| `CompletionContext` | `triggerKind: int`, `triggerCharacter: string?` | `triggerKind`: 1 Invoked / 2 TriggerCharacter / 3 TriggerForIncompleteCompletions. Informational — detection classifies from the token stream ([trigger-character resolution](#trigger-character-resolution)) |
| `CompletionItem` | `label: string`, `kind: int`, `detail: string?`, `insertText: string?` | `kind` per the [item-shapes table](#item-shapes). `insertTextFormat` omitted = 1 (PlainText — no snippets, D12). Not modeled: `documentation`, `sortText`, `filterText`, `textEdit`, `additionalTextEdits`, `commitCharacters`, `command`, `labelDetails`, `data` |

Response shape: `CompletionItem[]` — the array arm of the spec's
`CompletionItem[] | CompletionList | null` union; empty array when the context yields
nothing.

### Hover and definition DTOs

| DTO | Fields | Notes |
| --- | --- | --- |
| `HoverParams` | `textDocument`, `position` | = `TextDocumentPositionParams`; dropped optional: `workDoneToken` |
| `Hover` | `contents: MarkupContent`, `range: Range` | The legacy `MarkedString` arms of the `contents` union are never emitted |
| `MarkupContent` | `kind: string` (always `"markdown"`), `value: string` | |
| `DefinitionParams` | `textDocument`, `position` | Dropped optionals: `workDoneToken`, `partialResultToken`. Response: single `Location` or `null` |

### Semantic-token DTOs

| DTO | Fields | Notes |
| --- | --- | --- |
| `SemanticTokensParams` | `textDocument: TextDocumentIdentifier` | Dropped optionals: `workDoneToken`, `partialResultToken` |
| `SemanticTokens` | `data: int[]` | The delta-encoded quintuples ([semantic tokens](#semantic-tokens)); `resultId` omitted — no delta support (D7) |

### Workspace and window DTOs

| DTO | Fields | Notes |
| --- | --- | --- |
| `DidChangeConfigurationParams` | `settings: JsonElement` | Read per the D18 precedence rules (file wins field-by-field) |
| `LogMessageParams` | `type: int`, `message: string` | `type` (`MessageType`): 1 Error / 2 Warning / 3 Info / 4 Log |
| `ShowMessageParams` | `type: int`, `message: string` | Same `MessageType` values |

## Distribution mechanics

### Tool packaging (`src/Heddle.LanguageServer/Heddle.LanguageServer.csproj`)

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>heddle-lsp</ToolCommandName>
  <PackageId>Heddle.LanguageServer</PackageId>
  <RuntimeIdentifiers>win-x64;win-arm64;linux-x64;linux-arm64;linux-musl-x64;osx-x64;osx-arm64;any</RuntimeIdentifiers>
  <PublishReadyToRun Condition="'$(RuntimeIdentifier)' != '' AND '$(RuntimeIdentifier)' != 'any'">true</PublishReadyToRun>
  <SelfContained>false</SelfContained>
</PropertyGroup>
```

`dotnet pack` on the .NET 10 SDK produces the top-level manifest package + per-RID
packages + the `any` fallback. All variants are framework-dependent → the whole matrix
packs on one CI runner. Documented installs (published on the `docs/editor-support.md`
page WI10 creates):

```
dotnet tool install --global Heddle.LanguageServer --version <x.y.z>
# or, repo-local (recommended for teams):
dotnet new tool-manifest && dotnet tool install Heddle.LanguageServer --version <x.y.z>
```

### VSIX layout

One VSIX per target (`vsce package --target <t>` for `win32-x64`, `win32-arm64`,
`linux-x64`, `linux-arm64`, `alpine-x64`, `darwin-x64`, `darwin-arm64`):

```
extension/
  package.json                  (engines.vscode ^1.91.0; onLanguage:heddle)
  syntaxes/heddle.tmLanguage.json   (build-time copy from docs/coloring-scheme/ — never edited)
  language-configuration.json
  dist/extension.js             (bundled client, vscode-languageclient ^10.1.0)
  server/                       (per-RID framework-dependent R2R publish output —
                                 exactly what `dotnet publish -r <rid>` emits, nothing
                                 hand-curated)
    Heddle.LanguageServer.dll
    Heddle.dll  Heddle.Language.dll  Heddle.LanguageServices.dll
    StreamJsonRpc.dll (+ its transitive dependency closure as restored at build time)
    Heddle.LanguageServer.deps.json
    Heddle.LanguageServer.runtimeconfig.json
```

### Server discovery and launch (client, `extension.ts`)

1. `heddle.server.path` setting — if set: launch it directly (`.dll` → via
   `dotnet exec <path>`, executable → as-is). Misconfigured path → failure UX, no
   fallthrough (an explicit setting must not be silently overridden).
2. Bundled: `context.asAbsolutePath('server/Heddle.LanguageServer.dll')` → launch
   `dotnet exec <dll>`. Note: `dotnet exec` runs a **local file**; it is unrelated to the
   rejected feed-restoring `dotnet tool exec`, which the extension never invokes on any
   path (D19).
3. `heddle-lsp` found on `PATH` → launch it.

Before 1–2 the client verifies the runtime: `dotnet --list-runtimes` contains
`Microsoft.NETCore.App 10.`. Failure UX (no server found, runtime missing, or spawn
failure): one non-modal error notification — `Heddle language server not found. Install
it with: dotnet tool install --global Heddle.LanguageServer --version <x.y.z> — or
install the .NET 10 runtime.` — plus an output-channel entry; no retry loop; the
extension stays active so TextMate highlighting continues (success criterion 5). A
`heddle.restartServer` command re-runs discovery. After a successful handshake the
client compares `serverInfo.version` with its own major version and shows a
`window/showMessage`-style warning on mismatch (D20).

## Contract-test fixtures

`src/Heddle.LanguageServices.Tests/Fixtures/messages/` — one folder per scenario, files
`NN-client.json` / `NN-server.json` in exchange order. `ProtocolContractTests` drives
the real server class over `FullDuplexStream.CreatePair()` (in-proc, no process, no
editor), sends each client message, and asserts the server's messages equal the fixtures
after JSON canonicalization (property-order- and whitespace-insensitive; volatile fields
`serverInfo.version` and `PublishDiagnosticsParams.version` are matched by placeholder).
Fixture set:

| Scenario | Pins |
| --- | --- |
| `initialize/` | The capability advertisement above, verbatim |
| `diagnostics-basic/` | didOpen (member typo) → publishDiagnostics with `code: "HED0001"`, exact range |
| `diagnostics-clear/` | didClose → empty diagnostics array |
| `semantic-tokens/` | The encoding walkthrough example, literal `data` array |
| `completion-members/` | C01 — typed member items inside `@list` body |
| `completion-props/` | C04 — prop items with detail strings |
| `completion-exported-function/` | D24 — the scanned `titlecase` export offered with its real signature detail (`string titlecase(string)`); a didOpen with its call publishes **no** `HED1001` |
| `completion-scanned-extension/` | D23 — the scanned `badge` export offered among extension items |
| `diagnostics-imported/` | D25 — didOpen of an importer whose `@<<` target carries a member typo → publishDiagnostics with the zero-width range at the import site, `imported '<path>':` message prefix, original `code: "HED0001"` preserved |
| `hover-member/` | Member hover markdown |
| `definition-import/` | Cross-file definition Location |
| `cancel/` | `$/cancelRequest` → `-32800` reply |
| `shutdown/` | shutdown → null; exit code path asserted separately in `ServerLifecycleTests` |

Fixtures are recorded once from the running implementation, then reviewed and frozen —
a fixture change is a spec change (golden policy per the
[testing standards](../common/testing-standards.md#fixtures-and-goldens)).
