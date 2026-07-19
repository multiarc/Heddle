// propagate_js.js — deterministic propagation of the ANTLR-generated JS grammar
// from the root `js/` output into the Ace mode copy under `js/src/mode/heddle/`.
//
// The root files (js/HeddleLexer.js, js/HeddleParser.js, js/HeddleParserListener.js)
// are the canonical ANTLR output. The Ace bundle consumes copies under
// js/src/mode/heddle/ that differ ONLY by module-format shims:
//   1. a leading `"use strict";` line, and
//   2. the antlr4 runtime import rewritten to the vendored web build.
//
// This script applies those transforms and writes ONLY the three generated files.
// It NEVER touches the hand-written wrappers (HeddleLexerExtended.js,
// HeddleParserExtended.js, HeddleTokenizer.js, HeddleErrorListener.js,
// DocumentParser.js, ParseContext.js) or the antlr4/ runtime directory.
//
// It is idempotent: running it repeatedly on an up-to-date tree produces no change.

"use strict";

const fs = require("fs");
const path = require("path");

const SRC_DIR = path.join(__dirname, "js");
const DEST_DIR = path.join(__dirname, "js", "src", "mode", "heddle");

// The three generated files that must be propagated.
const GENERATED_FILES = [
  "HeddleLexer.js",
  "HeddleParser.js",
  "HeddleParserListener.js",
];

// Deterministic transforms applied to each generated file to produce the mode copy.
// Order matters only in that the "use strict" prepend must run once at the top.
const ROOT_ANTLR_IMPORT = "import antlr4 from 'antlr4';";
const MODE_ANTLR_IMPORT = 'import antlr4 from "./antlr4/index.web";';
const USE_STRICT_LINE = '"use strict";\n';

/**
 * Transform a root generated file's contents into the mode-copy form.
 * @param {string} contents Raw contents of the root generated file.
 * @param {string} fileName File name (for error messages).
 * @returns {string} The transformed contents for the mode copy.
 */
function toModeCopy(contents, fileName) {
  // 0. Normalize to LF so output is byte-identical regardless of the platform's
  //    autocrlf checkout behaviour. The committed form is LF (git text=auto),
  //    so this keeps propagation idempotent on Windows and Linux alike.
  let out = contents.replace(/\r\n/g, "\n");

  // 1. Rewrite the antlr4 runtime import to the vendored web build.
  if (!out.includes(ROOT_ANTLR_IMPORT)) {
    throw new Error(
      `${fileName}: expected antlr4 import line not found: ${ROOT_ANTLR_IMPORT}`
    );
  }
  out = out.replace(ROOT_ANTLR_IMPORT, MODE_ANTLR_IMPORT);

  // 2. Prepend the "use strict"; pragma (idempotent — skip if already present).
  if (!out.startsWith(USE_STRICT_LINE)) {
    out = USE_STRICT_LINE + out;
  }

  return out;
}

function main() {
  if (!fs.existsSync(DEST_DIR)) {
    throw new Error(`Destination directory does not exist: ${DEST_DIR}`);
  }

  let changed = 0;
  for (const fileName of GENERATED_FILES) {
    const srcPath = path.join(SRC_DIR, fileName);
    const destPath = path.join(DEST_DIR, fileName);

    if (!fs.existsSync(srcPath)) {
      throw new Error(`Generated source file missing (run ANTLR first): ${srcPath}`);
    }

    const srcContents = fs.readFileSync(srcPath, "utf8");
    const transformed = toModeCopy(srcContents, fileName);

    const existing = fs.existsSync(destPath)
      ? fs.readFileSync(destPath, "utf8")
      : null;

    if (existing === transformed) {
      console.log(`  unchanged: js/src/mode/heddle/${fileName}`);
      continue;
    }

    fs.writeFileSync(destPath, transformed);
    changed++;
    console.log(`  propagated: js/src/mode/heddle/${fileName}`);
  }

  console.log(
    changed === 0
      ? "propagate_js: mode copies already up to date (no changes)."
      : `propagate_js: updated ${changed} mode cop${changed === 1 ? "y" : "ies"}.`
  );
}

main();
