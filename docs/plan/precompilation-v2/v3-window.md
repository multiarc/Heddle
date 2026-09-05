# The v3 breaking window

Back to the [plan](README.md). This plan is the window's planning document under
[breaking-windows policy](../../spec/common/breaking-windows.md#policy-applies-to-every-window)
rule 2. v3 opens when 2.1 is released and reconciled (rule 1, [PD8](decisions.md#pd8--the-window)).

## Window items

| # | Item | Migration | Owner |
| --- | --- | --- | --- |
| 1 | `Heddle.Generator` removed; the last 2.x package deprecated on NuGet naming `Heddle.Build` | Reference `Heddle.Build`; keep items, metadata and option properties | [4](phase-4-removal-and-release-tail.md) |
| 2 | Everything in `Heddle.dll` that existed for generated 2.x code is removed — public and internal; the spec enumerates from `public-api-heddle.txt`. What the v3 loader and printer call is new v3 surface. Adopts the register's `ResolvePartial` row | Rebuild with `Heddle.Build` | 4 |
| 3 | v3 does not read 2.x-precompiled assemblies: `Register` throws `PrecompiledRegistrationException` for a pre-v3 marker ([PD4](decisions.md#pd4--artifact-compatibility)); the schema window restarts at the v3 schema | Rebuild every precompiled assembly | 4 |
| 4 | Retired properties `HeddleObserveEngine`, `HeddleNodeFallback`, `HeddleEmitUtf8Pieces` draw a build warning when set; the two observe path properties retire silently ([PD3](decisions.md#pd3--msbuild-surface)) | Delete the property | 4 |
| 5 | Typed entry points render under process-wide default options and throw `PrecompiledMismatchException` on a failed validation of their own artifact ([PD2](decisions.md#pd2--typed-entry-points)) | Assign the default options; call `Register` + `ValidateAll` at startup | [2](phase-2-build-integration.md) |
| 6 | Engine ids replace build-twin ids; `HED70xx` ids for engine-diagnosed facts retire in place ([PD11](decisions.md#pd11--engine-ids-at-build)) | Re-key `WarningsAsErrors`/suppressions on the engine id | 4 |
| 7 | Unbindable function calls: bodiless value sites are late-bound data; bodied or hook-typing use is refusal class (c) ([PD9](decisions.md#pd9--calls-the-build-registry-cannot-bind)) | `[ExportFunctions]`, else `Precompile="false"` | [1](phase-1-compiled-form.md) |
| 8 | Host ≠ target runtime: a BCL member absent on the target framework is a load-time gate fallback, not a build error ([PD6](decisions.md#pd6--build-host)) | `ValidateAll` on the target before serving | 2 |
| 9 | Registry validation gains member-path and late-bound-function checks with must-surface reasons | — | 1 |
| 10 | Spec records: `shared-source-architecture.md` collapses into a program record; D4 superseded by a dated note; generator rules leave `.claude/rules` | — | 4 |

## Execution order

Rule 3: no byte-changing swap and no default flip is in this window. The release tail lands items
1–4 and 6 together with the deprecation and the migration note after phases 1–3 have their
evidence; the golden re-ratification commit is expected to move zero rendered goldens.

## Migration note

Rule 4's one page, owned by phase 4, shipped with the release notes and CHANGELOG: package swap;
every precompiled assembly rebuilt with `Heddle.Build`; the retired properties; typed entry
options source and thrown mismatch; twin → engine-id mapping; the bodiless rule; the .NET 10
runtime prerequisite and host ≠ target consequence.

## Reconciliation

Rule 5: after the v3 release the items are reconciled against shipped source and condensed into
the release records.
