# Phase 9 WASM demo supplement — host projects, worker protocol, page binding, deploy

Supplement to the [phase 9 specification](README.md). This document pins the typed
in-browser demo end-to-end for the implementer: the two new project shapes, the
trimming/feature-switch wiring, the `[JSExport]` surface and its logic/shell split, the
worker `postMessage` protocol with worked message examples, the Ace feature binding, the
render loop, imports-in-the-browser behavior, the docs-workflow changes, staging and
local development, and the Playwright smoke scenarios. Decision rationale lives in the
entry document (D1–D10, D14, D16); this file is the build reference.

## Host projects

### `src/Heddle.Demo.Models/` — the compiled-in corpus

Plain `net10.0` class library. Contents:

- The published blog model, **exactly** as [language-reference.md](../../language-reference.md)
  defines it (the docs and the demo must never drift):

```csharp
namespace Heddle.Demo.Models
{
    public class Blog    { public string Title { get; set; } public int Year { get; set; } public ICollection<Article> Articles { get; set; } }
    public class Article { public string Title { get; set; } public Author Author { get; set; } public DateTime PublishedOn { get; set; }
                           public bool IsFeatured { get; set; } public IList<string> Tags { get; set; } public ICollection<Comment> Comments { get; set; } }
    public class Author  { public string Name { get; set; } public string Email { get; set; } }
    public class Comment { public string Author { get; set; } public string Text { get; set; } public ICollection<Comment> Replies { get; set; } }
}
```

- `DemoCatalog` — the model-set registry the page's picker binds to. Every instance is
  deterministic (fixed dates, fixed ordering — the smoke render golden depends on it):

```csharp
namespace Heddle.Demo.Models
{
    /// <summary>One selectable demo model set: a root type, a canned instance,
    /// and the starter template shown when the set is selected.</summary>
    public sealed class DemoModelSet
    {
        public string Id { get; }              // "blog", "article-card"
        public string DisplayName { get; }     // shown in the picker
        public Type RootType { get; }          // typed root for @model + render
        public object CreateInstance();        // deterministic canned data
        public string StarterTemplate { get; } // includes the @model(){{...}} directive
    }

    public static class DemoCatalog
    {
        public static IReadOnlyList<DemoModelSet> Sets { get; }   // v1: "blog", "article-card"
        public static DemoModelSet Get(string id);                 // throws on unknown id (host bug)
    }
}
```

Two sets ship in v1: `blog` (root `Blog`, the full corpus — the default) and
`article-card` (root `Article`, a single article — the component-flavored starter that
pairs with a `<card>` definition in its starter template). Starter templates carry their
own `@model(){{Blog}}` / `@model(){{Article}}` directive — that is how the analysis and
the render agree on the root type with **no facade change**: the engine's `model`
extension pins the scope type, and the compiled-in assembly is name-resolvable because
the host registered it (below).

### `src/Heddle.Demo.Wasm/` — the browser host

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>          <!-- required by the JS interop generator -->
    <InvariantGlobalization>true</InvariantGlobalization> <!-- ≈ −30 % bundle; matches phase 1 invariant defaults -->
    <PublishTrimmed>true</PublishTrimmed>                 <!-- wasm default, stated explicitly -->
  </PropertyGroup>

  <ItemGroup>
    <!-- D4: disable the C# tier and let the trimmer drop the Roslyn graph -->
    <RuntimeHostConfigurationOption Include="Heddle.CSharpTierEnabled" Value="false" Trim="true" />
    <!-- D5: reflection roots -->
    <TrimmerRootAssembly Include="Heddle.Demo.Models" />
    <TrimmerRootDescriptor Include="linker\Heddle.Demo.roots.xml" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Heddle.LanguageServices\Heddle.LanguageServices.csproj" />
    <ProjectReference Include="..\Heddle.Demo.Models\Heddle.Demo.Models.csproj" />
  </ItemGroup>
</Project>
```

`linker/Heddle.Demo.roots.xml` roots the engine members reached only by reflection —
the `[ExtensionName]` extension types (instantiated via `TemplateFactory`) and the
`FunctionRegistry` default-method holder type(s):

```xml
<linker>
  <assembly fullname="Heddle">
    <type fullname="Heddle.Extensions.*" />                       <!-- Activator-created extensions -->
    <type fullname="Heddle.Runtime.Expressions.DefaultFunctions" /><!-- registry MethodInfos (phase 1 naming) -->
  </assembly>
