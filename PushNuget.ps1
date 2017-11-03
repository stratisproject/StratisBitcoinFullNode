
rm "src\Stratis.Bitcoin\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Api\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Api --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.Api\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.BlockStore\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.BlockStore --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.BlockStore\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Consensus\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Consensus --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.Consensus\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.LightWallet\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.LightWallet --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.LightWallet\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.MemoryPool\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.MemoryPool --configuration Release 
dotnet nuget push "src\Stratis.Bitcoin.Features.MemoryPool\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Miner\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Miner --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.Miner\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Notifications\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Notifications --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.Notifications\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.RPC\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.RPC --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.RPC\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Wallet\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Wallet --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.Wallet\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.WatchOnlyWallet\bin\release\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.WatchOnlyWallet --configuration Release  
dotnet nuget push "src\Stratis.Bitcoin.Features.WatchOnlyWallet\bin\Release\*.nupkg" --source "https://api.nuget.org/v3/index.json"