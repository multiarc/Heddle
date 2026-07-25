# Phase 3 supplement — discovery-rules parity tables

Supplement to [Phase 3 — binding layer](phase-3-binding-layer.md). Rule-by-rule parity tables
for the two discovery surfaces this phase unifies: extension discovery
([03 F3](../research/generator-code-sharing/03-binding-layer.md)) and `[ExportFunctions]`
discovery ([03 F2](../research/generator-code-sharing/03-binding-layer.md)). Each table row
names the current runtime rule, the current generator rule, the target shared rule, and where
the target comes from (runtime-authoritative alignment, an open question, or a dependency).
Source citations were re-verified against the working tree on 2026-07-25; line numbers anchor
to the named members.

## Extension discovery and precedence

Runtime: `TemplateFactory.LoadExtensions`/`AddExtensions`
(`src/Heddle/Runtime/TemplateFactory.cs`) over `Helpers/TypeExtension.cs` attribute reads.
Generator: `ExtensionBinder.InspectType` (`src/Heddle.Generator/Emit/ExtensionBinder.cs`).

| Dimension | Runtime today | Generator today | Target shared rule |
|---|---|---|---|
| Shape predicate | `t.IsImplement<IExtension>()` — any class implementing the interface (transitively, via `GetInterfaces()`) | Non-abstract class **deriving from `AbstractExtension`** (`InspectType` `:196`, `DerivesFrom` `:340-346`) | **Runtime predicate** in the shared `ExtensionCandidate` rule-core: implements `IExtension`. The generator's `AbstractExtension` test moves to a separate *bindability* layer applied after discovery — a discovered-but-unbindable type degrades to dynamic with a recorded reason, never `HED7006` |
| Name read | `IsHaveAttribute<ExtensionNameAttribute>(true)` + `GetAttributes<ExtensionNameAttribute>(true)` — **inherited** | Declared-only `type.GetAttributes()` loop (`:199-209`) — inherited names invisible (the Tranche A bug) | Base-chain walk, outermost-derived-first match semantics identical to reflection `inherit: true` for a class-targeted inherited attribute; shared as the "layers, outermost first" sequence the rule-core consumes |
| Multiple names per type | Every `[ExtensionName]` yields a registration (`LoadExtensions` iterates all) | All declared names collected (`names` list) | Unchanged — both agree; the shared record carries the name list |
| Empty name | Registered as-is (the unnamed carrier is a runtime concept) | Skipped (`:232-233`, emitted directly) | Skip stays a *generator bindability* concern outside the shared predicate; the rule-core reports all names |
| Ordering before registration | Sort by `[DataType]` interface-ness, then `[ChainedType]` interface-ness (inherited reads) | Assembly-enumeration order (`:162-166`), first-wins | **Runtime ordering** reproduced in the shared precedence table (interface-ness of attribute arguments is readable from symbol metadata) — per OQ3's adoption recommendation |
| Collision / override | `Replace` sorted last; incumbent replaced when `Replace` **or** `incumbent.IsAssignableFrom(candidate)`; else `TemplateOverrideException` (`AddExtensions` `:84-114`) | First registration wins, `[ExtensionReplace]` unmodeled (acknowledged `:234-235`) | Full runtime precedence in the rule-core (OQ3 — recommended: adopt); assignability edge via `ITypeFacts.IsAssignableFrom`; a collision the runtime would throw on becomes a generator degrade-with-reason (the build must not hard-fail what is a host wiring error) |
| Identity string | `AqnSansVersion(liveType)` (`PrecompiledGauntlet.cs:207-212`) | `FullyQualifiedFormat` minus `global::` + assembly (`:211-216`) — diverges on nested/generic | Both sides through the shared `AqnFormatter` (Tranche A) |
| Failure surface | Runtime renders; gauntlet arbitrates manifest-vs-live per render | `HED7006` **Error** for bodied calls (`TemplateEmitter.cs:650-654`) | `HED7006` fires only when the name resolves to nothing under the **runtime** predicate; resolvable-but-unbindable → quiet degrade (runtime-faithful) |

## `[ExportFunctions]` discovery and registration

Runtime: `FunctionRegistry.RegisterFrom`/`RegisterContainer`/`Register`/`AddOrReplace`
(`src/Heddle/Runtime/Expressions/FunctionRegistry.cs`). Generator:
`FunctionExportResolver.Build`/`AddContainer`
(`src/Heddle.Generator/Binding/FunctionExportResolver.cs`).

