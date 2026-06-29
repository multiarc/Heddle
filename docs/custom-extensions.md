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
   (used when a parent needs your result as a value, e.g. inside a chain).

> Implement both `ProcessData` and `RenderData` with equivalent behavior. The engine chooses
> between them depending on context (direct rendering vs. value composition).

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
| `RootData` | The root model (what `::` / `@root` resolve to). |
| `CallerData` | Caller context. |
| `Renderer` | The output sink (`Renderer.Render(string)`). |

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

Compare with the real [`StringExtension`](../src/Heddle/Extensions/StringExtension.cs) and
[`DateExtension`](../src/Heddle/Extensions/DateExtension.cs), which follow the same shape.

---

## Attributes

Declared in [src/Heddle/Attributes](../src/Heddle/Attributes):

| Attribute | Target | Purpose |
| --- | --- | --- |
| `[ExtensionName("name")]` | class | The verb used in templates (`@name(...)`). Required. The empty name `""` is reserved for the unnamed `@(...)` extension. |
| `[DataType(typeof(T))]` | class | The model type the extension expects. Repeatable (e.g. `int` *and* `long` on `IntegerExtension`). |
| `[ChainedType(typeof(T))]` | class | The expected chained‑input type. |
| `[EncodeOutput]` | class | HTML‑encode the output by default (pairs with `AbstractHtmlExtension`). |
| `[ExtensionReplace]` | class | Marks an extension intended to replace another of the same name. |
| `[NotEncode]` | model property | Mark a *model* property as pre‑trusted so its value isn't HTML‑encoded. |
| `[Hidden]` | model property | Hide a model property from template resolution. |
| `[Options("fieldName")]` | member | Override the name a property is addressed by in templates. |

`[ExtensionName]` is `AllowMultiple = true`, so one class can answer to several names. A later
registration of the same name **replaces** an earlier one.

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