</linker>
```

The descriptor deliberately roots **nothing** in `Heddle.Runtime.ContextCompilation` —
the D4 substitution must be free to eliminate the guarded Roslyn branch, and the D9
purity gate asserts the result (`_framework` contains no `Microsoft.CodeAnalysis*`).
The boot path of the smoke suite (S2–S4) is the completeness check for this root set: an
over-trimmed member fails a fixture visibly, and the fix is a descriptor line, reviewed.

### The logic/shell split (what `DemoContractTests` runs)

All behavior lives in `DemoHost` — plain `net10.0` code with **no** browser dependency,
compiled into the WASM project and exercised on CoreCLR by
`src/Heddle.LanguageServices.Tests/DemoContractTests.cs` (README D14). The
`[JSExport]` shell contains marshalling only:

```csharp
namespace Heddle.Demo.Wasm
{
    /// <summary>All demo behavior; single-threaded by contract (one worker).</summary>
    internal sealed class DemoHost
    {
        public DemoHost()
        {
            HeddleTemplate.Configure(typeof(Blog).Assembly);        // model name-resolution seam
            _service = new HeddleLanguageService(new HeddleLanguageServiceOptions
            {
                AssemblyPaths = Array.Empty<string>(),              // compiled-in models; no ALC
                RootPath = "/",                                     // imports resolve nowhere useful (below)
                OutputProfile = OutputProfile.Html,
                ExpressionMode = ExpressionMode.Native,
                FileNamePostfix = string.Empty
            });
        }

        public InitResult Init();                                    // catalog + engine version
        public AnalyzeResult Analyze(string path, string text, int version);
        public CompleteResult Complete(string path, int offset);
        public HoverResult Hover(string path, int offset);           // null payload = no hover
        public RenderResult Render(string path, string modelId);     // renders the last analyzed text
    }

    [SupportedOSPlatform("browser")]
    public static partial class DemoInterop                          // the shell: JSON in, JSON out
    {
        [JSExport] internal static string Init();
        [JSExport] internal static string Analyze(string path, string text, int version);
        [JSExport] internal static string Complete(string path, int offset);
        [JSExport] internal static string Hover(string path, int offset);
        [JSExport] internal static string Render(string path, string modelId);
    }
}
```

`Render` compiles the **last analyzed text** for `path` with a render-tuned context
(`ProvideLanguageFeatures` off) via `new HeddleTemplate(text, new CompileContext(renderOptions))`,
calls `Generate(DemoCatalog.Get(modelId).CreateInstance())`, disposes the template, and
returns the HTML — or a caught exception message as `{ error }` (README D8: the page
never crashes on a render fault; a `:: dynamic` template hitting trimmed runtime-binder
paths is the documented example). All result DTOs serialize through the
source-generated `DemoJsonContext` (`[JsonSerializable]` per type; camelCase; nulls
omitted) — no reflection serialization under trimming.

## The worker protocol

Facade-shaped (absolute UTF-16 offsets, facade vocabulary), JSON over `postMessage`.
Envelope: requests `{ id, cmd, ...args }`, responses `{ id, ok: true, result }` or
`{ id, ok: false, error: { message } }`, worker-initiated events `{ evt, ... }`.

TypeScript shapes (normative for the page and the worker; the .NET DTOs mirror them):

```ts
type Request =
  | { id: number; cmd: "init" }
  | { id: number; cmd: "analyze";  path: string; text: string; version: number }
  | { id: number; cmd: "complete"; path: string; offset: number }
  | { id: number; cmd: "hover";    path: string; offset: number }
  | { id: number; cmd: "render";   path: string; modelId: string };

type Diagnostic = { id: string | null; message: string; fix: string | null;
                    severity: "error" | "warning"; offset: number; length: number };

type Response =
  | { id: number; ok: true;  result: InitResult | AnalyzeResult | CompleteResult | HoverResult | RenderResult }
  | { id: number; ok: false; error: { message: string } };

type InitResult     = { engineVersion: string;
                        models: { id: string; displayName: string; starterTemplate: string }[] };
type AnalyzeResult  = { diagnostics: Diagnostic[]; csharpTierUsed: boolean };
type CompleteResult = { items: { label: string; kind: string; detail: string | null; insertText: string }[] };
type HoverResult    = { markdown: string; offset: number; length: number } | null;
type RenderResult   = { html: string } | { error: string };

