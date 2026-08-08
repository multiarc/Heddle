---
paths:
  - "src/Heddle/**/*.cs"
  - "benchmarks/dotnet/**"
---

# Performance (render path is hot)

Details: [coding-standards.md § performance](../docs/spec/common/coding-standards.md#performance-rules-the-render-path-is-hot).

- No new per-render allocations. `Scope` stays a `readonly struct` passed by `in`; optional per-render state is lazily created.
- Reflection at compile time only; render time executes pre-compiled delegates and direct calls.
- `[MethodImpl(AggressiveInlining)]` only on tiny, provably-hot transforms — with a benchmark justifying it.
- Perf claims are proven by BenchmarkDotNet (`benchmarks/dotnet`), never asserted. Hot-path changes run affected benchmarks before/after on the same machine: allocated bytes must not increase; mean must stay within reported error. Intentional trade-offs need maintainer ratification.
- Compile-time cost is "compile once, render many" — moderate compile-path allocation is fine, but startup/precompilation paths get measured too.
- No JIT-rescue assumptions for escaping allocations (D7).
