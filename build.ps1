Push-Location src
dotnet restore
Push-Location Templates.Tests
dotnet test
Pop-Location
Push-Location Templates
dotnet pack -o ..\..\packages\Templates --configuration Release
Pop-Location
Push-Location Templates.Language
dotnet pack -o ..\..\packages\Templates.Language --configuration Release
Pop-Location
Push-Location Antlr4.Runtime
dotnet pack -o ..\..\packages\Antlr4.Runtime --configuration Release
Pop-Location
Push-Location Templates.Mvc
dotnet pack -o ..\..\packages\Templates.Mvc --configuration Release
Pop-Location
Pop-Location