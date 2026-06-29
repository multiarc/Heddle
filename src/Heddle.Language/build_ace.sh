#!/bin/bash

mkdir ace_build
cd ace_build
git clone --depth 1 --branch v1.32.6 https://github.com/ajaxorg/ace.git
cd ace
npm install
npm install antlr4@4.13.1
cp -R ../../js/src/ ./
cp -R ./node_modules/antlr4/src/antlr4/ ./src/mode/heddle/
cp ../../js/fs.js ./src/mode/heddle/antlr4/
find ./src/mode/heddle/antlr4/ -iname "*.js" -exec sed -i -r "s#import fs from 'fs'#import fs from \"\./fs\"#g" {} +
sed -i -r "s#target:\s*\"ES5\"#target: \"ES2015\"#g" Makefile.dryice.js
node ./Makefile.dryice.js -nc

# Ship license + attribution inside the published npm package (its root is ./build).
# The bundle embeds Ace and the ANTLR4 JS runtime, both BSD-3-Clause; their license
# texts must travel with the redistribution.
mkdir -p ./build
cp ../../../../LICENSE ./build/LICENSE
{
  echo "# Third-Party Notices for @multiarc/ace_heddle"
  echo
  echo "This package is a custom build of the Ace editor that embeds the ANTLR 4"
  echo "JavaScript runtime. The Heddle editor-mode code is licensed under Apache-2.0"
  echo "(see LICENSE). It bundles the following third-party components:"
  echo
  echo "## Ace editor (BSD-3-Clause) — https://github.com/ajaxorg/ace"
  echo
  cat ./LICENSE
  echo
  echo "## ANTLR 4 JavaScript runtime (BSD-3-Clause) — https://github.com/antlr/antlr4"
  echo
  cat ./node_modules/antlr4/LICENSE 2>/dev/null || echo "See https://github.com/antlr/antlr4/blob/master/LICENSE.txt"
} > ./build/THIRD-PARTY-NOTICES.md