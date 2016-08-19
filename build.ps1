Push-Location src
dotnet restore
Push-Location Templates.Tests
dotnet test
Pop-Location
Push-Location Templates
dotnet pack -o ..\..\packages --configuration Release
Pop-Location
Push-Location Templates.Language
dotnet pack -o ..\..\packages --configuration Release
Pop-Location
Push-Location Antlr4Core.Runtime
dotnet pack -o ..\..\packages --configuration Release
Pop-Location
Push-Location Templates.Mvc
dotnet pack -o ..\..\packages --configuration Release
Pop-Location
Pop-Location