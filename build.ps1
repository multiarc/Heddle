Push-Location src
dnu restore --parallel
Push-Location Templates.Tests
dnu build --configuration Release
dnvm run default -r clr -arch x64 . test
dnvm run default -r coreclr -arch x64 . test
Pop-Location
Push-Location Templates.Language
dnu pack --out ..\..\packages --configuration Release
Pop-Location
Push-Location Templates
dnu pack --out ..\..\packages --configuration Release
Pop-Location
Pop-Location