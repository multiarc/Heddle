# custom-extensions

**Shows:** the public `Scope.Publish`/`TryRead` coordination channel and the branch protocol as third-party
extension APIs. **Source of record:** [phase 3](../../docs/spec/phase-3-branching/README.md) (phase 9 D13 row 6).

## Run it

```bash
dotnet run --project samples/custom-extensions
```

`ChannelExtensions.cs` defines three third-party extensions (exported via `[assembly: ExportExtensions]`):

- **`@stash(x)`** — a publisher marked `[ScopeChannel]`; writes its model value under its own (non-reserved) key.
- **`@recall()`** — a reader; retrieves the published value later in the same body.
- **`@ifmiss(){{…}}`** — a **branch-protocol participant**: reads the `BranchState` an `@if`/`@elif` publishes under
  the reserved `heddle.branch` key and renders its body only on the *not-taken* path — a third-party `@else`, built
  entirely on the public channel.

Two renders:

| Template | Output |
| --- | --- |
| `@stash(Tag)@recall()` | the stashed value round-trips back out (`hello-channel`) |
| `@if(Show){{visible}}@ifmiss(){{fallback}}` | `visible` when `Show`, `fallback` when not |

## Capture mode (what CI runs)

```bash
dotnet run --project samples/custom-extensions -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/custom-extensions
```

## What the golden pins

`channel.txt` (the publisher/reader round-trip) and `branch.txt` (the participant's behavior across both branch
outcomes). Together they prove the channel and the branch protocol are usable from outside the engine.
