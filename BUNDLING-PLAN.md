# Bundling the VS Code extension

## Status

Executed in 453cad53. The review of that change added three things this plan did not foresee:

- **License texts.** Bundling drops the loose `node_modules/*/License*` files that used to satisfy the
  dependencies' MIT/ISC/BlueOak notice clauses, and esbuild strips their copyright headers. `build/bundle.js`
  now writes `dist/ThirdPartyNotices.txt` from the packages esbuild actually bundled, and it ships beside the
  bundle.
- **A gate that loads the bundle.** Step 4 below is no longer a manual check. CI's `vsix` job runs
  `build/verify-vsix.js` on every packaged VSIX. It loads the bundle against a stubbed `vscode` module outside
  any `node_modules`, and requires notices for every bundled package. Nothing reaches activation.
- **The Marketplace token.** The publish job's `npm ci` used to run every dependency's install script,
  esbuild's included, with the token in the environment. It now runs with `--ignore-scripts`, the job sets
  `npm_config_ignore_scripts` so no `npx` runs one either, and the token is scoped to the two steps that use
  it.

Step 5, the GUI leg, was run during the review: VS Code 1.139.0 and 1.91.0 against a real server
(activation, diagnostics, completion, Restart Language Server), matching the unbundled build.

## Why

`vsce package` warns on every one of the seven per-target VSIXs:

> This extension consists of 356 files, out of which 182 are JavaScript files. For performance reasons,
> you should bundle your extension... You should also exclude unnecessary files by adding them to your
> `.vscodeignore`.

That count is from a CI-built VSIX, so it includes roughly 93 files of the .NET server under `server/`.
Packaged without `server/`, the same VSIX holds 263 entries, 182 of them JavaScript.

Both halves have one cause. The extension has a single runtime dependency,
`vscode-languageclient@^10.1.0`, which resolves to nine packages (~2.9 MB); `main` points at raw `tsc`
output; and `.vscodeignore` never excluded `node_modules/**`. So the whole dependency tree ships as loose
files, in every target, seven times over. 181 of the 182 JavaScript files are dependencies — exactly one
is ours.

Bundling collapses them into a single `dist/extension.js`, which is what the extension host loads at
activation. The .NET language server under `server/` is untouched: it is not JavaScript, it is resolved at
runtime through `context.asAbsolutePath` (relative to the extension root, not to `dist/`), and it must
keep shipping.

## Out of scope

Left exactly as found, because they are a separate defect and folding them in would make this two changes:

- the broken `test` script — `node ./dist/test/runTest.js`, where no `runTest.ts` exists and none can be
  produced by the current tsconfig
- the wrong extension id in `editors/vscode/src/test/smoke.test.ts` (`heddle.heddle`; the real id is
  `multiarc.heddle`)

Note for whoever fixes those: after this change `tsconfig.json` is non-emitting, so the test harness will
need its own `tsconfig.test.json` emitting to `out/`.

## The one non-obvious problem: the output collision

Before this change, `tsc -p ./` emitted `dist/extension.js`. esbuild would write the same path. Whichever
runs last wins, so `npm run compile` would silently replace a bundle with unbundled output — and the damage
shows only at F5 or in a shipped VSIX.

**Resolution: put `"noEmit": true` in `tsconfig.json`** and let `dist/` belong to the bundler alone. A
`--noEmit` flag on a script line protects only the invocations that carry it; a human running `npx tsc -p .`
still clobbers the bundle. In the config, no invocation of `tsc` in this directory can write to `dist/`, so
the collision stops being a rule to remember. Type checking is fully preserved — `tsc -p ./` still runs,
still `strict`, still covers the test file. `outDir` and `sourceMap` become dead config and go in the same
edit.

## Changes

### 1. `editors/vscode/tsconfig.json`

Add `"noEmit": true`; remove `"outDir": "dist"` and `"sourceMap": true`. Keep `rootDir` — it still
constrains inputs. Do this first, so the collision is closed before anything can exploit it.

### 2. `editors/vscode/build/bundle.js` (new)

Plain CommonJS node script, modelled on `build/copy-grammar.js` in the same directory: `require` at the
top, a header comment saying *why*, paths from `path.resolve(__dirname, '..')`, a closing `console.log`,
`process.exit(1)` on failure. No config file.

