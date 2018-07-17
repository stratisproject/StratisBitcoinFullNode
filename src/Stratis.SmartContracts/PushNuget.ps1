rm "bin\release\" -Recurse -Force
dotnet pack --configuration Release  
dotnet nuget push bin\Release\Stratis.SmartContracts.*.nupkg -k  -s https://api.nuget.org/v3/index.json