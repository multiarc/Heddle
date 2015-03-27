Push-Location src
dnu restore
Push-Location Templates.Language.K
dnu build --configuration Release
Pop-Location
Push-Location Templates.Native.K
dnu build --configuration Release
Pop-Location
Push-Location Templates.K
dnu build --configuration Release
Pop-Location
Push-Location Templates.Tests.K
dnu build --configuration Release
dnx . test
Pop-Location
Push-Location Templates.K
dnu pack --out ..\..\pack --configuration Release
Pop-Location
Push-Location Templates.Language.K
dnu pack --out ..\..\pack --configuration Release
Pop-Location
Push-Location Templates.Native.K
dnu pack --out ..\..\pack --configuration Release
Pop-Location
Pop-Location