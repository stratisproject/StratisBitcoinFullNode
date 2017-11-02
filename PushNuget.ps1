
rm "Stratis.Bitcoin\bin\release\" -Recurse -Force
dotnet pack Stratis.Bitcoin --configuration Release  
dotnet nuget push "Stratis.Bitcoin\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "Stratis.Bitcoin.Features.BlockStore\bin\release\" -Recurse -Force
dotnet pack Stratis.Bitcoin.Features.BlockStore --configuration Release  
dotnet nuget push "Stratis.Bitcoin.Features.BlockStore\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "Stratis.Bitcoin.Features.Consensus\bin\release\" -Recurse -Force
dotnet pack Stratis.Bitcoin.Features.Consensus --configuration Release  
dotnet nuget push "Stratis.Bitcoin.Features.Consensus\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "Stratis.Bitcoin.Features.MemoryPool\bin\release\" -Recurse -Force
dotnet pack Stratis.Bitcoin.Features.MemoryPool --configuration Release 
dotnet nuget push "Stratis.Bitcoin.Features.MemoryPool\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "Stratis.Bitcoin.Features.Miner\bin\release\" -Recurse -Force
dotnet pack Stratis.Bitcoin.Features.Miner --configuration Release  
dotnet nuget push "Stratis.Bitcoin.Features.Miner\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "Stratis.Bitcoin.Features.RPC\bin\release\" -Recurse -Force
dotnet pack Stratis.Bitcoin.Features.RPC --configuration Release  
dotnet nuget push "Stratis.Bitcoin.Features.RPC\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"