| Option | Value | Why |
| --- | --- | --- |
| entry | `src/extension.ts` | esbuild transpiles TS directly, so it consumes CI's `sed`-rewritten `PINNED_VERSION` straight from source with no intermediate to go stale |
| outfile | `dist/extension.js` | unchanged `main`; `dist` stays gitignored |
| `external` | `['vscode']`, nothing else | node builtins are external automatically under `platform: 'node'` |
| platform | `'node'` | **mandatory.** `vscode-languageclient`, `vscode-languageserver-protocol` and `vscode-jsonrpc` all export `./node` under the `node` condition only, with no `default` — any other platform cannot resolve `vscode-languageclient/node` at all |
| format | `'cjs'` | the host `require()`s `main`; also keeps `minimatch` (type: module, dual exports) on its `require` branch |
| target | `'node20'` | `engines.vscode: ^1.91.0` → VS Code 1.91 → Electron 29.4 → Node 20.9. The exact floor, not a guess |
| minify | `false` | unminified output names each module in stack traces; minified collapses to ~28 lines naming nothing. Since no `.map` ships, this is the only form debuggable from a user's bug report, and ~950 KB is noise beside the .NET server payload |
| sourcemap | `false` (`--watch` only: on) | `.vscodeignore` ships no maps; a `sourceMappingURL` pointing at a file that isn't in the VSIX is worse than none. The watch build feeds F5, which needs them |

Support `--watch` through `esbuild.context()` so F5 keeps working.

### 3. `editors/vscode/package.json`

```json
"vscode:prepublish": "npm run copy-grammar && npm run check-types && npm run bundle",
"copy-grammar": "node build/copy-grammar.js",
"check-types": "tsc -p ./",
"bundle": "node build/bundle.js",
"compile": "npm run check-types && npm run bundle",
"watch": "node build/bundle.js --watch",
"watch:types": "tsc -watch -p ./",
"pretest": "npm run compile",
"test": "node ./dist/test/runTest.js"
```

`check-types` is byte-identical to the old `compile`; only the name moves, because under `noEmit` the
command genuinely type-checks rather than compiles. `watch` **must** become the bundler — under `noEmit` a
`tsc -watch` leaves `dist/extension.js` stale or absent, which would break F5, a regression this change
would otherwise introduce. `compile` survives as the pair so `pretest` needs no edit. `vscode:prepublish`
gains `check-types` explicitly, because esbuild does not type-check and `vsce package` is the only thing
CI runs.

Dependency: `npm install --save-exact --save-dev esbuild`. Pin exactly, as `@vscode/vsce` is pinned to
`3.9.2` — this tool produces the shipped bytes. Verify `package-lock.json` gains the `@esbuild/linux-x64`
and `@esbuild/linux-arm64` entries even though you installed on Windows, or `npm ci` on `ubuntu-latest`
fails. (esbuild has `hasInstallScript: true`; CI's packaging `npm ci` does not use `--ignore-scripts`.)

This does not disturb `VersionConsistencyTests.NpmManifestsCarryTheCanonicalVersion`, which cuts the
manifest at the first of `dependencies`/`devDependencies`/`engines` — `engines` is at line 24, well above.

### 4. `editors/vscode/.vscodeignore`

**Primary: keep the existing denylist and add `node_modules/**`.** One line, minimal diff, certain to
work. Update the header comment too — it claims the VSIX ships `dist/extension.js` "(plus its runtime
dependencies)", which is false once bundled.

An allowlist form (`**` then `!` negations) is stricter and cannot ship a file nobody named, but
gitignore-style negation cannot re-include a path whose parent directory is excluded, and this was not
validated by actually packaging. Treat it as optional hardening: adopt it only after listing the built
VSIX and confirming `README.md`, `LICENSE`, `language-configuration.json` and `syntaxes/**` are all still
present.

Must keep shipping either way: `package.json`, `README.md`, `LICENSE`, `language-configuration.json`,
`syntaxes/**`, `dist/extension.js`, `server/**`.

### 5. The pin — `src/Heddle.Tests/VsCodeExtensionPackagingTests.cs` (new)

The failure mode is not "node_modules is missing" — it is "the manifest names an entry point the packaging
no longer produces in a usable form", which installs cleanly and throws at activation on a user's machine.
That is a chain across four files, and any link breaking ships a broken extension. Assert the closed chain:

1. `.vscodeignore` excludes `node_modules`
2. `main` is `./dist/extension.js`, and `vscode:prepublish` runs both `check-types` and `bundle` — `vsce`
   runs only that hook, so a bundle step outside it never runs in CI
