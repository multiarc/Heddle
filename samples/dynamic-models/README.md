# dynamic-models

**Shows:** the `:: dynamic` expression tier and runtime-assembled template strings — one dynamic template
rendered against two different model shapes. **Source of record:** roadmap current-engine demo (phase 9 D13 row 3).

## Run it

```bash
dotnet run --project samples/dynamic-models
```

The program assembles a template string at runtime (no compiled model type), then renders it against:

1. a statically-typed **object graph** (`Order`/`Customer` POCOs) accessed entirely through the dynamic tier, and
2. a **dictionary-shaped graph** (`ExpandoObject`), the same template serving a completely different shape.

> The object graph uses *named* public types rather than C# anonymous types: the C# runtime binder cannot see
> another assembly's anonymous types, so anonymous objects can't cross into the engine — a .NET limitation, not a
> Heddle one. The dynamic member access at render time is the point, not how the graph was declared.

## Capture mode (what CI runs)

```bash
dotnet run --project samples/dynamic-models -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/dynamic-models
```

Writes `out/dynamic-anon.txt` (the object graph) and `out/dynamic-dictionary.txt` (the `ExpandoObject`).

## What the golden pins

Both rendered outputs. They prove that a single `@model(){{dynamic}}` template resolves members
(`@(Id)`, `@(Customer.Name)`) and enumerates (`@list(Items)`) across heterogeneous runtime shapes.
