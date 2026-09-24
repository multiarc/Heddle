// Post-package check of what a VSIX actually ships, run by CI on the unzipped package's extension/
// directory. vsce refuses only a missing entry point; a bundle that is present but unusable - left with a
// require of a package the VSIX does not carry, say - installs cleanly and throws at activation on the
// user's machine. So this loads the shipped bundle the way the host would, against a stub of the host's
// own vscode module, and checks that the dependencies ship inside it, with their license texts.
const fs = require('fs');
const path = require('path');
const Module = require('module');

const root = path.resolve(process.argv[2] || '');
const failures = [];

function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = path.join(dir, e.name);
    return e.isDirectory() ? walk(full) : [path.relative(root, full).split(path.sep).join('/')];
  });
}

if (!fs.existsSync(path.join(root, 'package.json'))) {
  console.error(`Not an unzipped VSIX extension directory: ${root}`);
  process.exit(1);
}
// Unzipped under a project, a require the VSIX cannot satisfy would resolve from that project's
// node_modules and the load below would pass for a package that throws on every user's machine.
// NODE_PATH and node's global folders would do the same from anywhere.
const reachable = Module._nodeModulePaths(path.dirname(root)).concat(Module.globalPaths)
  .filter((p) => fs.existsSync(p));
if (reachable.length > 0) {
  console.error(`Unzip the VSIX outside any project, with no NODE_PATH or global node folders: requires would resolve from ${reachable[0]}`);
  process.exit(1);
}

// The language server under server/ is .NET and not this check's business.
const files = walk(root).filter((f) => !f.startsWith('server/'));
const main = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8')).main.replace(/^\.\//, '');

if (files.some((f) => f.split('/').includes('node_modules'))) {
  failures.push('node_modules/ ships: the dependency tree is loose files again, not the bundle');
}
const scripts = files.filter((f) => /\.[cm]?js$/.test(f));
if (scripts.length !== 1 || scripts[0] !== main) {
  failures.push(`expected ${main} to be the only JavaScript file, found: ${scripts.join(', ')}`);
}

// Every property of the stub is another stub, callable and constructible, so module-level code such as
// `class X extends vscode.CompletionItem` evaluates. It is never a thenable and never an ES module.
const opaque = (key) => key === 'then' || key === '__esModule' || typeof key === 'symbol';
function stub() {
  return new Proxy(function () {}, {
    get: (target, key) => (key === 'prototype' ? target.prototype : opaque(key) ? undefined : stub()),
    apply: () => stub(),
    construct: () => stub()
  });
}
// esbuild's ESM interop (`var vscode = __toESM(require("vscode"))`) copies a module's OWN properties into
// the namespace it hands the extension code, so every member read through a variable bound that way has to
// exist up front; anything else still answers with a stub. The dependencies' tsc-emitted __importStar
// copies own properties too, unless the module is __esModule.
const bundleText = fs.readFileSync(path.join(root, main), 'utf8');
const host = { __esModule: true };
for (const binding of bundleText.matchAll(/([A-Za-z_$][\w$]*)\s*=\s*__toESM\(require\("vscode"\)/g)) {
  const escaped = binding[1].replace(/\$/g, '\\$');
  for (const m of bundleText.matchAll(new RegExp(`(?<![\\w$.])${escaped}\\.([A-Za-z_$][\\w$]*)`, 'g'))) host[m[1]] = stub();
}
const vscode = new Proxy(host, {
  get: (target, key) => (key in target ? target[key] : opaque(key) ? undefined : stub())
});
const load = Module._load;
Module._load = function (request, ...rest) {
  return request === 'vscode' ? vscode : load.call(this, request, ...rest);
};
try {
  const exported = Object.keys(require(path.join(root, main))).sort().join(',');
  if (exported !== 'activate,deactivate') {
    failures.push(`${main} exports ${exported || 'nothing'}, not activate,deactivate`);
  }
} catch (error) {
  failures.push(`${main} throws when the extension host loads it: ${error.message}`);
} finally {
  Module._load = load;
}

// esbuild marks each bundled module with its source path; every package named there - the innermost
// one, for a copy npm nested under another package - must have its license text in the notices file
// that ships beside the bundle.
const noticesPath = path.join(root, path.dirname(main), 'ThirdPartyNotices.txt');
const bundled = new Set();
for (const m of bundleText.matchAll(/^\/\/ (?:.*\/)?node_modules\/((?:@[^/]+\/)?[^/]+)\//gm)) {
  bundled.add(m[1]);
}
if (bundled.size === 0) {
  failures.push(`${main} carries no // node_modules/ module markers (minified?), so its notices cannot be checked`);
} else if (!fs.existsSync(noticesPath)) {
  failures.push(`${[...bundled].join(', ')} ship inside ${main} with no ThirdPartyNotices.txt beside it`);
} else {
  // bundle.js writes each package as an 80-'=' rule, "<name> <version> (<license>)", a rule, then its
  // texts. Sections are found by that whole three-line heading, so a license text's own '=' underlines
  // cannot shift them.
  const noticesText = fs.readFileSync(noticesPath, 'utf8');
  const headings = [...noticesText.matchAll(/^={80}\r?\n((?:@[^/\s]+\/)?\S+) \S+ \([^\n]*\)\r?\n={80}\r?$/gm)];
  const texts = new Map(headings.map((h, i) => [h[1],
    noticesText.slice(h.index + h[0].length, i + 1 < headings.length ? headings[i + 1].index : undefined).trim()]));
  const missing = [...bundled].filter((p) => !texts.get(p));
  if (missing.length > 0) {
    failures.push(`bundled without their license text in ThirdPartyNotices.txt: ${missing.join(', ')}`);
  }
}

// A sourceMappingURL naming a file the VSIX does not carry sends a debugger after nothing.
const mapUrl = /^\/\/# sourceMappingURL=(\S+)\s*$/m.exec(bundleText);
if (mapUrl && !mapUrl[1].startsWith('data:') && !files.includes(path.posix.join(path.posix.dirname(main), mapUrl[1]))) {
  failures.push(`${main} names a source map the VSIX does not ship: ${mapUrl[1]}`);
}

if (failures.length > 0) {
  for (const f of failures) console.error(`VSIX check failed: ${f}`);
  process.exit(1);
}
console.log(`VSIX ok: ${main} loads and exports activate,deactivate; ${bundled.size} bundled packages, all with notices`);
