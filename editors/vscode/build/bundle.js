// Build-time bundle of the extension entry point into a single dist/extension.js, which is what the
// extension host loads at activation. Unbundled, the nine packages behind vscode-languageclient ship as
// loose files in every per-target VSIX. esbuild reads src/extension.ts directly, so CI's stamped
// PINNED_VERSION is consumed straight from source. tsconfig.json is non-emitting: dist/ is this script's.
const esbuild = require('esbuild');
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const watch = process.argv.includes('--watch');
const notices = path.join(root, 'dist', 'ThirdPartyNotices.txt');

// esbuild keeps only /*! and @license comments, and none of the bundled packages carries one, so their
// license texts - which shipped beside them in node_modules before bundling - would leave the VSIX. The
// packages the metafile says went into the bundle get their texts written next to it instead.
const legalFile = /^(licen[cs]e|copying|thirdpartynotices)(\.[a-z]+)?$/i;

function writeNotices(metafile) {
  const dirs = new Set();
  for (const input of Object.keys(metafile.inputs)) {
    const m = /^(.*node_modules\/(?:@[^/]+\/)?[^/]+)\//.exec(input);
    if (m) dirs.add(m[1]);
  }

  const sections = [...dirs].sort().map((dir) => {
    const pkg = JSON.parse(fs.readFileSync(path.join(root, dir, 'package.json'), 'utf8'));
    const files = fs.readdirSync(path.join(root, dir)).filter((f) => legalFile.test(f)).sort();
    if (!files.some((f) => /^licen[cs]e/i.test(f))) {
      throw new Error(`No license file in ${dir}; its text cannot ship in ${path.basename(notices)}`);
    }
    const texts = files.map((f) => fs.readFileSync(path.join(root, dir, f), 'utf8').trim());
    if (texts.some((t) => t.length === 0)) {
      throw new Error(`An empty license file in ${dir}; its text cannot ship in ${path.basename(notices)}`);
    }
    const rule = '='.repeat(80);
    return `${rule}\n${pkg.name} ${pkg.version} (${pkg.license})\n${rule}\n\n${texts.join('\n\n')}\n`;
  });

  fs.writeFileSync(notices,
    'Third-party software bundled into dist/extension.js, with the license texts it ships under.\n\n' +
    sections.join('\n'));
}

const options = {
  entryPoints: [path.join(root, 'src', 'extension.ts')],
  outfile: path.join(root, 'dist', 'extension.js'),
  absWorkingDir: root,
  bundle: true,
  external: ['vscode'],
  platform: 'node',
  format: 'cjs',
  target: 'node20',
  minify: false,
  // Maps for the F5 loop only: .vscodeignore excludes dist/**/*.map, and a shipped sourceMappingURL naming a
  // file the VSIX does not carry is worse than none, so the packaged build has none.
  sourcemap: watch,
  metafile: true,
  plugins: [{
    name: 'third-party-notices',
    setup(build) {
      build.onEnd((result) => {
        if (result.errors.length > 0) return undefined;
        try {
          writeNotices(result.metafile);
          return undefined;
        } catch (error) {
          return { errors: [{ text: error.message }] };
        }
      });
    }
  }]
};

async function run() {
  if (watch) {
    const context = await esbuild.context(options);
    await context.watch();
    console.log(`Watching → ${options.outfile}`);
    return;
  }

  await esbuild.build(options);
  console.log(`Bundled → ${options.outfile} (+ ${path.basename(notices)})`);
}

run().catch((error) => {
  console.error(error);
  process.exit(1);
});
