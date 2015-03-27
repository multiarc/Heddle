cd src
dnu restore
cd Templates.Language.K
dnu build
cd ..\Templates.Native.K
dnu build
cd ..\Templates.K
dnu build
cd ..\Templates.Tests.K
dnu build
dnx . test
cd ..\Templates.K
dnu pack --out ..\..\pack
cd ..\Templates.Language.K
dnu pack --out ..\..\pack
cd ..\Templates.Native.K
dnu pack --out ..\..\pack