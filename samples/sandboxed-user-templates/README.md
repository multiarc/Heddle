# sandboxed-user-templates

**Shows:** running *untrusted* user templates safely — `ExpressionMode.Native` plus a curated `FunctionRegistry`
as the trust boundary. Benign templates render; hostile ones are declined at compile with positioned `HED1xxx`
diagnostics and never execute. **Source of record:** [phase 1](../../docs/spec/phase-1-native-expressions/README.md)
(phase 9 D13 row 4).

## Run it

```bash
dotnet run --project samples/sandboxed-user-templates
```

The host builds a `FunctionRegistry` that whitelists exactly one function (`shout`) and compiles every user
template with `ExpressionMode.Native` — so operators, literals, member paths and whitelisted functions work, but
the Roslyn C# tier is unavailable. Four benign templates render; three hostile templates are rejected:

| Hostile template | Rejected because |
| --- | --- |
| `@(@ Canary.Fire())` | the `@` C# tier is not allowed under `Native` — the escape never reaches `Canary.Fire()` |
| `@(danger(Name))` | `danger` is not a registered function → `HED1001` |
| `@(Name.ToUpper())` | method calls are not available in native expressions → `HED1003` |

A public `Canary.Fire()` (reachable only through the C# tier) would flip a static flag if a rejected template ever
executed. The program asserts the canary stays **unfired** — the sandbox held.

## Capture mode (what CI runs)

```bash
dotnet run --project samples/sandboxed-user-templates -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/sandboxed-user-templates
```

Writes `out/rendered/<name>.txt` for each accepted template and `out/rejected.txt` — one line per rejected
template: `name: HEDxxxx @ offset:length message`.

## What the golden pins

The rendered outputs (the boundary *lets the safe ones through*) **and** `rejected.txt` — the positioned diagnostic
IDs and offsets (the boundary *stops the rest*). The unfired canary is asserted in-capture (a breach fails the run
before any golden is written).
