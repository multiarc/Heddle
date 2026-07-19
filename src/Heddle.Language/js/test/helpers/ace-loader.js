"use strict";

/*
 * Ace-module loader shim for the WS9 JS test harness.
 *
 * WHY THIS EXISTS
 * ---------------
 * The Heddle editor sources under `js/src/**` are written to live *inside* an
 * Ace checkout. They therefore `require(...)` Ace-internal modules using paths
 * that are relative to Ace's own `src/` tree, e.g.
 *
 *     require("../lib/oop")                 // heddle_highlight_rules.js
 *     require("./text_highlight_rules")
 *     require("./html_highlight_rules")
 *     require("./csharp_highlight_rules")
 *     require("../token_iterator")          // beautify.js / heddle_completions.js
 *
 * Those files do NOT exist in this repository - they only exist in
 * ajaxorg/ace. At bundle time `build_ace.sh` copies `js/src/**` into a cloned
 * Ace checkout (pinned to v1.32.6) so the relative requires resolve. We do not
 * want to run that whole webpack bundle just to unit-test the artifacts.
 *
 * CHOSEN APPROACH  (option (a)+(c) from the WS9 brief)
 * ---------------------------------------------------
 * Add a real dev dependency on `ace-code@1.32.6` - the npm package that ships
 * Ace's `src/` modules and is version-locked to the SAME Ace pin the bundle
 * uses (D-C). Then install a tiny `Module._resolveFilename` hook that ONLY
 * fires when a file located under `js/src/**` asks for a relative module that
 * Node cannot resolve natively. In that case we remap the request into
 * `ace-code/src/**`, preserving the mirrored directory layout (js/src mirrors
 * Ace's src/: `js/src/mode` <-> `ace-code/src/mode`, `js/src/ext` <->
 * `ace-code/src/ext`, ...).
 *
 * This was preferred over hand-vendoring Ace source (option (b)) because the
 * highlight-rules dependency graph (html -> css/javascript/xml/...) is large,
 * and over a blanket require map because the try-native-first fallback means
 * Heddle's OWN submodules (which DO exist in `js/src`) keep resolving to the
 * repo copies - only genuinely-missing Ace internals are redirected.
 */

const Module = require("node:module");
const path = require("node:path");

const jsSrcDir = path.resolve(__dirname, "..", "..", "src");
// require.resolve("ace-code") -> <pkg>/src/ace.js ; dirname -> <pkg>/src
const aceSrcDir = path.dirname(require.resolve("ace-code"));

const origResolveFilename = Module._resolveFilename;
let installed = false;

function isUnder(file, dir) {
    const rel = path.relative(dir, file);
    return rel !== "" && !rel.startsWith("..") && !path.isAbsolute(rel);
}

/**
 * Installs the resolver hook. Idempotent - safe to call from every helper.
 */
function installAceResolver() {
    if (installed) {
        return;
    }
    installed = true;

    Module._resolveFilename = function (request, parent, isMain, options) {
        try {
            return origResolveFilename.call(this, request, parent, isMain, options);
        } catch (err) {
            const parentFile = parent && parent.filename;
            const isRelative = typeof request === "string" && request[0] === ".";

            if (parentFile && isRelative && isUnder(parentFile, jsSrcDir)) {
                // Re-anchor the request as if the requiring file lived at the
                // mirrored location inside ace-code/src/**.
                const relDir = path.relative(jsSrcDir, path.dirname(parentFile));
                const mappedDir = path.join(aceSrcDir, relDir);
                const mapped = path.resolve(mappedDir, request);
                return origResolveFilename.call(this, mapped, parent, isMain, options);
            }
            throw err;
        }
    };
}

/**
 * Require a Heddle editor source module by its path relative to `js/src`
 * (without extension), e.g. `requireHeddleModule("mode/heddle_highlight_rules")`.
 * The Ace resolver hook is installed first so any Ace-internal requires the
 * module makes are satisfied from `ace-code`.
 * @param {string} relPath
 * @returns {*}
 */
function requireHeddleModule(relPath) {
    installAceResolver();
    return require(path.join(jsSrcDir, relPath));
}

/**
 * Require an Ace-internal module from the pinned `ace-code` package, e.g.
 * `requireAceModule("tokenizer")` or `requireAceModule("edit_session")`.
 * @param {string} relPath  path relative to ace-code/src (no extension)
 * @returns {*}
 */
function requireAceModule(relPath) {
    installAceResolver();
    return require(path.join(aceSrcDir, relPath));
}

module.exports = {
    installAceResolver,
    requireHeddleModule,
    requireAceModule,
    jsSrcDir,
    aceSrcDir
};
