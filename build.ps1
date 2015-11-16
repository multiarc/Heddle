Push-Location src
dnu restore --parallel
Push-Location Templates.Tests
dnu build --configuration Release
dnx test
#dnvm use default -r coreclr -arch x64
#dnx test
dnvm use default -r clr -arch x64
Pop-Location
Push-Location Templates
dnu pack --out ..\..\packages\Templates --configuration Release
Pop-Location
Push-Location Templates.Language
dnu pack --out ..\..\packages\Templates.Language --configuration Release
Pop-Location
Push-Location Antlr4.Runtime
dnu pack --out ..\..\packages\Antlr4.Runtime --configuration Release
Pop-Location
Pop-Location