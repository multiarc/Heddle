// Build-time copy of the TextMate grammar from the single source of truth
// (docs/coloring-scheme/heddle.tmLanguage.json). The copy under syntaxes/ is never edited by hand.
const fs = require('fs');
const path = require('path');

const source = path.resolve(__dirname, '..', '..', '..', 'docs', 'coloring-scheme', 'heddle.tmLanguage.json');
const targetDir = path.resolve(__dirname, '..', 'syntaxes');
const target = path.join(targetDir, 'heddle.tmLanguage.json');

if (!fs.existsSync(source)) {
  console.error(`Grammar source not found: ${source}`);
  process.exit(1);
}

fs.mkdirSync(targetDir, { recursive: true });
fs.copyFileSync(source, target);
console.log(`Copied grammar → ${target}`);
