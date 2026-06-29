# Contributing to Heddle

Thanks for your interest in improving Heddle! This document explains how to set up your
environment, the pull-request workflow, and the rules every contribution must follow.

By contributing you agree that your work is licensed under the project's
[Apache License 2.0](LICENSE) and that you certify the
[Developer Certificate of Origin](#developer-certificate-of-origin-dco) for every commit.

## Ground rules

- **All changes go through a pull request** against `main`. Direct pushes are not allowed.
- **Every PR is reviewed by a maintainer (code owner) before it can merge.** Automated,
  unreviewed, or "drive-by" PRs that the author has not understood and tested will be closed.
- **CI must be green** and **all commits must be signed off (DCO)** before a PR is mergeable.

## Development setup

Prerequisites: the **.NET SDK 10.0** (pinned in [global.json](global.json)). Java is only
needed if you regenerate the ANTLR parser from the `.g4` grammar — the generated parser is
checked in, so a normal build does not need it. See [docs/building.md](docs/building.md).

```bash
dotnet restore
dotnet build -c Release
dotnet test src/Heddle.Tests
```

The repository ships an [.editorconfig](.editorconfig) with the project's conventions.
Please follow it in new code (your editor will pick it up automatically) and match the
style of the surrounding code.

## Pull-request workflow

1. **Fork** the repository and create a topic branch from `main`
   (`git checkout -b fix/short-description`).
2. Make focused changes. Keep PRs small and single-purpose; unrelated changes should be
   separate PRs.
3. **Add or update tests.** Many tests are golden-file comparisons under
   [src/Heddle.Tests/TestTemplate](src/Heddle.Tests/TestTemplate); follow the existing patterns.
4. Run `dotnet test src/Heddle.Tests` locally and check your changes against the `.editorconfig`.
5. **Sign off every commit** (`git commit -s`).
6. Open the PR, fill in the template, and link any related issue.
7. A maintainer will review. Address feedback by pushing additional commits (also signed off).

## Coding style

- Style guidance lives in [.editorconfig](.editorconfig) (editor hints, not a CI gate).
- Match the conventions of the surrounding code. Target `LangVersion=latest`.
- Don't commit generated or scratch artifacts (e.g. `bin/`, `obj/`, ANTLR `gen/`, `.antlr/`).

## Developer Certificate of Origin (DCO)

We use the [DCO](https://developercertificate.org/) instead of a CLA. Signing off certifies
that you wrote the patch or otherwise have the right to submit it under the project's license.
Add a `Signed-off-by` line to every commit:

```bash
git commit -s -m "Your commit message"
```

This appends `Signed-off-by: Your Name <your.email@example.com>` using your `git` identity.
A CI check verifies that every commit in a PR is signed off; PRs with unsigned commits cannot
be merged. To fix existing commits, amend or rebase with `git rebase --signoff`.

## Use of AI / code-generation tools

AI assistance is **allowed**, but it does not change your responsibilities as the author:

- You must have **read, understood, reviewed, and tested** every line you submit.
- The contribution must pass CI and follow the style and test conventions above.
- Signing off (DCO) certifies that **you** have the right to submit the change under
  Apache-2.0 — do not submit code you cannot legally license this way (including code an AI
  tool may have reproduced from incompatible sources).
- PRs that appear to be unreviewed machine output (no understanding of the change, untested,
  failing CI, or low-signal mass edits) will be closed.

In short: tools are fine; unverified output is not. The human opening the PR owns the result.

## Reporting bugs and requesting features

Use the [issue templates](.github/ISSUE_TEMPLATE). For **security vulnerabilities**, do **not**
open a public issue — follow [SECURITY.md](SECURITY.md).

## Code of Conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). By participating you agree to
uphold it.
