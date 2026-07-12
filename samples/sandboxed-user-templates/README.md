# sandboxed-user-templates

**Shows:** running *untrusted* user templates safely — `ExpressionMode.Native` plus a curated `FunctionRegistry`
as the trust boundary. Benign templates render; hostile ones are declined at compile with positioned `HED1xxx`
diagnostics and never execute. **Source of record:** [Native expressions](../../docs/native-expressions.md)
(phase 9 D13 row 4). For the host‑side checklist — DTOs, `[Hidden]`, the registry freeze, render
budgets, and encoding contexts — see
[Exposing models to untrusted templates](../../docs/patterns.md#exposing-models-to-untrusted-templates).

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

### The availability leg — render budgets

The compile-time boundary stops a template from *reading* or *calling* what it shouldn't, but a well-formed
template can still burn CPU or memory (`@for(1000000000){{x}}` compiles cleanly). A
[`RenderBudget`](../../docs/csharp-api.md#render-budgets) on `TemplateOptions` is the availability leg: it caps
output size, render operations, and wall-clock time at the render seam and throws `TemplateRenderBudgetException`
on breach. The sample renders that runaway loop under one budget per dimension and records which kind stopped it:

| Budget | Stops the runaway loop with |
| --- | --- |
| `MaxOutputChars = 1000` | `RenderBudgetKind.OutputChars` |
| `MaxRenderOps = 1000` | `RenderBudgetKind.RenderOps` |
| `MaxRenderTime = 50 ms` | `RenderBudgetKind.RenderTime` |

## Capture mode (what CI runs)

```bash
dotnet run --project samples/sandboxed-user-templates -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/sandboxed-user-templates
```

Writes `out/rendered/<name>.txt` for each accepted template, `out/rejected.txt` — one line per rejected
template: `name: HEDxxxx @ offset:length message` — and `out/budgets.txt`, one line per budget scenario:
`name: stopped by <RenderBudgetKind>`.

## What the golden pins

The rendered outputs (the boundary *lets the safe ones through*), `rejected.txt` — the positioned diagnostic
IDs and offsets (the boundary *stops the rest*) — and `budgets.txt` — the render-budget kind that stops each
runaway loop (the availability leg *bounds the well-formed ones*). The unfired canary is asserted in-capture (a
breach fails the run before any golden is written).
