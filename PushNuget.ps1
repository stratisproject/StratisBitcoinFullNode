
#FEATURES PROJECTS
rm "src\Stratis.FederatedPeg.Features.FederationGateway\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.FederatedPeg.Features.FederationGateway --configuration Debug --include-source --include-symbols
dotnet nuget push "src\Stratis.FederatedPeg.Features.FederationGateway\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"
