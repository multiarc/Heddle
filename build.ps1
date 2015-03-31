Push-Location src
dnu restore --parallel
Push-Location Templates.Tests
dnu build --configuration Release
dnx . test
Pop-Location
Push-Location Templates.Language
dnu pack --out ..\..\packages --configuration Release
Pop-Location
Push-Location Templates
dnu pack --out ..\..\packages --configuration Release
Pop-Location
Pop-Location
.\tools\nuget\nuget.exe push packages\Release\Templates.Language.1.0.0.nupkg 351e721d-773c-4de3-9583-119c28829995 -Source https://www.myget.org/F/antlrcs/
.\tools\nuget\nuget.exe push packages\Release\Templates.2.0.0.nupkg 351e721d-773c-4de3-9583-119c28829995 -Source https://www.myget.org/F/antlrcs/