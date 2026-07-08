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

## [1.0.0]

Initial public release.

[2.0.0]: https://github.com/multiarc/Heddle/releases/tag/v2.0.0
[1.0.0]: https://github.com/multiarc/Heddle/releases/tag/v1.0.0
