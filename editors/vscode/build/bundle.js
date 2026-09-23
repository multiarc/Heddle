// Build-time bundle of the extension entry point into a single dist/extension.js, which is what the
// extension host loads at activation. Unbundled, the nine packages behind vscode-languageclient ship as
// loose files in every per-target VSIX. esbuild reads src/extension.ts directly, so CI's stamped
// PINNED_VERSION is consumed straight from source. tsconfig.json is non-emitting: dist/ is this script's.
const esbuild = require('esbuild');
const path = require('path');

const root = path.resolve(__dirname, '..');
const watch = process.argv.includes('--watch');

const options = {
  entryPoints: [path.join(root, 'src', 'extension.ts')],
  outfile: path.join(root, 'dist', 'extension.js'),
  bundle: true,
  external: ['vscode'],
  platform: 'node',
  format: 'cjs',
  target: 'node20',
  minify: false,
  sourcemap: false
};

async function run() {
  if (watch) {
    const context = await esbuild.context(options);
    await context.watch();
    console.log(`Watching → ${options.outfile}`);
    return;
  }

  await esbuild.build(options);
  console.log(`Bundled → ${options.outfile}`);
}

run().catch((error) => {
  console.error(error);
  process.exit(1);
});
