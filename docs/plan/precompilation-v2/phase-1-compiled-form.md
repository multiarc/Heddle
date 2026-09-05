# Phase 1 — Compiled form and loader

Back to the [plan](README.md). Depends on nothing; gates everything.

## Goal

A second front door for the engine: besides compiling from text (parse → shaping → binding →
expression compilation → a strategy behind `IProcessStrategy`), it compiles from a **compiled
form** — a serialized record of everything the text path decided — and materializes the very
objects the dynamic tier's compiler builds. Rendering a loaded template runs the dynamic tier's
code on every sink.

## The split

Categories, not layouts; the spec fixes the encoding.

| Serialized | Rebuilt at load by the engine's existing machinery |
| --- | --- |
| Shaped document and static pieces; strategy shape; locals requirement; positions, chain structure, item order, render types | Strategy objects, including the runtime's own UTF-8 pre-encoding of pieces |
| Extension identity: registry name plus version-less type identity | Extension instances, constructed by name; their real compile-time hooks run over the deserialized body through a v3 loader entry |
| Prop layouts, frozen prototype values, slot indices, conversion target types | Prop converters |
| Member paths (start type plus segments) with the identities the binding gate checks | Member accessors |
| Native-expression syntax trees with positions | Native delegates, typed at load |
| Function call shapes in value positions ([PD9](decisions.md#pd9--calls-the-build-registry-cannot-bind)) | Overload selection and bound target against the live registry |
| Embedded C# source plus binding context ([PD5](decisions.md#pd5--embedded-c-before-generated-sites)) | Roslyn-compiled delegate under the engine's checks |
| Import closure and partial references by key; definition and region layouts | Nested templates through the registry |
| Artifact identity (options, content, engine and schema versions) | `TemplateOptions`, encoder, render budget — the host's |

## Scope

- **Contract and serializer** in `Heddle.dll`: `netstandard2.0`, Roslyn-free read path,
  deterministic, versioned per [PD4](decisions.md#pd4--artifact-compatibility); produced by walking
  a compiled `HeddleTemplate`'s post-compile graph, nothing re-derived from text.
- **Deferred function binding** as a text-path compile mode for bodiless value sites (PD9).
- **Stable site ids** for every delegate-bearing site, tied to the content hash; identical
  templates give identical ids, any change invalidates them.
- **Loader** behind the existing precompiled-adapter entry of `HeddleTemplate`; type names resolve
  over registered and loaded assemblies only; materialization once per entry, memoized; no render
  allocation.
- **Binding gate**: member-path identities and late-bound function names are checked bindings, so
  `ValidateAll` and the gauntlet report a member that no longer binds or a function the registry
  lacks before materialization, as must-surface fallbacks ([window item 9](v3-window.md#window-items)).
- **Refusal classes**, each return-shaped and costing the site alone: (a) an extension declaring
  `[PrecompileUnsupported]`; (b) a value typed by reflection enumeration order; (c) a body or hook
  whose model type depends on an unbindable function call. The intent table is the sole definition
  of which entries carry them.
- **Parity harness** in `Heddle.Tests`: every corpus entry compile → serialize → load → render,
  byte-compared on three sinks under `Strict` with an `OnFallback` sentinel, plus a file-backed pass.

## Exit criteria

1. Every corpus template the dynamic tier compiles round-trips byte-identically on all three
   sinks; declared refusals equal the intent table by set equality.
2. Serialize twice → identical bytes; serialize → load → serialize → identical bytes.
3. `[MemoryDiagnoser]`: a loaded strategy and its dynamic twin allocate equal bytes per render.
4. Loading touches no `Microsoft.CodeAnalysis` type unless the artifact carries a C# site.
5. Schema or engine mismatch among v3 artifacts is refused with the existing reasons; a member
   path that no longer binds is a reported fallback from `ValidateAll` and the gauntlet.

## Validation scenarios

- A definition library (layering, props, slots, regions, `@partial`) round-trips; hooks run at
  load and body post-state matches the dynamic tier's three post-states.
- A function absent from the build registry: in `@(toUpper(model.Name))` it defers and, at load,
  binds the overload the dynamic tier binds under the same registry; as
  `@list toItems(model.Raw) { @item.Name }` the site is class (c) and a misspelled `@item` member
  raises the same engine error on both tiers once the function is registered.
- A model property renamed after the build: `ValidateAll` reports the entry; the request degrades
  and the dynamic tier raises the engine's compile error at the `.heddle` position.
- Embedded C# under `FullCSharp` loads with Roslyn; under `Heddle.CSharpTierEnabled=false` it
  degrades with `HED9001`.
