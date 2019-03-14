rm "bin\release\" -Recurse -Force
dotnet pack --configuration Release
dotnet nuget push "bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

