# Writing Custom Extensions

Extensions are how you add new `@yourhelper(...)` directives to the language. Every built‑in
verb (`if`, `list`, `date`, …) is just an extension; yours work exactly the same way. This
page covers the contract, the data flow, the attributes, and registration.

The built‑ins in [src/Heddle/Extensions](../src/Heddle/Extensions) are the best worked
examples — `IfExtension`, `ListExtension`, and `DateExtension` are referenced throughout.

---

## The contract

An extension implements [`IExtension`](../src/Heddle/Runtime/IExtension.cs), but you will
almost always derive from one of the base classes instead:

- [`AbstractExtension`](../src/Heddle/Core/AbstractExtension.cs) — the standard base. Emits
  output as‑is.
- [`AbstractHtmlExtension`](../src/Heddle/Core/AbstractHtmlExtension.cs) — adds opt‑in HTML
  encoding; override `ProcessDataInternal` / `RenderDataInternal` instead of the plain
  methods.

The interface:

```csharp
public interface IExtension : IDisposable
{
    void   SetUpRenderType(RenderType renderType);
    ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent);
    void   CompleteInit(CompileScope newScope, ParseContext parseContext);
    object ProcessData(in Scope scope);   // build & return a string result
    void   RenderData(in Scope scope);    // stream output directly to scope.Renderer
    BlockPosition Position { get; set; }
}
```

### Lifecycle

1. **`InitStart` (compile time)** — called once while compiling. You receive the incoming
   `dataType` (the model type at this point), the `chainedType`, and the `parent` type, and
   you **return the output type** your extension produces. This is where the engine threads
   types through a chain, so returning the right `ExType` matters. Call `base.InitStart(...)`
   to let the base compile your `{{ … }}` subtemplate (it stores it for later rendering).
2. **`CompleteInit` (compile time, optional)** — for extensions that need a second pass
   (e.g. [`PartialExtension`](../src/Heddle/Extensions/PartialExtension.cs) compiles a
   referenced template here, after the main pass).
3. **`ProcessData` / `RenderData` (render time)** — called per render. Implement **both**:
   `RenderData` streams to `scope.Renderer` (the fast path), `ProcessData` returns a string
   (used when a parent needs your result as a value, e.g. inside a chain). `ProcessData`'s
   contract is a `string`: return the textual value, or `string.Empty` when there is no textual
   value here (e.g. a render‑only extension). The value/string rail coerces any non‑string
   result to empty output (`as string ?? string.Empty`) — a deliberate guard against a stray
   object's default `ToString()` leaking into concatenated output, but it also silently drops an
   otherwise‑meaningful boxed scalar returned instead of a string. Stringify at your own boundary
   (as `@int`/`@string`/`@guid` do) — do not rely on the rail to convert a non‑string value for you.

> Implement both `ProcessData` and `RenderData` with equivalent behavior. The engine chooses
> between them depending on context (direct rendering vs. value composition). `ProcessData` must
> return a `string` (or `string.Empty`) — a non‑string result is silently coerced to empty output,
> not stringified for you.

