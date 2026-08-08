---
paths:
  - "src/Heddle/**/*.cs"
  - "src/Heddle.Generator/**/*.cs"
  - "src/Heddle.Language*/**/*.cs"
---

# Errors and diagnostics

Details: [coding-standards.md § errors](../../docs/spec/common/coding-standards.md#error-handling-and-diagnostics), [cross-cutting-decisions.md D1 + registry](../../docs/spec/common/cross-cutting-decisions.md).

- Compile-path problems are collected, not thrown: positioned `HeddleCompileError`/`HeddleCompileWarning` on the compile result, so template authors see all problems at once. The public API never surfaces raw exceptions for template mistakes.
- Host-programming errors throw at public API entry points (`ArgumentNullException` etc.).
- Every new diagnostic claims a stable `HED####` ID from the registry (feature-area blocks, D1) **in the same change**; IDs are never reused or renumbered.
- Messages name the construct, point at the remedy, and carry the template position.
- Security-sensitive failures never degrade to execution: a sandbox violation is a compile error, full stop.