| Dimension | Runtime today | Generator today | Target shared rule |
|---|---|---|---|
| Container eligibility | Public static class (`IsClass && IsAbstract && IsSealed`, `IsPublic \|\| IsNestedPublic`) **or throw** `ArgumentException` (`RegisterContainer` `:132-136`) | Silently skipped when not `DeclaredAccessibility == Public && IsStatic` (`AddContainer` `:101-103`) | Shared eligibility predicate over the facts record; ineligible container → generator diagnostic per OQ6 (recommended: warning, ID claimed from `HED70xx`), runtime throw unchanged. Note the accessibility nuance the spec must pin: reflection accepts `IsNestedPublic`; the symbol test must accept a nested-public container too |
| Method enumeration | `Public \| Static \| DeclaredOnly` (`:138-139`) | `container.GetMembers()` filtered to `MethodKind.Ordinary`, public, static (`:108-113`) — `GetMembers` is declared-only, matching | Shared: declared-only, public, static — stated once over the facts record |
| Special-name skip | `IsSpecialName` skip (`:141-142`) | `MethodKind.Ordinary` filter — close but **not identical** relations | Facts record carries `isSpecialName`; the Roslyn adapter documents the `MethodKind.Ordinary` ↔ `!IsSpecialName` mapping and its residual differences in one place |
| Per-method eligibility | Rejected with `ArgumentException`: open generic, `void` return, `ref`/`out`/pointer parameter (`Register` `:84-92`) | **No counterpart** — every ordinary public static method counted (`:108-118`) | Shared predicate over `ExportedMethodFacts` (`isOpenGeneric`, `returnsVoid`, `hasByRefOrPointerParam`); generator excludes exactly what the runtime refuses — this alone fixes the standing overload-count gauntlet failure (`PrecompiledGauntlet.cs:147-158` fails on both `>` and `<`) |
| Name rule | `method.Name.ToLowerInvariant()` (`:144`) | Same (`:115`) | Shared constant rule (trivial, but stated once so a future normalization change cannot fork) |
| Overload bookkeeping | `AddOrReplace`: replace on identical signature, else append (`:182-200`) — **merges across containers** | Per-container `overloadCounts`; first container claims the name exclusively (`:129-137`) | Shared merge bookkeeping producing the manifest rows the gauntlet consumes (OQ2 — recommended: merge). Signature identity for replace-on-identical is part of the shared rule |
| Cross-container same name | Merged overload set | Later containers ignored | Manifest rows reflect the merged registry immediately; **emission** for multi-container names degrades to dynamic until Phase 4's overload-rank core proves overload choice (OQ2) |
| Assembly / attribute enumeration order | Assembly attribute order via `GetCustomAttributes` (`RegisterFrom` `:118-124`) | Compilation assembly + referenced-assembly symbol order (`Build` `:64-77`) | Order only matters where it changes replace-on-identical outcomes; the spec pins one documented order for the shared bookkeeping and both adapters feed it in that order |
| Target identity string | `AqnSansVersion(reg.Method.DeclaringType)` (`PrecompiledGauntlet.cs:141`) | Inline `global::`-strip AQN (`:123-127`) | Shared `AqnFormatter` (Tranche A) |
| Delegate registrations | Legal at runtime; gauntlet treats a delegate under a bound name as mismatch (`PrecompiledGauntlet.cs:138-140`) | Unmodeled | Out of shared-core scope (host-runtime-only concept); noted so the spec does not accidentally "fix" the gauntlet's intended delegate refusal |

## Fault-class table for `PropLayoutCore` (forward reference)

The ordered fault enum the plan's `PropLayoutCore<TType>` item requires, aligned to the
runtime's validation order in `PropLayout.ResolveFromExtension`
(`src/Heddle/Runtime/Expressions/PropLayout.cs:83-179`) and the diagnostic pairs that already
exist (`HED5015`/`HED5010`/`HED5008` family on the dynamic tier, `HED7017` on the build tier —
see the [registry](../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)):

| Order | Fault | Runtime check | Build twin |
|---|---|---|---|
| 1 | `NameInvalid` (null/empty/whitespace) | `PropLayout.cs:106-112` | `HED7017` |
| 2 | `NameReserved` (`out`, `this`) | `:115-121` | `HED7017` |
| 3 | `DuplicateAtLevel` | `:123-129` | `HED7017` |
| 4 | `TypeUnusable` (null / open generic / pointer / by-ref) | `:131-140` | `HED7017` |
| 5 | `RedeclarationNotAssignable` | `:144-154` (via `ITypeFacts.IsAssignableFrom`) | `HED7017` |
| 6 | Default-conversion faults | `ApplyDefault` path (`:254-285`), tables from Phase 4 | `HED7017` |

Both sides map the shared enum to their existing diagnostics — **no new IDs** for prop-layout
faults; the enum guarantees same fault, same declaration, same order, which is what the
lockstep test asserts.
