@echo off
rem Local regeneration of the JS ANTLR grammar. Run this after editing HeddleLexer.g4
rem or HeddleParser.g4 and COMMIT the regenerated js/** output. CI (docs.yml) reruns
rem the same generate+propagate and fails via `git diff --exit-code -- src/Heddle.Language/js`
rem if the checked-in files are stale (Decision D-D). Requires Java on PATH.
setlocal
set "ANTLR_JAR=antlr-4.13.1-complete.jar"
if not exist "%ANTLR_JAR%" curl -fSL -o "%ANTLR_JAR%" "https://www.antlr.org/download/%ANTLR_JAR%"
java -jar "%ANTLR_JAR%" -Dlanguage=JavaScript "HeddleLexer.g4" "HeddleParser.g4" -o "js" -lib "js" -package Heddle.Language || exit /b 1
rem Propagate the generated grammar into the Ace mode copy (js/src/mode/heddle/).
node propagate_js.js || exit /b 1
