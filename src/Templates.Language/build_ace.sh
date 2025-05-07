#!/usr/bin/env bash

mkdir ace_build
(cd ./ace_build ; git clone --depth 1 --branch v1.32.6 https://github.com/ajaxorg/ace.git)
(cd ./ace_build/ace ; npm install)
(cd ./ace_build/ace ; npm install antlr4@4.13.1)
cp -R ./js/src/ ./
( cd ace_build/ace ; cp -R ./node_modules/antlr4/src/antlr4/ ./ace_build/ace/src/mode/tts/ )
cp ./js/fs.js ./ace_build/ace/src/mode/tts/antlr4/
( cd ace_build/ace ; find ./src/mode/tts/antlr4/ -iname "*.js" -exec sed -i -r "s#import fs from 'fs'#import fs from \"\./fs\"#g" {} + )
( cd ace_build/ace ; sed -i -r "s#target:\s*\"ES5\"#target: \"ES2015\"#g" Makefile.dryice.js )
( cd ace_build/ace ; node ./ace_build/ace/Makefile.dryice.js -nc )