type Event = { evt: "ready" } | { evt: "boot-failed"; error: string };
```

Worked exchange (the C01 cross-host fixture, also asserted by smoke S3 — cursor after
`@(` inside the `@list(Articles)` body). The item set is the facade's expression-position
rule (phase 6 [protocol.md, detection rule 5](../phase-6-tooling-lsp/protocol.md#context-detection)):
the scope type's visible members, then the keyword/literal items, then one item per
registered function. The member and keyword items are exhaustive below; the function
items — one per overload group in the live default `FunctionRegistry` (phase 1's frozen
set plus phase 4's `range`) — follow the C06 item shape (`kind: "function"`, `detail` =
first overload signature with a `+ n overloads` suffix when overloaded), and the frozen
fixture file `complete-c01.json` pins the exact recorded-and-reviewed list, since this
spec must not restate phase 1's registry table:

```ts
// page → worker
{ "id": 7, "cmd": "complete", "path": "demo.heddle", "offset": 64 }
// worker → page
{ "id": 7, "ok": true, "result": { "items": [
  // Article members (the scope type inside @list(Articles) — not Blog's members):
  { "label": "Title",       "kind": "property", "detail": "string",               "insertText": "Title" },
  { "label": "Author",      "kind": "property", "detail": "Author",               "insertText": "Author" },
  { "label": "PublishedOn", "kind": "property", "detail": "DateTime",             "insertText": "PublishedOn" },
  { "label": "IsFeatured",  "kind": "property", "detail": "bool",                 "insertText": "IsFeatured" },
  { "label": "Tags",        "kind": "property", "detail": "IList<string>",        "insertText": "Tags" },
  { "label": "Comments",    "kind": "property", "detail": "ICollection<Comment>", "insertText": "Comments" },
  // keyword/literal items (the full fixed set of rule 5):
  { "label": "this",        "kind": "keyword",  "detail": null,                   "insertText": "this" },
  { "label": "true",        "kind": "keyword",  "detail": null,                   "insertText": "true" },
  { "label": "false",       "kind": "keyword",  "detail": null,                   "insertText": "false" },
  { "label": "null",        "kind": "keyword",  "detail": null,                   "insertText": "null" },
  { "label": "::",          "kind": "keyword",  "detail": null,                   "insertText": "::" },
  // registered functions, e.g. (shapes normative, list pinned by the frozen fixture):
  { "label": "upper",       "kind": "function", "detail": "string upper(string)", "insertText": "upper" },
  { "label": "range",       "kind": "function", "detail": "IEnumerable<int> range(int, int) + 1 overload", "insertText": "range" }
] } }
```

The fixture's assertion mode matches the facade tests: the member and keyword items are
matched exactly (label + kind + detail + insertText, order-insensitive), and the
function-item set is matched against the full recorded list in the frozen fixture — a
registry change that alters completion output is a reviewed fixture change on both
hosts at once, which is precisely the drift detection D14 exists for.

Rules:

- `analyze` supersedes: the worker tracks the latest `version` per `path`; a response
  for a stale version is still sent (the page drops it by version check) — no
  cancellation plumbing in v1, the debounce bounds waste (README D8).
- `complete`/`hover`/`render` answer against the **latest completed analysis**; if the
  text changed since, the page re-`analyze`s first (the page owns freshness — mirror of
  the phase 6 server's request-forced analysis, simplified for one document).
- One document in v1: `path` is always `"demo.heddle"`; the protocol carries it anyway
  so the shape needs no change for a future multi-pane demo.
- Contract fixtures (`src/Heddle.Demo.Wasm/contract-fixtures/*.json`) are
  request/expected-result pairs in exactly these shapes (README D14): `complete-c01`,
  `complete-c02-root`, `complete-c04-props`, `complete-c09-stepback`,
  `diagnostic-member-typo` (expects `HED0001` + exact offset/length), `render-blog`
  (expects the golden HTML fragment). Fixtures are recorded once, reviewed, frozen —
  fixture changes are spec changes.

### `wwwroot/heddle-demo-worker.js`

The thin dispatcher — the Microsoft worker pattern verbatim, no logic:

```ts
import { dotnet } from './_framework/dotnet.js';

let exports = null;
try {
  const { getAssemblyExports, getConfig } = await dotnet.create();
  exports = await getAssemblyExports(getConfig().mainAssemblyName);
  self.postMessage({ evt: 'ready' });
} catch (err) {
  self.postMessage({ evt: 'boot-failed', error: String(err?.message ?? err) });
}

const api = {
  init:     ()  => exports.Heddle.Demo.Wasm.DemoInterop.Init(),
  analyze:  (m) => exports.Heddle.Demo.Wasm.DemoInterop.Analyze(m.path, m.text, m.version),
  complete: (m) => exports.Heddle.Demo.Wasm.DemoInterop.Complete(m.path, m.offset),
  hover:    (m) => exports.Heddle.Demo.Wasm.DemoInterop.Hover(m.path, m.offset),
  render:   (m) => exports.Heddle.Demo.Wasm.DemoInterop.Render(m.path, m.modelId)
};

self.addEventListener('message', (e) => {
  const { id, cmd } = e.data;
  try {
    if (!exports) throw new Error('worker not booted');
    self.postMessage({ id, ok: true, result: JSON.parse(api[cmd](e.data)) });
  } catch (err) {
    self.postMessage({ id, ok: false, error: { message: String(err?.message ?? err) } });
  }
});
```

## Page integration (`docs/public/demo.html`)

The page script grows four modules (inline, as today — the page stays self-contained):

1. **Worker bridge.** After the base page is interactive (`requestIdleCallback`,
   `load`-event fallback), `new Worker('demo/heddle-demo-worker.js', { type: 'module' })`
   inside `try/catch`; a 30 s boot timeout, `boot-failed`, worker `error`, or
   constructor throw all resolve to **fallback mode** — the flag is simply never set,
   and layers 2–3 never attach. Request correlation by `id`; stale responses dropped by
   version.
2. **Offset boundary.** `session.doc.positionToIndex(pos)` /
   `indexToPosition(offset)` — the same Ace APIs the mode worker already uses — convert
   between Ace rows/cols and facade offsets at the bridge and nowhere else (README D6).
3. **Feature attach (on `ready` + successful `init`).**
   - Completer: `langTools.setCompleters([demoCompleter])` on the editor — the dynamic
     completer calls `complete` and maps items (`label`→`caption`, `insertText`→`value`,
     `detail`→`meta`; `getDocTooltip` renders `detail` + label as the item doc). The
     static word-list completer is thereby out of the active set, restored only in
     fallback mode (README D7).
   - Hover: `new HoverTooltip(editor)` (from `ace/tooltip` in the bundle) whose provider
     calls `hover` and shows the markdown as text with the signature line first;
     the tooltip range comes from the result's `offset`/`length`.
   - Diagnostics: on each `analyze` result, map diagnostics to Ace annotations
     (`{ row, column, text: "[HED1003] message — Fix: …", type }`), merge with the last
     base-worker `annotate` payload (listener on the session's worker events),
     de-duplicate by `(row, column, text)`, `session.setAnnotations(merged)`.
   - Status pill: `Syntax` → `Typed` on attach → `Typed + Render` after the first
     successful render. The header note text switches with the state; the "does NOT
     render" copy exists only in the fallback state, reworded to "syntax checking only —
     rendering requires WebAssembly".
   - Model picker: populated from `init`'s catalog; switching sets the starter template
     (with confirmation if the buffer was edited) and the render `modelId`.
4. **Render pane.** A vertical split (editor | output). On each clean analysis (zero
   errors; warnings allowed) after the 300 ms debounce: `render` → set the sandboxed
   iframe's `srcdoc` (`<iframe sandbox title="Rendered output">` — no scripts, opaque
   origin). `{ error }` results and C#-tier declines show the pane's note bar instead
   (README D8); the previous successful render stays visible underneath it.

## Imports in the browser

No virtual-file-system seeding ships in v1 (entry doc, Deferred items). `RootPath` is
`"/"`; an `@<<{{path}}` or `@partial` target therefore fails with the engine's normal
positioned file-not-found compile diagnostic, surfaced like any other error — the page
adds no special case, and the curated starter templates use none of these constructs.
The revisit trigger is a curated multi-file demo scenario; the design space (seed the
Emscripten VFS from fetched assets before boot) is noted for that day and deliberately
unbuilt now.

## Docs workflow changes

The complete change to [.github/workflows/docs.yml](../../../.github/workflows/docs.yml)
(README D9). Trigger block:

```yaml
on:
  push:
    branches: [ "main" ]
    paths:
      - 'docs/**'
      - '.github/workflows/docs.yml'
      # The demo WASM bundle embeds the engine + facade (build_ace.sh and js/** are
      # covered by the whole-project globs below, which supersede the old entries):
      - 'src/Heddle/**'
      - 'src/Heddle.Language/**'
      - 'src/Heddle.LanguageServices/**'
      - 'src/Heddle.Demo.Models/**'
      - 'src/Heddle.Demo.Wasm/**'
  pull_request:
    branches: [ "main" ]
    paths:                          # identical list, repeated verbatim — GitHub Actions
      - 'docs/**'                   # path filters are per-trigger; keep the two lists
      - '.github/workflows/docs.yml'   # in sync when either changes
      - 'src/Heddle/**'
      - 'src/Heddle.Language/**'
      - 'src/Heddle.LanguageServices/**'
      - 'src/Heddle.Demo.Models/**'
      - 'src/Heddle.Demo.Wasm/**'
  workflow_dispatch:
```

Build-job steps, inserted after "Stage Ace bundle into docs/public" and before
"Install dependencies":

```yaml
    - name: Setup .NET
      uses: actions/setup-dotnet@v5
      with:
        dotnet-version: 10.0.x
    - name: Install wasm-tools workload
      run: dotnet workload install wasm-tools
    - name: Publish demo WASM host
      run: dotnet publish src/Heddle.Demo.Wasm -c Release
    - name: Stage demo bundle into docs/public
      run: |
        mkdir -p docs/public/demo
        cp -R src/Heddle.Demo.Wasm/bin/Release/net10.0/publish/wwwroot/. docs/public/demo/
    - name: Demo bundle budget and purity gate
      run: |
        size=$(du -sb docs/public/demo | cut -f1)
        echo "demo bundle: $size bytes"
        test "$size" -le 12582912 || { echo "::error::demo bundle exceeds 12 MB budget"; exit 1; }
        if find docs/public/demo -name 'Microsoft.CodeAnalysis*' | grep -q .; then
          echo "::error::Roslyn assets found in the demo bundle (D4 regression)"; exit 1
        fi
```

After "Build site", before "Configure Pages":

```yaml
    - name: Install Playwright chromium
      working-directory: docs
      run: npx playwright install chromium --with-deps
    - name: Demo smoke tests
      working-directory: docs
      run: npm run demo:smoke
```

And the deploy job is gated so PRs build + smoke without deploying:

```yaml
  deploy:
    needs: build
    if: github.event_name != 'pull_request'
```

`docs/package.json` gains dev-dependency `@playwright/test` and the script
`"demo:smoke": "playwright test demo-smoke"` (the Playwright config starts
`npm run docs:preview` as its `webServer` and targets its served origin with the
`/Heddle/` base).

## Staging and local development

- `/docs/public/demo/` joins `.gitignore` — the bundle is workflow-staged, never
  committed (the verified `docs/public/ace/` posture).
- `src/Heddle.Demo.Wasm/stage_demo.sh` = the two workflow staging commands (publish +
  copy) for local use; after running it, `npm run docs:dev` serves the typed demo.
- Without staging, the page runs in fallback mode — local docs work never requires the
  .NET WASM toolchain (README D9: the fallback doubles as the local-dev mode).

## Smoke scenarios

`docs/demo-smoke/demo.spec.ts` (Playwright, chromium project only, headless CI
default). All scenarios navigate to `/Heddle/demo.html` on the preview server. The
typed scenarios wait on the status pill (bounded by Playwright's default timeouts —
that bound *is* the demo's latency requirement, README Performance).

| # | Setup | Script | Assertions |
| --- | --- | --- | --- |
| S1 `fallback-wasm-blocked` | `page.route('**/demo/**', r => r.abort())` before navigation | Load; type into the editor; delete a `)` from the starter template | Editor visible; pill shows `Syntax`; an `ace_error` annotation appears; **no** console page-error — today's behavior byte-for-byte (the standing fallback regression) |
| S2 `typed-diagnostics` | default | Wait for pill `Typed`; replace a member with `Titlle` inside `@(...)` | An annotation whose text contains a `HED` code appears at the typo's line |
| S3 `typed-completion` | default | Place the cursor after `@(` inside the `@list(Articles){{ … }}` body; trigger autocomplete | The popup contains `Title` and `Author` with `meta` showing CLR types — the C01 fixture, browser side (README D14) |
| S4 `render-roundtrip` | default | Load; wait for pill `Typed + Render` | The output iframe `srcdoc` contains the pinned golden fragment for the `blog` starter (an exact element + encoded-text substring, stored beside the spec file) |
| S5 `csharp-declined` | default | Paste a template using a C#-tier construct | A positioned annotation with a phase 1 `HED1xxx` code; the pane note contains "not available in the browser demo"; no page error |

The **seeded bundle break** (roadmap success criterion 4) is procedural, once per
harness change: on a scratch branch, truncate the staged `dotnet.js` after the staging
step (one added workflow line), observe S2–S5 red and S1 green, revert, record the run
link in the PR. This proves the suite fails when the typed layer breaks *and* that the
fallback assertion holds independently — the harness's own test.
