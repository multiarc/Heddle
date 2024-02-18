#!/bin/bash

mkdir ace_build
cd ace_build
git clone --depth 1 --branch v1.32.6 https://github.com/ajaxorg/ace.git
cd ace
npm install
npm install antlr4@4.13.1
cp -R ../../js/src/ ./
node ./Makefile.dryice.js -nc
cp -R ./build/src-noconflict/ ../