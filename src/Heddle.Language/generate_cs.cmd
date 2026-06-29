@echo off
setlocal
set "ANTLR_JAR=antlr-4.13.1-complete.jar"
if not exist "%ANTLR_JAR%" curl -fSL -o "%ANTLR_JAR%" "https://www.antlr.org/download/%ANTLR_JAR%"
java -jar "%ANTLR_JAR%" -Dlanguage=CSharp "HeddleLexer.g4" "HeddleParser.g4" -o "generated" -lib "generated" -package Heddle.Language
rem java -jar "%ANTLR_JAR%" -Dlanguage=CSharp "HeddleLexerNoWS.g4" -o "generated" -lib "generated" -package Heddle.Language
