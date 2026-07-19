/*
 * Node module-customization hooks (registered via module.register) that let the
 * WS5 harness load the Heddle ANTLR parse pipeline under node:test.
 *
 * WHY THIS EXISTS
 * ---------------
 * The parse-pipeline sources under js/src/mode/heddle/ (DocumentParser.js,
 * ParseContext.js, HeddleErrorListener.js, HeddleLexer(Extended).js,
 * HeddleParser(Extended).js, HeddleParserListener.js) are ES modules. They are
 * authored to live inside the Ace build, where build_ace.sh copies the ANTLR4
 * JS runtime to js/src/mode/heddle/antlr4/ and a bundler resolves the imports.
 * Two problems block loading them directly in Node:
 *
 *   1. They `import ... from "./antlr4/index.web"` (and one deep
 *      "./antlr4/error/LexerNoViableAltException"), a folder that only exists at
 *      build time. We re-anchor those specifiers onto the installed
 *      `antlr4@4.13.1` package's ES source tree (node_modules/antlr4/src/antlr4),
 *      whose package is `type: module`, so a single ANTLR module graph is shared
 *      (keeping `instanceof` checks in ParseContext valid).
 *
 *   2. The package.json for these sources is `type: commonjs`, so Node would
 *      parse the `import`/`export` syntax as CommonJS. The `load` hook forces
 *      `format: "module"` for the heddle-dir sources, and the `resolve` hook
 *      appends the `.js` extension to their extensionless sibling imports.
 *
 * This is test-only glue; production loading is handled by the Ace bundle.
 */

import { pathToFileURL, fileURLToPath } from "node:url";
import { createRequire } from "node:module";
import path from "node:path";
import fs from "node:fs";

const require = createRequire(import.meta.url);

// require.resolve("antlr4") -> <pkg>/dist/antlr4.node.cjs (the only exported
// subpath); the ES source tree we want sits at <pkg>/src/antlr4.
const antlrPkgRoot = path.resolve(path.dirname(require.resolve("antlr4")), "..");
const antlrSrcDir = path.join(antlrPkgRoot, "src", "antlr4");
const antlrIndexWeb = pathToFileURL(path.join(antlrSrcDir, "index.web.js")).href;
const antlrLexerNoViable = pathToFileURL(
    path.join(antlrSrcDir, "error", "LexerNoViableAltException.js")
).href;

// Directory holding the Heddle ESM parse sources.
const heddleDir = fileURLToPath(new URL("../../src/mode/heddle/", import.meta.url));

function isUnderHeddleDir(filePath) {
    const rel = path.relative(heddleDir, filePath);
    return rel !== "" && !rel.startsWith("..") && !path.isAbsolute(rel);
}

export async function resolve(specifier, context, nextResolve) {
    if (specifier.endsWith("/antlr4/index.web")) {
        return { url: antlrIndexWeb, shortCircuit: true };
    }
    if (specifier.endsWith("/antlr4/error/LexerNoViableAltException")) {
        return { url: antlrLexerNoViable, shortCircuit: true };
    }
    const parentUrl = context.parentURL;
    if (
        parentUrl &&
        parentUrl.startsWith("file:") &&
        specifier.startsWith(".") &&
        !specifier.endsWith(".js") &&
        isUnderHeddleDir(fileURLToPath(parentUrl))
    ) {
        return {
            url: new URL(specifier + ".js", parentUrl).href,
            format: "module",
            shortCircuit: true
        };
    }
    return nextResolve(specifier, context);
}

export async function load(url, context, nextLoad) {
    if (url.startsWith("file:")) {
        const filePath = fileURLToPath(url);
        if (filePath.endsWith(".js") && isUnderHeddleDir(filePath)) {
            return {
                format: "module",
                source: fs.readFileSync(filePath, "utf8"),
                shortCircuit: true
            };
        }
    }
    return nextLoad(url, context);
}
