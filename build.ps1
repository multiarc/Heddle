Push-Location src
dnu restore
Push-Location Templates.Tests
dnu build --configuration Release
dnx . test
Pop-Location
Push-Location Templates.Language
dnu pack --out ..\..\pack --configuration Release
Pop-Location
Push-Location Templates
dnu pack --out ..\..\pack --configuration Release
Pop-Location
Pop-Location