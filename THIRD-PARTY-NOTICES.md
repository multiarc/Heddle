# Third-Party Notices

Heddle is licensed under the Apache License 2.0 (see [LICENSE](LICENSE)). It
makes use of the third-party components listed below. This file records
attribution for components whose source is **bundled into a shipped artifact**.
Components that are merely *declared dependencies* (restored independently by the
consumer from NuGet/npm) are listed afterwards for transparency; they are not
redistributed by this project and carry no attribution obligation of their own.

---

## Bundled / redistributed

### ANTLR — generated parser & runtime

The C# lexer/parser under `src/Heddle.Language/generated/` is produced by the
[ANTLR](https://www.antlr.org/) 4 tool from the project's own grammars and is
compiled into the shipped `Heddle.Language` assembly. At run time the assembly
depends on the **ANTLR 4 C# Runtime** (`Antlr4.Runtime.Standard`).

> Copyright (c) 2012-2022 The ANTLR Project. All rights reserved.
>
> Licensed under the **BSD 3-Clause License**.
> <https://github.com/antlr/antlr4/blob/master/LICENSE.txt>

The Heddle grammars themselves (`HeddleLexer.g4`, `HeddleParser.g4`,
`CSharp.g4`) are original works authored for this project and are covered by the
Heddle Apache-2.0 license.

### Ace editor & ANTLR JavaScript runtime (npm package `@multiarc/ace_heddle`)

The published npm bundle is a custom build of the **Ace** code editor that embeds
the **ANTLR 4 JavaScript runtime**. Both are redistributed as part of that
package and are covered by their own licenses (see the `LICENSE` and
`THIRD-PARTY-NOTICES` shipped inside the npm package):

> **Ace (Ajax.org Cloud9 Editor)** — Copyright (c) 2010, Ajax.org B.V.
> Licensed under the **BSD 3-Clause License**.
> <https://github.com/ajaxorg/ace/blob/master/LICENSE>

> **ANTLR 4 JavaScript Runtime** — Copyright (c) 2012-2022 The ANTLR Project.
> Licensed under the **BSD 3-Clause License**.
> <https://github.com/antlr/antlr4/blob/master/LICENSE.txt>

---

## Declared dependencies (not redistributed by this project)

These are resolved from NuGet by consumers of the `Heddle` / `Heddle.Language`
packages and are listed here only for transparency.

| Component | License |
| --- | --- |
| Antlr4.Runtime.Standard | BSD-3-Clause |
| Microsoft.CodeAnalysis.CSharp (Roslyn) | MIT |
| Microsoft.CSharp | MIT |
| Microsoft.Extensions.DependencyModel | MIT |
| Microsoft.Extensions.FileProviders.Embedded | MIT |

## Build- and test-time only (never shipped)

Used to build, test, benchmark, or document the project. Not part of any shipped
artifact.

| Component | License | Used for |
| --- | --- | --- |
| ANTLR 4 tool (`antlr-4.13.1-complete.jar`) | BSD-3-Clause | Grammar code generation |
| Java / JDK | (vendor-dependent) | Running the ANTLR tool |
| xunit, xunit.runner.visualstudio | Apache-2.0 | Unit tests |
| Microsoft.NET.Test.Sdk, coverlet.collector | MIT | Test host / coverage |
| BenchmarkDotNet | MIT | Performance benchmarks |
| Microsoft.AspNetCore.Mvc.* (Razor) | MIT | Benchmark comparison baseline |
| Newtonsoft.Json | MIT | Test/benchmark data |
| VitePress, Vue, Mermaid, Shiki (and transitive deps) | MIT / ISC / BSD | Documentation site |
