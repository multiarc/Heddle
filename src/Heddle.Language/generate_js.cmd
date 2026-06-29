@echo off
setlocal
set "ANTLR_JAR=antlr-4.13.1-complete.jar"
if not exist "%ANTLR_JAR%" curl -fSL -o "%ANTLR_JAR%" "https://www.antlr.org/download/%ANTLR_JAR%"
java -jar "%ANTLR_JAR%" -Dlanguage=JavaScript "HeddleLexer.g4" "HeddleParser.g4" -o "js" -lib "js" -package Heddle.Language
