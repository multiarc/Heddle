dotnet restore
Push-Location src
Push-Location Heddle.Tests
dotnet test
Pop-Location
Push-Location Heddle
dotnet pack -o ..\..\packages --configuration Release
Pop-Location
Push-Location Heddle.Language
dotnet pack -o ..\..\packages --configuration Release
Pop-Location
Push-Location Heddle.Mvc
dotnet pack -o ..\..\packages --configuration Release
Pop-Location
Pop-Location