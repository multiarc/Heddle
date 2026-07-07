# component-props-slots

**Shows:** a small component library — typed **props** (defaults, overrides, required) and parameterized
**slots** — composed from a page template with named arguments. **Source of record:**
[phase 5](../../docs/spec/phase-5-props-and-slots/README.md) (phase 9 D13 row 7).

## Run it

```bash
dotnet run --project samples/component-props-slots
```

Two components are defined in a `@% … %@` block:

- **`<card(style = "plain", compact = false, badge)> :: Article`** — three props: `style` and `compact` have
  defaults, `badge` is **required** (no default, every call site must pass it). The body renders an `@out()` slot.
- **`<picker(out:: MenuOption)> :: Menu`** — declares a **parameterized slot**: `@out(this)` projects each
  `Menu.Options` item as the slot value, and the caller's slot content renders once per item with that projection
  as its scope.

The page composes them:

| Call | Demonstrates |
| --- | --- |
| `@card(Featured, badge: "hot"){{<em>read more</em>}}` | required prop bound; defaults used; slot filled |
| `@card(Featured, style: "wide", compact: true, badge: "sale")` | prop overrides; `compact` hides the summary; empty slot |
| `@picker(Nav){{<a href="/go/@(Id)">@(Label)</a>}}` | parameterized slot projected per `MenuOption` |

## Capture mode (what CI runs)

```bash
dotnet run --project samples/component-props-slots -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/component-props-slots
```

## What the golden pins

`components-page.html`. Each visible region maps to a prop/slot feature: the two `<article>` cards prove
default-vs-override props and the `@out()` slot; the `<ul class="picker">` proves the `out:: MenuOption` parameterized
slot.
