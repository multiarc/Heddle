# Unverified platform surface

Recorded 2026-07-29, after eight review cycles. Everything below is **unmeasured**, not
suspected-broken. It is written down because the review cycles produced confident-sounding
verification claims — "3,576 tests green", "10/10 goldens" — that are true only of the
platforms this work happened on, and the difference between "verified" and "verified where we
could look" is exactly the kind of thing that gets lost after the fact.

The development box is Linux (kernel 6.14), with `Microsoft.NETCore.App` **8.0.29 and 10.0.10
only**. No .NET Framework, no Mono, no Windows, and — see below — no .NET 6 either.

## 1. A whole target framework runs zero tests, and the run still exits 0

`src/Heddle.Tests` declares `net6.0;net8.0;net10.0`. On this box the net6.0 container cannot
start:

```
Testhost process for source(s) '.../bin/Debug/net6.0/Heddle.Tests.dll' exited with error:
You must install or update .NET to run this application.
```

`dotnet test` then reports `Passed!` for net8.0 and net10.0 and **exits 0**. Nothing in the
output says a third of the declared matrix did not execute; you have to go looking for the line
above in a log nobody reads on a green build.

This matters beyond this box. Any CI image missing a declared runtime silently drops that
framework's coverage while staying green, and net6.0 is the target where the `#if
NET6_0_OR_GREATER` branches below first turn on — so it is the one framework where a
lower-bound behaviour could regress unobserved.

**Worth fixing, and not fixed here:** the build should fail when a declared TFM's test host
cannot start. It is a build-wiring change, not an engine change, and it is the single highest-
value item on this page.

## 2. .NET Framework — `netstandard2.0` behaves differently, by design, in four places

`src/Heddle` and `src/Heddle.Language` target `netstandard2.0;net6.0;net8.0;net10.0`.
`netstandard2.0` is what .NET Framework consumes, and the test project only adds `net48` when
`$(OS) == Windows_NT` — so on this box **no netstandard2.0-flavoured code path is executed at
all**. It compiles; that is all that is known.

The conditional sites, each of which is a deliberate behavioural difference rather than a
polyfill:

| Site | On .NET Framework | Consequence if wrong |
| --- | --- | --- |
| [`RoslynReferenceProvider.FromLoadedImage`](../../src/Heddle/Native/RoslynReferenceProvider.cs) | Returns `null` — there is no `TryGetRawMetadata` | An assembly with no `Location` yields no metadata reference, so the language service's byte-loaded model assemblies are invisible to the C# tier. This is the exact defect that was fixed for .NET Core; on .NET Framework it remains, knowingly, because there is no API to fix it with |
| [`AssemblyHelper.IsObservable`](../../src/Heddle/Native/AssemblyHelper.cs) | The `AssemblyLoadContext` exclusion is compiled out | There is no ALC on .NET Framework, so nothing is excluded on those grounds. Collectible isolation there is an `AppDomain` concern the engine does not model at all |
| `AssemblyHelper.MarkEngineEmitted` | `lock` + `TryGetValue`/`Add` instead of `AddOrUpdate` | A different concurrency shape for the same table, exercised nowhere |
| [`Models/Range.cs`](../../src/Heddle/Models/Range.cs) | The `System.Range` interop is compiled out | Unexercised |

## 3. `Path` semantics differ, and the engine's import identity depends on them

`Path.Combine` rejects `<`, `>`, `|` and other characters on .NET Framework and accepts them on
.NET Core. `ParserSettings.ImportIdentity` canonicalises an `@<<` import path to decide whether
two spellings are the same document, and on this box the combine cannot throw — measured:

| Input | `Path.Combine` (net8) | `Path.GetFullPath` (net8) |
| --- | --- | --- |
| `a<b>.heddle` | ok | ok |
| `a\0b.heddle` | ok | **throws `ArgumentException`** |
| `a\|b.heddle` | ok | ok |

The combine was moved inside the guard so a .NET Framework throw becomes a degraded cache key
rather than a parse that dies. **That change is unverified**: no platform available here can
make `Path.Combine` throw, so the guard is reasoning, not evidence. The `GetFullPath` half of
the same guard *is* exercised — the NUL row above reaches it on net8.

## 4. The parse-depth bound is a measurement, and it was taken on one platform

`ParseDepthGuard.MaxDepth` is 250 because Release-build last-safe depths on a **1 MB Linux
thread** were measured at 284 (prefix and `?:`), 287 (`??`) and 574 (parens). The bound is a
depth count rather than a stack probe precisely so that it answers the same on every host — but
the *number* was chosen against one host's stack.

Unknown: whether a Windows thread, a .NET Framework thread, or an IIS/ASP.NET request thread
reaches its limit before 250 rule levels. If any of them does, the guard cannot fire in time and
the process dies where it was supposed to report `HED4007`. Nothing here can measure that.

Note also that the property this bound exists for is **not testable in-process** for the shapes
that recurse inside ANTLR: a test that parses one past the limit does not go red when the
property breaks, it takes the test host down. Only the flat left-associative shape — which ANTLR
loops rather than recurses — reaches the reporting path safely, and that is what
`DeepNestingTests.AFlatRunPastTheLimitIsReported` covers. Verifying the recursive shapes needs a
child process comparing exit codes, which no suite here does.

## 5. Sample goldens are a single-platform artifact

The ten sample goldens are captured and compared on Linux/net8.0. Line endings are governed by
`.gitattributes` (`eol=lf`), so the obvious hazard is handled, but no golden has ever been
produced on Windows. Culture is a second unmeasured axis: the samples run under whatever
culture this box defaults to, and formatting extensions (`DateExtension`, `MoneyExtension`,
`IntegerExtension`, `TimeExtension`) are exactly where a culture difference would show up as a
byte difference.

## What would close this

In rough order of value per unit of effort:

1. Fail the build when a declared TFM's test host cannot start (§1). Cheap, and it converts a
   silent gap into a loud one everywhere, not just here.
2. A Windows CI leg running `net48` + `net6.0` + `net8.0` + `net10.0`. That alone covers §2, §3
   and §5.
3. A child-process depth harness comparing exit codes, run per platform, to turn §4 from a
   measurement on one host into a measurement on each (§4).
