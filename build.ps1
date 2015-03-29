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
Get-ChildItem "pack\Release\" *.nupkg -rec -erroraction 'silentlycontinue' | Select-Object -Expand FullName | Foreach { Write-Host `#`#teamcity`[publishArtifacts `'$_`'`] }