> **Directive‑style extensions and `TrimDirectiveLines`.** An extension whose `InitStart`
> returns `null` produces no output element and the compiler removes its block from the
> document (the `@using`/`@model`/`@profile` pattern). Such blocks automatically participate in
> [directive‑line trimming](language-reference.md#whitespace-trimming-): when
> `TemplateOptions.TrimDirectiveLines` is on and the block occupies its line by itself, the
> whole line is swallowed. You get this for free — trimming keys on the removal mechanism, not
> on a name list, so there is nothing extra to implement.

### Helpers from the base class

`AbstractExtension` gives you:

- `GetInnerResult(in Scope scope)` → renders your subtemplate body to a string.
- `RenderInnerResult(in Scope scope)` → streams your subtemplate body to the renderer.
- `InnerExist` → whether a `{{ … }}` body was provided.

---

## The `Scope`

At render time you read data from [`Scope`](../src/Heddle/Data/Scope.cs) and write via its
`Renderer`. The relevant fields:

| Field | Meaning |
| --- | --- |
| `ModelData` | The current model (your parameter value). |
| `ChainedData` | The chained value — the output of the call to your **right** in a chain (chains run right‑to‑left), and the loop index for iteration extensions. |
| `ParentModelData` | The enclosing scope's model. |
| `CallerData` | Caller context. |
| `Renderer` | The output sink (`Renderer.Render(string)`). Under the string, `TextWriter`, and UTF‑8 sinks alike — write through `Render(string)` and it just works. |

`Scope` is a readonly struct with pure transforms used to build the scope for your subtemplate:

- `scope.Parent()` / `scope.Parent(chained)` — step back to the **parent** (caller) model,
  optionally setting a new chained value. This is the "one step back": rendering your body
  against `scope.Parent()` makes the body see the surrounding model instead of your parameter —
  exactly what the conditionals and formatters do (e.g. so `@if(flag){{ @(Title) }}` still sees
  the caller's model). See [Language Reference → stepping back](language-reference.md#stepping-back-the-parent-context).
- `scope.Model(model)` / `scope.Model(model, chained)` — descend into a child model.
- `scope.Chain(chained)` — set the chained value while keeping the model.

For example, `ListExtension` iterates and renders its body once per element with the index as
the chained value:

```csharp
var index = 0;
foreach (var item in (IEnumerable)scope.ModelData)
{
    var itemScope = scope.Model(item, index);   // model = element, chained = index
    RenderInnerResult(itemScope);
    index++;
}
```

### Writing to the sink: spans and values

`scope.Renderer.Render(string)` is all most extensions ever need, and it works unchanged against
every sink (string, `TextWriter`, and the UTF‑8 `IBufferWriter<byte>`). When you already have a
`ReadOnlySpan<char>` or a formattable value, opt into the allocation‑free helpers in
[`ScopeRendererExtensions`](../src/Heddle/Data/ScopeRendererExtensions.cs):

```csharp
using Heddle.Data;

// Span write — dispatches to the sink's native span path when available, else materializes a string.
scope.Renderer.Render(mySpan);

// Value format — no intermediate string on the span/UTF-8 tiers (net6+):
//   IUtf8SpanFormattable straight to bytes on a UTF-8 sink (net8+), else ISpanFormattable into a
//   stackalloc char span, else ToString(format, provider). Identical characters on every tier.
scope.Renderer.Render(count, "N0", CultureInfo.InvariantCulture);   // where count : struct, ISpanFormattable
```

The capability interfaces behind this are additive and opt‑in — you never have to implement them:

| Interface | Adds | Implemented by |
| --- | --- | --- |
| `IScopeRenderer` | `Render(string)` | every renderer (unchanged) |
| `ISpanScopeRenderer : IScopeRenderer` | `Render(ReadOnlySpan<char>)` | the sink adapters and `HtmlEncodedRenderer` |
| `IUtf8ScopeRenderer : ISpanScopeRenderer` | `RenderUtf8(ReadOnlySpan<byte>)` | the UTF‑8 sink only |

`HtmlEncodedRenderer` is deliberately **not** an `IUtf8ScopeRenderer`: pre‑encoded bytes must never
skip an active encode proxy, so under `@html` a value routes through the string bridge (encode,
then transcode) and is single‑encoded by construction.

> **Never cache the renderer.** `scope.Renderer` is a **per‑render** artifact — the string path
> builds a fresh one each call, and the sink overloads construct a new adapter per render. Read it
> from the `Scope` you were handed; never store it in a field. Extension instances are shared across
> concurrent renders and must stay stateless (all per‑render state lives in the `Scope` lineage).

---

## The local context channel

Sibling extensions can coordinate declaratively through a small per‑body **local context frame**
reached from `Scope`:

```csharp
scope.Publish(string key, object value);          // last write wins within the frame
bool scope.TryRead(string key, out object value); // false (never throws) when absent
```

This is the general mechanism behind the built‑in [branch sets](built-in-extensions.md#branch-sets):
`@if`/`@ifnot`/`@elif` publish a `BranchState` and `@else` reads and clears it. Use it for any
"publish in document order, read by a later sibling" pattern — zebra striping, tab sets,
first‑match‑wins pickers.

**Rules of the road:**

- **Declare participation with `[ScopeChannel]`.** A body is provisioned with a frame at compile
  time **iff** its document statically contains a `[ScopeChannel]` extension. Mark every extension
  that calls `Publish`/`TryRead` with `[ScopeChannel]` (it is inherited by subclasses). Forget it
  and `Publish` throws `InvalidOperationException`; `TryRead` returns `false`. This keeps templates
  that use no channel allocation‑identical — they never provision a frame.
- **Keys are ordinal, case‑sensitive strings; `null` values are allowed.** The prefix `heddle.` is
  reserved for the engine — only `BranchState.ReservedKey` (`"heddle.branch"`) may be published
  under it, and only with a `BranchState` value. Namespace your own keys (e.g. `"myapp.row"`).
- **Frames never cross a body boundary.** Each `@list`/`@for` iteration, nested body, `@partial`,
  and definition invocation starts a fresh frame; a body never sees its parent's. Coordination is
  strictly across **siblings** of one body execution (like CSS counters), never parent→child.
- **Thread‑safety.** A frame belongs to one body execution of one render invocation on one thread,
  so `Publish`/`TryRead` are unsynchronized by design. As always, never store a `Scope` on your
  extension instance or use it after the call that received it — extension instances are shared
  across concurrent renders.
- **Bodies execute only through the funnel.** Any extension that runs a subtemplate body does so
  via the protected `GetInnerResult(in Scope)` / `RenderInnerResult(in Scope)` — the single seam
  that installs the body's frame. Custom extensions inherit this for free.

### Example — a publisher/consumer pair (zebra striping)

One `[ScopeChannel]` extension both reads the previous row parity and publishes the next, so a run
of siblings alternates:

```csharp
[ExtensionName("zebra")]
[ScopeChannel]
public class ZebraExtension : AbstractExtension
{
    private const string Key = "myapp.zebra.row";

    public override object ProcessData(in Scope scope) => Next(scope);
    public override void RenderData(in Scope scope) => scope.Renderer.Render(Next(scope));

    private static string Next(in Scope scope)
    {
        bool odd = scope.TryRead(Key, out var value) && value is bool b && b;
        scope.Publish(Key, !odd);       // flip for the next sibling
        return odd ? "odd" : "even";
    }
}
```

`@zebra()@zebra()@zebra()` renders `evenoddeven`; inside a `@list` body each row starts fresh.

### Example — a `BranchState` participant that drives a set

`BranchState` and its reserved key are public, so a custom matcher can **satisfy** a branch set —
publishing `new BranchState(true)` makes a following `@else` render nothing:

```csharp
[ExtensionName("satisfy")]
[ScopeChannel]
public class SatisfyExtension : AbstractExtension
{
    public override object ProcessData(in Scope scope)
    {
        scope.Publish(BranchState.ReservedKey, new BranchState(true));
        return string.Empty;
    }

    public override void RenderData(in Scope scope)
    {
        scope.Publish(BranchState.ReservedKey, new BranchState(true));
    }
}
```

`@satisfy()@else(){{ fallback }}` renders nothing — the set is already satisfied. A matcher could
just as well read the state with `scope.TryRead(BranchState.ReservedKey, out var v)` to render
alongside a set.

### Building your own branch set

The built‑in `@if`/`@ifnot`/`@elif`/`@else` family is not special‑cased in the engine — each is an
ordinary extension that declares its **position in a branch set** with `[BranchRole]`. Attach the
same attribute to your own extensions and a complete `@begin`/`@between`/`@finish` set gets identical
set semantics: adjacency stripping, orphan diagnostics, the terminal‑optional rule, and locals‑frame
provisioning — with no engine changes.

There are three roles:

| Role | Built‑ins | Position in the set |
| --- | --- | --- |
| `BranchRole.Opener` | `@if`, `@ifnot` | **First.** Opens a fresh set and publishes the initial `BranchState`. Needs no predecessor; never diagnosed. |
| `BranchRole.Continuation` | `@elif`/`@elseif` | **Middle.** Requires a preceding opener or continuation in the same scope; may re‑publish an updated `BranchState`. Never first, never last. |
| `BranchRole.Terminal` | `@else` | **Last (optional).** Closes the set and clears its state. Takes no condition. |

**The contract the roles enforce** (the same rules the built‑ins already obey):

- **Opener** starts a fresh set and is never diagnosed (R1). It publishes *opportunistically* — see
  the `[ScopeChannel]` note below.
- **Continuation** requires a preceding opener/continuation. An orphaned continuation warns
  (**HED3002**) and then behaves as an opener (R2). It may change the published value entirely inside
  its own body (R3).
- **Terminal** closes the set (R4). An orphaned terminal is a compile **error** (**HED3003**) where
  statically visible, and the extension's own render‑time exception otherwise. A terminal is
  **optional** — a set may validly end on an opener or continuation (R5) — and takes **no condition**;
  a non‑empty parameter warns (**HED3004**) and is evaluated then ignored (R6).
- Whitespace‑only text between the blocks of one set is stripped silently; non‑whitespace draws
  **HED3001** (R7).
- A definition may **shadow** a branch name — a shadowed leftmost call is never a branch (R8). A
  non‑branch block between siblings ends stripping adjacency but leaves the open set intact, so a
  following terminal still binds (R9). A roleless `[ScopeChannel]` participant (like `@satisfy`
  above) suppresses orphan diagnostics for the rest of the set (R10).
- Roles interoperate **across families** — the machine is role‑based, not name‑based — so
  `@if(...)…@between(...)…@else(...)` is one valid set.

**`[ScopeChannel]` goes on Continuation and Terminal, not on the Opener** (R11). Continuation and
terminal extensions *read* the channel (`TryRead`), and locals‑frame provisioning keys off
`[ScopeChannel]`; omit it and their read always misses at render time (the engine warns —
**HED3005** at runtime, **HED7016** at build time — but cannot fix it for you). An opener publishes
*opportunistically*: it carries no `[ScopeChannel]`, so a set with no continuation/terminal sibling
provisions no frame and the publish is a harmless no‑op — this is exactly what keeps templates that
use no branch allocation‑identical.

**Type the body against the parent model** (R12). A branch body renders under the *enclosing* model,
not the condition value, so override `InitStart` with the canonical form all four built‑ins use:

```csharp
public override ExType InitStart(InitContext init, ExType dataType, ExType chainedType, ExType parent)
{
    return base.InitStart(init, parent, chainedType, null);
}
```

**Worked example — `@begin`/`@between`/`@finish`** (mirrors the trio kept honest by
`BranchRoleUniversalityTests`). All three drive the set through the *public* `Scope` channel
(`Publish`/`TryRead` with `BranchState.ReservedKey`) — never the engine's internal branch
conveniences:

```csharp
using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;

// Opener — canonical InitStart; publishes the initial BranchState. No [ScopeChannel].
[ExtensionName("begin")]
[BranchRole(BranchRole.Opener)]
public class BeginExtension : AbstractExtension
{
    public override ExType InitStart(InitContext init, ExType dataType, ExType chainedType, ExType parent)
        => base.InitStart(init, parent, chainedType, null);

    public override object ProcessData(in Scope scope)
    {
        bool satisfied = Truthy(scope.ModelData);
        TryPublish(scope, satisfied);                      // opportunistic (see R11)
        return satisfied ? GetInnerResult(scope.Parent()) : string.Empty;
    }

    public override void RenderData(in Scope scope)
    {
        bool satisfied = Truthy(scope.ModelData);
        TryPublish(scope, satisfied);
        if (satisfied) RenderInnerResult(scope.Parent());
    }

    private static bool Truthy(object v) => v != null && (!(v is bool b) || b);

    private static void TryPublish(in Scope scope, bool satisfied)
    {
        try { scope.Publish(BranchState.ReservedKey, new BranchState(satisfied)); }
        catch (InvalidOperationException) { /* no frame => no reader; nothing to publish */ }
    }
}

// Continuation — reads the channel, republishes, may render. Carries [ScopeChannel].
[ExtensionName("between")]
[ScopeChannel]
[BranchRole(BranchRole.Continuation)]
public class BetweenExtension : AbstractExtension
{
    public override ExType InitStart(InitContext init, ExType dataType, ExType chainedType, ExType parent)
        => base.InitStart(init, parent, chainedType, null);

    public override object ProcessData(in Scope scope)
    {
        if (AlreadySatisfied(scope)) return string.Empty; // an earlier branch fired — leave it unchanged
        bool truthy = scope.ModelData is bool b ? b : scope.ModelData != null;
        scope.Publish(BranchState.ReservedKey, new BranchState(truthy));
        return truthy ? GetInnerResult(scope.Parent()) : string.Empty;
    }

    public override void RenderData(in Scope scope)
    {
        if (AlreadySatisfied(scope)) return;
        bool truthy = scope.ModelData is bool b ? b : scope.ModelData != null;
        scope.Publish(BranchState.ReservedKey, new BranchState(truthy));
        if (truthy) RenderInnerResult(scope.Parent());
    }

    private static bool AlreadySatisfied(in Scope scope)
        => scope.TryRead(BranchState.ReservedKey, out var v) && v is BranchState s && s.Satisfied;
}

// Terminal — reads the channel, renders when unsatisfied, throws when no set is open. Carries [ScopeChannel].
[ExtensionName("finish")]
[ScopeChannel]
[BranchRole(BranchRole.Terminal)]
public class FinishExtension : AbstractExtension
{
    public override ExType InitStart(InitContext init, ExType dataType, ExType chainedType, ExType parent)
        => base.InitStart(init, parent, chainedType, null);

    public override object ProcessData(in Scope scope)
    {
        if (!scope.TryRead(BranchState.ReservedKey, out var v) || !(v is BranchState s))
            throw new TemplateProcessingException("'@finish' is a branch terminal with no matching opener in this scope.");
        return s.Satisfied ? string.Empty : GetInnerResult(scope.Parent());
    }

    public override void RenderData(in Scope scope)
    {
        if (!scope.TryRead(BranchState.ReservedKey, out var v) || !(v is BranchState s))
            throw new TemplateProcessingException("'@finish' is a branch terminal with no matching opener in this scope.");
        if (!s.Satisfied) RenderInnerResult(scope.Parent());
    }
}
```

`@begin(A){{a}}@between(B){{b}}@finish(){{c}}` now behaves exactly like
`@if(A){{a}}@elif(B){{b}}@else(){{c}}`. The role is `Inherited = true`: a subclass of `BeginExtension`
with a new `[ExtensionName]` and no re‑attribution is still an Opener.

Because a custom terminal's render‑time orphan message is yours to phrase, throw a
`TemplateProcessingException` when the read misses (as `@finish` does) — the engine's HED3003 covers
only the statically visible case.

**Precompilation.** Custom branch sets are fully functional on the runtime (dynamic) tier — the
set‑*structuring* rules above apply on both tiers because classification is role‑based everywhere.
Only the pinned branch *emission* is reserved for the engine's own built‑ins; a bodied call to a
custom branch extension simply falls back quietly to the dynamic tier (no `HED7015` error — the
`InitStart` override is the canonical shape here, not a mistake), and renders identically with full
role semantics.

---

## A minimal example

A `@upper(...)` extension that uppercases a string and HTML‑encodes the result:

```csharp
using System.Globalization;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace MyApp.Extensions
{
    [ExtensionName("upper")]
    [DataType(typeof(string))]
    [EncodeOutput]                       // HTML‑encode the output
    public class UpperExtension : AbstractHtmlExtension
    {
        public override ExType InitStart(InitContext init, ExType dataType, ExType chainedType, ExType parent)
        {
            // produce a string; let the base compile any {{ }} body
            return base.InitStart(init, parent, chainedType, null);
        }

        protected override object ProcessDataInternal(in Scope scope)
        {
            return scope.ModelData is string s
                ? s.ToUpper(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        protected override void RenderDataInternal(in Scope scope)
        {
            if (scope.ModelData is string s)
                scope.Renderer.Render(s.ToUpper(CultureInfo.InvariantCulture));
        }
    }
}
```

Usage in a template: `@upper(Name)`.

> **Precompilation note.** This example overrides `InitStart` (to type the body against the parent
> model), which is a **runtime‑tier** shape. Because `UpperExtension` is not a `[BranchRole]` extension,
> a *precompiled* template calling `@upper(...)` draws a build error (`HED7015`) — see
> [Precompiled mode](#precompiled-mode). If your templates must precompile, omit the `InitStart` override
> (accepting the default typing) or keep such templates on the dynamic tier.
>
> The example also derives from `AbstractHtmlExtension`/`[EncodeOutput]`, whose HTML encoding is **not**
> reproduced by precompiled binding (see the warning under [Precompiled mode](#precompiled-mode)).

Compare with the real [`StringExtension`](../src/Heddle/Extensions/StringExtension.cs) and
[`DateExtension`](../src/Heddle/Extensions/DateExtension.cs), which follow the same shape.

---

## Attributes

Declared in [src/Heddle/Attributes](../src/Heddle/Attributes):

| Attribute | Target | Purpose |
| --- | --- | --- |
| `[ExtensionName("name")]` | class | The verb used in templates (`@name(...)`). Required. The empty name `""` is the unnamed `@(...)` carrier; `raw` is its always‑verbatim alias. |
| `[DataType(typeof(T))]` | class | The model type the extension expects. Repeatable (e.g. `int` *and* `long` on `IntegerExtension`). |
| `[ChainedType(typeof(T))]` | class | The expected chained‑input type. |
| `[EncodeOutput]` | class | HTML‑encode the output (pairs with `AbstractHtmlExtension`). This encodes under **both** output profiles — it is independent of `OutputProfile`, which only governs the unnamed `@(...)` carrier. Keep `[EncodeOutput]` on value formatters that emit user text; leave it off for containers that merely forward a body (so encoding stays at the emitting leaf). |
| `[ExtensionReplace]` | class | Marks an extension intended to replace another of the same name. |
| `[BranchRole(BranchRole.Opener\|Continuation\|Terminal)]` | class | Declares the extension's position in a branch set (opener/continuation/terminal), giving it the same set semantics as the built‑in `@if`/`@elif`/`@else` family. Compile‑time only; inherited by subclasses. See [Building your own branch set](#building-your-own-branch-set). |
| `[NotEncode]` | model property | Reserved, currently **inert** — the attribute type ships but has no effect (its only check runs against extension classes, never properties). Do not rely on it; its per‑property meaning is revisited with typed props. |
| `[Hidden]` | model property | Hide a model property from template resolution. |
| `[Options("fieldName")]` | member | Reserved, currently **inert** — `FieldName` is stored but never read for member resolution (which consults only `[Hidden]`). Intended to override the name a property is addressed by in templates; do not rely on it yet. |

`[ExtensionName]` is `AllowMultiple = true`, so one class can answer to several names. A later
registration of the same name **replaces** an earlier one only if the newcomer derives from it or
carries `[ExtensionReplace]`; otherwise registration throws `TemplateOverrideException`.

---

## Registering your extensions

Two steps:

1. **Export** the extension(s) from the assembly with the assembly‑level attribute
   [`ExportExtensions`](../src/Heddle/Attributes/ExportExtensionsAttribute.cs):

   ```csharp
   using Heddle.Attributes;

   [assembly: ExportExtensions(typeof(MyApp.Extensions.UpperExtension))]
   // or export several:
   // [assembly: ExportExtensions(typeof(A), typeof(B))]
   // or export everything discoverable in the assembly:
   // [assembly: ExportExtensions]
   ```

2. **Configure** the engine with your startup assembly so the export is discovered:

   ```csharp
   HeddleTemplate.Configure(typeof(Program).GetTypeInfo().Assembly);
   ```

`Configure` walks the given assembly and its references, so exporting from any referenced
assembly is sufficient as long as that assembly is reachable from the one you pass.

## Precompiled mode

When you [pre‑compile templates](precompilation.md) at build time, a custom extension is
**bound from its referenced assembly, never inlined** — a security or logic patch reaches
precompiled templates by updating the package, no regeneration. For a template that uses your
extension to precompile, the extension must satisfy the same contract precompiled binding
reproduces:

- **A parameterless constructor.** The generator constructs one shared, pre‑built instance per
  call site (`new YourExtension()`); no `Activator`, no registry lookup at run time.
- **No reliance on runtime registry mutation.** The instance is built once and never mutated
  after binding; extensions that expect to be re‑registered or reconfigured per render are not
  supported.
- **No `InitStart`/`CompleteInit` override** *(outside the engine assembly)*. Those are
  compile‑time hooks the build‑time backend runs the *base* behavior of; an override could run
  arbitrary compile‑time logic the generator cannot evaluate, so a template binding such an
  extension is a build error (`HED7015`). **Exception:** a `[BranchRole]` custom branch extension
  is expected to override `InitStart` (its canonical parent‑model shape), so it is *not* a
  `HED7015` error — a bodied call to it degrades quietly to the dynamic tier instead (see
  [Building your own branch set](#building-your-own-branch-set)). Keep custom logic in `ProcessData`/`RenderData` — the
  render‑time methods both backends share. A plain, non‑encoding extension (the common case) needs no changes.

**Build‑time binding covers only bodiless custom calls.** A bodiless value transform (`@ext(x)`) binds
directly to your extension at build time; a call that carries a `{{ … }}` body falls back to the dynamic
tier for that call site (a bodied body's model‑typing is extension‑specific, so the generator cannot bind
it conservatively). Bodied calls therefore run identically to the dynamic path.

> **Warning — precompiled binding does NOT reproduce `[EncodeOutput]` / `AbstractHtmlExtension` encoding.**
> On the dynamic tier, an extension deriving from `AbstractHtmlExtension` and marked `[EncodeOutput]`
> HTML‑encodes its output. Precompiled binding hard‑codes `RenderType.Raw` for every custom extension and
> never reads `[EncodeOutput]`, so the *same* extension renders its output **unencoded** once its template
> is precompiled — a silent loss of HTML encoding, i.e. an XSS vector for untrusted data. There is **no
> build‑time diagnostic** for this. If you rely on `[EncodeOutput]`/`AbstractHtmlExtension` for encoding,
> either **encode explicitly inside your `ProcessDataInternal`/`RenderDataInternal` override** (so output is
> safe on both tiers) or **exclude such templates from precompilation**. The same gap affects the built‑in
> `@html`. (Scope: the unsafe path is a *bodiless* call — a `{{ … }}` body falls back to the encoding
> dynamic tier — with *no* `InitStart`/`CompleteInit` override, since an override is caught loudly as
> `HED7015`. Built‑ins `@string`/`@money`/`@date`/`@time`/`@int` are unaffected because they override a
> compile‑time hook and fall back to the dynamic tier.)

An extension name that resolves to no `[ExtensionName]` type in any referenced assembly is a
build error (`HED7006`) **when the call carries a `{{ … }}` body**; a bodiless unresolvable call falls back
to the dynamic/function path silently (a delegate‑registered function could satisfy it at run time).
Extensions that only ever run through the dynamic path are unaffected.

## Declaratively exporting functions

Registered functions (the native‑expression helpers of
[native expressions](native-expressions.md)) can also be exported **declaratively** from an
assembly, so the same set is visible to the host at runtime, to the editor tooling, and — in a
build‑time compilation — to the source generator. One attribute, three readers.

1. **Export** the function container(s) with the assembly‑level attribute
   [`ExportFunctions`](../src/Heddle/Attributes/ExportFunctionsAttribute.cs). A container is a
   `public static` class; **every** public static method it declares becomes one registrable
   function under its lowercase‑invariant method name (`TitleCase` → `titlecase`). Make any helper
   that must not be exported non‑public — the container is the unit of export.

   ```csharp
   using Heddle.Attributes;

   [assembly: ExportFunctions(typeof(MyApp.TemplateFunctions))]
   // or several: [assembly: ExportFunctions(typeof(A), typeof(B))]

   namespace MyApp
   {
       public static class TemplateFunctions
       {
           public static string TitleCase(string value) => /* … */;   // → titlecase(string)
       }
   }
   ```

   Eligibility is exactly the [`FunctionRegistry.Register`](csharp-api.md) rule set: static, closed
   (no open generics), non‑`void`, no `ref`/`out`/pointer parameters. An ineligible method or a
   non‑public/non‑static container is a host programming error (`ArgumentException`).

2. **Register** the exports into your function registry at startup — before the first compile:

   ```csharp
   var functions = new FunctionRegistry();                    // starts with the built‑ins
   functions.RegisterFrom(typeof(Program).Assembly);          // add every [ExportFunctions] export
   var options = new TemplateOptions { Functions = functions };
   ```

`RegisterFrom` goes through the exact `Register(string, MethodInfo)` path — replace on an exact
signature, overload otherwise, the same ranked overload resolution. Calling it during startup keeps
the runtime registry identical to what the editor's one‑shot workspace scan sees, so completion,
hover, and diagnostics match your host's compile. Purely runtime `Register(name, delegate)`
registrations remain host‑only (invisible to the editor) — export them declaratively to share them.
See [editor support](editor-support.md) for the editor side.

---

## Common categories in practice

Production projects tend to add a handful of extensions for cross‑cutting concerns. Knowing the
shapes helps you recognise where a custom extension is the right tool:

- **Escapers / encoders** — value transforms used in attributes and URLs, e.g. `@quote(Name)`
  (attribute‑safe text) or a stricter HTML encoder. These wrap a value and emit a string;
  model the API on [`StringExtension`](../src/Heddle/Extensions/StringExtension.cs).
- **Asset / URL helpers** — e.g. `@asset_url(Image)` to map a path to a CDN URL (often combined
  with a literal suffix in the template: `@asset_url(Image).webp`). A value‑in, string‑out
  extension.
- **Region collectors** — extensions like `@head(){{ … }}` or `@script(){{ … }}` that *capture*
  their body and emit it elsewhere in the document (the `<head>`, end‑of‑body scripts). These
  consume a subtemplate and defer its output; they pair naturally with a
  [layout definition](language-reference.md#inheritance-and-override-childbase) that renders the
  collected regions.
- **Declarations** — extensions that configure compilation and emit nothing (like the built‑in
  [`using`](built-in-extensions.md#using) / [`model`](built-in-extensions.md#model)).

See [Patterns → custom extensions in practice](patterns.md#custom-extensions-in-practice) for
how these read at the call site.

---

## Tips

- **Return the correct type from `InitStart`.** It drives compile‑time type checking for any
  call chained after yours.
- **Keep `ProcessData` and `RenderData` consistent.** Divergent behavior between them causes
  output that depends on context.
- **Use the base helpers** (`GetInnerResult` / `RenderInnerResult` / `InnerExist`) rather than
  re‑implementing subtemplate rendering.
- **Dispose owned resources** by overriding `Dispose(bool)` and calling `base.Dispose(...)`
  (see `PartialExtension`).
- For deeper internals (how `InitStart`/`CompleteInit` fit into the compile pass), see
  [Architecture](architecture.md).
