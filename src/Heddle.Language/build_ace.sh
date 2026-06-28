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