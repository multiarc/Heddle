# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0]

The single 2.0 breaking window: the safer-by-default engine defaults are now active.

### Changed (breaking)

- `TemplateOptions.OutputProfile` now defaults to `OutputProfile.Html`: the unnamed
  `@(...)` output HTML-encodes by default. Opt out per output with `@raw`, or restore the
  1.x behavior per template with `OutputProfile.Text`.
- `TemplateOptions.TrimDirectiveLines` now defaults to `true`: whole-line directives
  swallow their line. Set `TrimDirectiveLines = false` to restore 1.x behavior.
- Precompiled projects must be rebuilt with the 2.0 `Heddle.Generator` when upgrading the
  engine; 1.x manifests are rejected by the engine-version gate and fall back (or throw
  under `PrecompiledMismatchPolicy.Strict`).

### Added

- Public `Heddle.Attributes.BranchRole` enum (`Opener`/`Continuation`/`Terminal`) and
  `[BranchRoleAttribute]`. Branch-set classification (adjacency stripping, orphan diagnostics,
  terminal-optional validation, locals-frame provisioning) is now driven by `[BranchRole]` rather
  than a hardcoded `@if`/`@ifnot`/`@elif`/`@elseif`/`@else` name list. Any custom extension carrying
  the attribute gets the same set semantics as the built-ins and can even share a set with them; the
  four built-ins now carry `[BranchRole]` and behave byte-identically. Custom branch sets run on the
  dynamic tier (bodied calls fall back quietly on the precompiled tier).
- Optional drift diagnostics for a branch `Continuation`/`Terminal` extension that omits the required
  `[ScopeChannel]` (its read of the branch state would always miss at render time): warning `HED3005`
  from the runtime compiler and warning `HED7016` from the build-time generator. Additive — neither
  fires for the compliant built-ins.

### Changed

- Branch-set diagnostic messages (`HED3002`/`HED3003`/`HED3004`) are reworded to role-neutral
  language (opener/continuation/terminal) so they read correctly for custom branch sets. Diagnostic
  IDs, severities, and positions are unchanged.
- Branch-set classification now follows the registered extension *type*'s `[BranchRole]` rather than
  the literal directive name. This only differs from previous behaviour when a built-in branch name
  (`if`/`ifnot`/`elif`/`elseif`/`else`) is deliberately re-bound via `[ExtensionReplace]` to a type
  that does *not* carry `[BranchRole]`: such a replacement is now (correctly) no longer treated as a
  branch, whereas it was previously forced into the built-in's role by name. No built-in template or
  standard configuration is affected.

### Compatibility

- `Heddle.Generator` and `Heddle` are released and version-locked in lockstep (both `2.0.0`); pair
  the matching versions. If the generator is paired with a pre-`[BranchRole]` engine, branch
  templates simply fall back to the dynamic (runtime) tier — safe and byte-identical output, only
  without precompilation for those templates.

## [1.0.0]

Initial public release.

[2.0.0]: https://github.com/multiarc/Heddle/releases/tag/v2.0.0
[1.0.0]: https://github.com/multiarc/Heddle/releases/tag/v1.0.0
