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
> whole line is swallowed. Trimming keys on the removal mechanism, not on a name list, so no
> registration or name list needs updating.
>
> On the **dynamic tier** that is all it takes. For the block to be removed when the template is
> **precompiled**, declare [`[ZeroOutput]`](#precompiled-mode): a build-time generator reads symbols
> and cannot observe that your `InitStart` returns `null`, so without the attribute the block is
> removed dynamically and kept as output precompiled — the two tiers disagree on bytes, which is the
> one thing pre-compilation may never do. Declare it whenever `InitStart` returns `null`.

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

// Value format — no intermediate string on the span/UTF-8 tiers (net8+):
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

## Extension parameters (`[Prop]`)

A custom extension can declare named, typed input **parameters** with class‑level `[Prop]`
attributes — the same contract definition props use, so the call shape and diagnostics are
identical to a definition with props:

```csharp
[ExtensionName("grid")]
[Prop("columns", typeof(int), Default = 3)]   // optional (has a default)
[Prop("span", typeof(int))]                   // required (no default)
[Prop("label", typeof(string), Optional = true)]   // optional with a null default
public sealed class GridExtension : AbstractExtension
{
    public override object ProcessData(in Scope scope)
    {
        int columns = (int) scope.GetParameter("columns");        // throws for an undeclared name
        if (scope.TryGetParameter("label", out var label)) { … }  // false (never throws) when absent
        …
    }
}
```

Callers pass parameters by name — `@grid(Photos, columns: 4, span: 2)` — and get the same
positioned diagnostics definitions get: unknown name (HED5001), missing required (HED5002), type
mismatch (HED5003), duplicate argument (HED5004). Named arguments on an extension that declares
**no** `[Prop]` remain an error (HED5005). `[Prop]` is inherited by subclasses; a subclass may
re‑declare an inherited parameter with an assignable (narrowing) type and a new default. Values are
bound once at compile — an all‑constant call site shares one frozen array across renders (no
per‑render allocation). Parameter‑declaring extensions precompile exactly as definitions with props do, on the bodiless and
the bodied path alike: the `[Prop]` layout is frozen at build and the carrier that installs it wraps
the extension *after* its own hook has run, which is the order the engine's compiler uses — see
[Precompiled mode](#precompiled-mode).

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
**HED3005** at the call on both tiers, plus **HED7016** once per drifting type at build time — but
cannot fix it for you). An opener publishes
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

**Precompilation.** Custom branch sets are fully functional on both tiers, and they **precompile**: the
set‑*structuring* rules above apply everywhere because classification is role‑based, and your
`InitStart` override — the canonical shape for a branch extension — runs for real inside your own
assembly when the generated template's type initializer runs. Nothing about it has to be predicted, so
nothing about it costs a tier.

---

## Building your own slot projection

A definition that declares a slot parameter — `<card(out:: Photo)>` — does not pre‑render the content
its call site passed it. The engine installs that content on the scope as an
[`ISlotContent`](../src/Heddle/Data/ISlotContent.cs) and lets each `[SlotProjection]` extension in the
body render it, once per projection, against a model the projection chooses. That is what lets one
caller body be rendered per element of a loop.

Everything the built‑in `@out` reads to do this is public, so a projection of your own is a normal
extension:

```csharp
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;

[ExtensionName("project")]
[SlotProjection]
public class ProjectExtension : AbstractExtension
{
    private bool _slotMode;
    private bool _composed;

    public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
    {
        // The slot type of the definition body being compiled — null anywhere else.
        var slotType = initContext.CompileScope.CompileContext.SlotParameterType;
        if (slotType != null)
        {
            _slotMode = true;
            _composed = initContext.IsChainedConsumer;
            if (!initContext.CallCarriesValue)
                initContext.CompileScope.CompileErrors.Add(/* your own positioned diagnostic */);
            base.InitStart(initContext, chainedType, parent, null);
            return typeof(string);
        }

        base.InitStart(initContext, chainedType, parent, null);
        return chainedType;
    }

    public override void RenderData(in Scope scope)
    {
        if (!_slotMode) { RenderInnerResult(scope); return; }
        var carrier = scope.SlotCarrier;
        if (_composed || carrier == null)
            throw new TemplateProcessingException("'@project' has no caller content to project here.");
        carrier.RenderCallerContentInto(carrier.InvocationScope.Model(scope.ModelData));
    }
}
```

Three things are load‑bearing:

- **Re‑model the invocation scope, don't render against it.** `InvocationScope.Model(value)` pairs the
  value the projection was passed with the props and caller frame of the definition's *invocation
  site*, which is what lets the caller's content see the value it was written for while its `::`‑rooted
  and prop references still resolve where it was written.
- **Prefer `RenderCallerContentInto`.** It writes straight into the scope's renderer;
  `RenderCallerContent` materialises a string and is only worth it when `ProcessData` needs the content
  as a value.
- **Refuse composition.** `InitContext.IsChainedConsumer` is true when the call has a producer to its
  right (`@wrap():project()`), which means the content arrived on the chained channel and there is no
  caller content to project.

`ISlotContent` and `Scope` are both confined to one render lineage — consume them in the call that
received them and never store them.

**Precompilation.** A slot projection precompiles: the generator dispatches on the declaration rather
than on a name, constructs your extension, and runs the `InitStart` above inside your own assembly, so
the slot state is decided by your hook rather than predicted.

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

> **Precompilation note.** This example overrides `InitStart` to type the body against the parent model,
> and a precompiled template calling `@upper(...)` **precompiles anyway**: the generated static
> initializer constructs `UpperExtension` in your own assembly and calls this very method, handing it the
> already‑generated body instead of letting it compile one. The typing decision below is therefore made
> by your code, not guessed at by the build — see [Precompiled mode](#precompiled-mode).
>
> The example also derives from `AbstractHtmlExtension`/`[EncodeOutput]`; that encoding **is** reproduced
> by precompiled binding, on the bodiless and the bodied path alike, and it is now *derived from the live
> type* rather than written into the generated file, so it cannot drift.

Compare with the real [`StringExtension`](../src/Heddle/Extensions/StringExtension.cs) and
[`DateExtension`](../src/Heddle/Extensions/DateExtension.cs), which follow the same shape.

---

## Attributes

Declared in [src/Heddle/Attributes](../src/Heddle/Attributes):

| Attribute | Target | Purpose |
| --- | --- | --- |
| `[ExtensionName("name")]` | class | The verb used in templates (`@name(...)`). Required. The empty name `""` is the unnamed `@(...)` carrier; `raw` is its always‑verbatim alias. |
| `[DataType(typeof(T))]` | class | The model type the extension expects. Repeatable (e.g. `int` *and* `long` on `IntegerExtension`); inherited by subclasses. A call whose value has a static type assignable to none of them is refused when the template is compiled (`HED0004`, naming the value's type and every accepted one) — on both tiers, so a template that does not compile does not precompile either. Assignability is reflection's (`Type.IsAssignableFrom`) in full, with a `Nullable<T>` value unwrapped first — so it includes generic variance (`List<string>` reaches `IEnumerable<object>`) and array covariance, including the element types the CLR reduces to one — each signed/unsigned integer pair (`nint`/`nuint` included) and an enum with its underlying primitive, in either direction and through the array's own generic interfaces (`uint[]` reaches `int[]`, `DayOfWeek[]` reaches `int[]`, `int[]` reaches `IList<uint>`). It is not a conversion relation: there is **no** numeric widening, so `[DataType(typeof(int))]` refuses a `long`, and a value-type element never covaries to a reference-type one, so `int[]` does not reach `object[]`. A value with no static type (`dynamic`) is decided at render instead. |
| `[ChainedType(typeof(T))]` | class | The expected chained‑input type. |
| `[EncodeOutput]` | class | HTML‑encode the output (pairs with `AbstractHtmlExtension`). This encodes under **both** output profiles — it is independent of `OutputProfile`, which only governs the unnamed `@(...)` carrier. Keep `[EncodeOutput]` on value formatters that emit user text; leave it off for containers that merely forward a body (so encoding stays at the emitting leaf). |
| `[ExtensionReplace]` | class | Marks an extension intended to replace another of the same name. |
| `[BranchRole(BranchRole.Opener\|Continuation\|Terminal)]` | class | Declares the extension's position in a branch set (opener/continuation/terminal), giving it the same set semantics as the built‑in `@if`/`@elif`/`@else` family. Compile‑time only; inherited by subclasses. See [Building your own branch set](#building-your-own-branch-set). |
| `[ScopeChannel]` | class | Declares that the extension publishes to or reads from the [local context channel](#the-local-context-channel). Bodies containing one are provisioned with a locals frame at compile time; without one, `Scope.Publish` throws and `Scope.TryRead` returns `false`. Compile‑time only; inherited by subclasses. |
| `[SlotProjection]` | class | Declares that the extension is a **slot projection**: inside a definition body that declares a slot parameter (`<name(out:: Type)>`) the call carries that slot's value and renders the caller's content in its place, and outside one it splices the caller's content against the enclosing model. Both tiers dispatch the slot channel on this declaration rather than on the name the extension answers to, so a custom projection is served exactly as the built‑in `@out` is. Carrying it obliges the extension to read the enclosing definition's slot type off the compile context in its own `InitStart`, to report the slot diagnostics that reading implies, and to render through the scope's slot carrier rather than through its own body. Compile‑time only; inherited by subclasses. |
| `[ChildTemplateHost]` | class | Declares that the extension is a **child‑template host**: its body is not content but a name, which it resolves at compile time to a second template, compiles as a child of the one being compiled, and hosts — rendering that child's output in place of its own body. Both tiers dispatch the child‑template route on this declaration rather than on the name the extension answers to, so a custom host is served exactly as the built‑in `@partial` is. Carrying it obliges the extension to evaluate its own body once at compile time to produce the name, to queue the child compile so the child's errors reach the parent's compile result, and to take delivery of the compiled child in `CompleteInit`. A call to one **precompiles in a default build**: the extension is constructed and its hook run at static init, and the child arrives through the engine's child supply — the precompiled entry when the registry holds the named template, the extension's own compile under the request's options when it does not. Compile‑time only; inherited by subclasses. |
| `[ZeroOutput]` | class | Declares a **directive**: the extension emits nothing and its whole block is removed from the document rather than kept as rendered output. The runtime’s own protocol for this is behavioral — a directive’s `InitStart` returns `null` — and stays authoritative; this attribute is the declarative form of it, and it is what makes the **precompiled tier** classify your extension correctly (a build‑time generator can only read symbols). Declare it whenever `InitStart` returns `null`: without it, the block is removed dynamically and kept as output when precompiled. The four built‑in directives (`@model`, `@using`, `@import`, `@profile`) carry it. Inherited by subclasses. |
| `[Prop("name", typeof(T))]` | class | Declares one typed, named input **parameter** the caller passes by name (`@grid(Photos, columns: 4)`) — the identical call shape a definition with props accepts, with the identical diagnostics. One attribute per parameter (`AllowMultiple = true`); inherited by subclasses. Optional when `Default = value` is set (or `Optional = true` for a null default); required otherwise. Read at render via `Scope.TryGetParameter`/`Scope.GetParameter`. |
| `[NotEncode]` | model property | Reserved, currently **inert** — the attribute type ships but has no effect (its only check runs against extension classes, never properties). Do not rely on it; its per‑property meaning is revisited with typed props. |
| `[Hidden]` | model property | Hide a model property from template resolution. |
| `[Options("fieldName")]` | member | Reserved, currently **inert** — `FieldName` is stored but never read for member resolution (which consults only `[Hidden]`). Intended to override the name a property is addressed by in templates; do not rely on it yet. |

`[ExtensionName]` is `AllowMultiple = true`, so one class can answer to several names. A later
registration of the same name **replaces** an earlier one only if the newcomer derives from it or
carries `[ExtensionReplace]`; otherwise registration throws `TemplateOverrideException`.

---

## Registering your extensions

Two steps to register, and a third if you pre‑compile:

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

2. **Register** each assembly that exports extensions:

   ```csharp
   HeddleTemplate.Register(typeof(Program).GetTypeInfo().Assembly);
   HeddleTemplate.Register(typeof(SomeLibrary.WidgetExtension).GetTypeInfo().Assembly);
   ```

3. **If you pre‑compile**, make sure the build sees the assembly too. The build tier resolves
   extension and model types over the **compilation's reference closure**, not over what is loaded, so
   an extension library you already reference needs nothing — but a project that does not reference it
   can put it in front of the compiler with the escape‑hatch item:

   ```xml
   <ItemGroup>
     <HeddleExtensionAssembly Include="$(SomeDir)Acme.Extensions.dll" />
   </ItemGroup>
   ```

   Model assemblies have the stronger declarative form, `[assembly: HeddleModelAssembly(typeof(T))]`,
   which configures both tiers at once. See
   [Assemblies the build must see](precompilation.md#assemblies-the-build-must-see). An unseen
   extension assembly is never an error — the templates that call it simply degrade to the dynamic
   tier, where step 2's registration still serves them.

Registration is per assembly and is **not** transitive: the engine loads nothing and scans nothing on
its own, so an extension library you merely *reference* is not discovered — register it too. This is
deliberate. Extension names are a shared namespace, and the set of assemblies allowed to claim a name
is the host's decision, not a consequence of which packages happened to be in the dependency graph.

Registration is idempotent per assembly and repeatable, so you may register in whatever order
establishes the precedence you want — a later `[ExtensionReplace]` extension displaces an earlier
incumbent of the same name, and two unrelated types claiming one name throw
`TemplateOverrideException` at the registering call rather than out of a type initializer.

`HeddleTemplate.Configure(assembly)` is the same call under its older name, kept working.

> **Migrating from 2.0.** 2.0 loaded the entry assembly's whole reference closure and scanned all of it,
> so exporting from any referenced assembly was enough and `Configure` was optional. Both are gone:
> export **and** register. A template calling an unregistered extension reports
> `Cannot find extension <name>` (`HED0002`); a precompiled one degrades per request with an
> `ExtensionBindingMismatch` naming it.

## Precompiled mode

When you [pre‑compile templates](precompilation.md) at build time, a custom extension is
**bound from its referenced assembly, never inlined** — a security or logic patch reaches
precompiled templates by updating the package, no regeneration.

**Your extension precompiles.** Bodied, hook‑overriding, `[Prop]`‑declaring, `[BranchRole]`‑carrying —
in a default build, with no property to set and no name list to be on. It needs a parameterless
constructor. If its compile‑time behaviour genuinely cannot be reproduced from a static initializer, it
declares `[PrecompileUnsupported]` and the calls to it fall back **one call site at a time**, never
taking the template with them. The rest of this section is those three sentences with their reasons:

- **A parameterless constructor.** The generator constructs one shared, pre‑built instance per
  call site (`new YourExtension()`); no `Activator`, no registry lookup at run time.
- **No reliance on runtime registry mutation.** The instance is built once and never mutated
  after binding; extensions that expect to be re‑registered or reconfigured per render are not
  supported.
- **An `InitStart`/`CompleteInit` override is fine — it runs for real.** The generated static
  initializer constructs your extension inside your own assembly and calls its actual hook, supplying
  the already‑generated body in place of a body compile. Everything the hook decides is decided by your
  code: the typing it hands the body, the state it caches, the diagnostics it raises. A bodied call to
  your extension precompiles too. Where your hook chooses a model type the build could not resolve, the
  body is emitted with **no model cast** and its member reads bind to the engine's own accessor once
  your hook has answered — three shapes inside such a body still cost that one call site its tier (a
  computed native expression, an embedded C# expression, and a nested call whose typing needs the same
  answer), and the rest of the template is unaffected.
  If your hook genuinely cannot survive this — the clear case being one that walks the enclosing
  document through `InitContext.ParseContext.Tokens`/`SubContexts`, which a single call site cannot
  carry — declare it and be taken at your word:

  ```csharp
  [ExtensionName("toc")]
  [PrecompileUnsupported("reads the enclosing document's headings through InitContext.ParseContext")]
  public sealed class TableOfContentsExtension : AbstractExtension { … }
  ```

  Every call to it binds dynamically and reports `HED7033` quoting your sentence verbatim; the rest of
  the template still precompiles. The attribute is read at build time *and* off the live type at run
  time, so adding it in a package update protects consumers who have already built.
- **A `[Prop]` default whose type generated code can name.** Defaults are frozen into the generated
  source as the exact boxed value the runtime would build from the attribute, so a default of an `enum`
  type — including on an `object`‑typed prop, where the box keeps the enum, not its underlying number —
  is written by naming that enum. A default whose type is `internal` to your assembly cannot be named
  there, so a template calling that extension quietly runs on the dynamic tier instead. Make the enum
  `public` if such templates must precompile.

**There is no list of supported body shapes, and no name on it.** What a `{{ … }}` body is typed against
is your `InitStart`'s decision, so the build stopped keeping an answer of its own — a bodiless value
transform and a bodied call bind by the same route, and a body the build cannot type is written so that
it does not need to be typed. Nothing about your extension has to be recognised for this to work.

> **`[EncodeOutput]` / `AbstractHtmlExtension` encoding is reproduced on both tiers.** Precompiled binding
> derives the render type from the extension's own `[EncodeOutput]`/`[NotEncode]` attributes — the same
> two‑bool decision the dynamic tier evaluates over the live instance — for bodiless calls and, since the
> body‑hosting bind stopped hard‑coding `RenderType.Raw`, for bodied ones too. This paragraph used to warn
> that it did not, and that warning outlived the code it described; the hard‑coded value that was still
> left had no reachable call site until an encoding extension could host a body, which is exactly what
> made fixing it urgent rather than cosmetic. Both halves are pinned by differential tests that render the
> same template on both tiers and compare bytes.

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