3. `build/bundle.js` sets `bundle: true`, `platform: 'node'`, `format: 'cjs'`, `external: ['vscode']` and
   that outfile
4. `tsconfig.json` has `"noEmit": true` and no `outDir`, so nothing else can write the path

Add `Heddle.Tests.VsCodeExtensionPackagingTests` to `src/Heddle.Tests/test-classes.txt` in the **same
commit** — ordinal sort puts it between `VersionConsistencyTests` and `WorkflowContractTests` — or the
suite's inventory gate reddens. Reuse `BuildSurfaceContractTests.FindRepoFile`, as `VersionConsistencyTests`
does.

Rehearse red before trusting it: delete `&& npm run bundle` from `vscode:prepublish` and confirm the
failure names that link.

## CI needs no change to run the bundler

`vsce package` triggers `vscode:prepublish`, so the bundler runs on all seven matrix targets with no edit
to `.github/workflows/lsp.yml`. The existing order already favours it: the `sed` that rewrites
`PINNED_VERSION` in `src/extension.ts` runs before `npm ci` and `vsce package`, so the bundle consumes the
stamped source. (The review later added a step that checks the packaged VSIX; see Status.)

## Verification

Run from `editors/vscode`.

1. **Before**: `npx @vscode/vsce package --target linux-x64 -o ../../before.vsix`; count entries and `.js`
   entries. Expect 263 and 182; that is without `server/`, which adds about 93 more.
2. Apply the change; `npm install --save-exact --save-dev esbuild`; confirm the lockfile gained the Linux
   binaries.
3. `npm run compile && npx @vscode/vsce package --target linux-x64 -o ../../after.vsix`. Expect the warning
   gone and exactly one `.js` entry. Without `server/` published in, the VSIX should hold 9 entries: the six
   shipped files, `dist/ThirdPartyNotices.txt`, and vsce's `[Content_Types].xml` and `extension.vsixmanifest`.
4. **Prove the bundle initialises without VS Code** — the cheap check that catches an elided or unresolvable
   require. Load `dist/extension.js` under `node` with a stubbed `vscode` module, from a copy outside any
   project — no `node_modules` in any parent directory, no `NODE_PATH`, no global node folders (otherwise
   requires resolve from there) — and assert its exports are exactly `activate,deactivate`;
   `build/verify-vsix.js` does exactly this on an unzipped VSIX, and refuses to run anywhere else. Module-level
   evaluation walks the whole `vscode-languageclient/node` → `vscode-languageserver-protocol` →
   `vscode-jsonrpc` → `ril` chain. It does not prove the RAL install:
   `RAL()` is first called at activation (see Known risks).
5. **Install and exercise both paths**: `code --install-extension ../../after.vsix --force`; open a
   `.heddle` file (coloring proves `syntaxes/**` shipped); with no server, confirm the install hint appears
   *and* coloring still works; point `heddle.server.path` at a local `Heddle.LanguageServer.dll` and confirm
   diagnostics and completion work — the end-to-end proof the bundled language client still talks to the
   server. Run **Heddle: Restart Language Server** to exercise `deactivate` → `stop` → `start`. Inspect the
   installed folder: `dist/` holds only `extension.js` and
   `ThirdPartyNotices.txt`, and `node_modules/` is absent.
6. **Prove the collision is closed**: `npx tsc -p .` writes nothing and leaves `dist/extension.js`
   untouched; introduce a type error and confirm `npm run compile` fails at `check-types` before the bundler
   runs.
7. **Gate**: `dotnet build -c Release`, the four suites, and the new pin rehearsed red.

## Known risks, checked against the installed packages

- `vscode-jsonrpc` installs its runtime abstraction layer as a top-level side effect (`ril.install()` →
  `RAL.install`). None of the nine production packages declares `sideEffects`, so esbuild may not elide it,
  and CI's `build/verify-vsix.js` evaluates the bundle on every packaged VSIX. That walks the chain, but
  `RAL()` is first called at activation, so an elided `install()` would load cleanly and fail only there;
  no gate reaches activation.
- `vscode-languageclient` deep-requires `semver/functions/parse` and `.../satisfies`; `semver` has no
  `exports` map, so these resolve as plain paths and bundle normally.
- No `__dirname`, `createRequire`, `require.resolve`, dynamic `require()` or native `.node` binaries
  anywhere in the production tree — so the classic "a file must sit next to the entry point" failure has no
  instance here.
