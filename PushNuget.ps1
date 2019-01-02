
# BASE PROJECTS
rm "src\NBitcoin\bin\debug\" -Recurse -Force
dotnet pack src\NBitcoin --configuration Debug --include-source --include-symbols 
dotnet nuget push "src\NBitcoin\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin --configuration Debug --include-source --include-symbols 
dotnet nuget push "src\Stratis.Bitcoin\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

#FEATURES PROJECTS
rm "src\Stratis.Bitcoin.Features.Api\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Api --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.Api\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.BlockStore\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.BlockStore --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.BlockStore\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Consensus\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Consensus --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.Consensus\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Dns\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Dns --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.Dns\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.LightWallet\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.LightWallet --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.LightWallet\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.MemoryPool\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.MemoryPool --configuration Debug --include-source --include-symbols 
dotnet nuget push "src\Stratis.Bitcoin.Features.MemoryPool\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Miner\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Miner --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.Miner\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.PoA\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.PoA --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.PoA\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Notifications\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Notifications --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.Notifications\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.RPC\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.RPC --configuration Debug --include-source --include-symbols
dotnet nuget push "src\Stratis.Bitcoin.Features.RPC\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.Wallet\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.Wallet --configuration Debug --include-source --include-symbols  
dotnet nuget push "src\Stratis.Bitcoin.Features.Wallet\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.WatchOnlyWallet\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.WatchOnlyWallet --configuration Debug --include-source --include-symbols
dotnet nuget push "src\Stratis.Bitcoin.Features.WatchOnlyWallet\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Networks\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Networks --configuration Debug --include-source --include-symbols
dotnet nuget push "src\Stratis.Bitcoin.Networks\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

# TESTS PROJECTS
rm "src\Stratis.Bitcoin.IntegrationTests.Common\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.IntegrationTests.Common --configuration Debug --include-source --include-symbols
dotnet nuget push "src\Stratis.Bitcoin.IntegrationTests.Common\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Tests.Common\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Tests.Common --configuration Debug --include-source --include-symbols 
dotnet nuget push "src\Stratis.Bitcoin.Tests.Common\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Tests.Wallet.Common\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Tests.Wallet.Common --configuration Debug --include-source --include-symbols 
dotnet nuget push "src\Stratis.Bitcoin.Tests.Wallet.Common\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

rm "src\Stratis.Bitcoin.Features.PoA.IntegrationTests.Common\bin\debug\" -Recurse -Force
dotnet pack src\Stratis.Bitcoin.Features.PoA.IntegrationTests.Common --configuration Debug --include-source --include-symbols 
dotnet nuget push "src\Stratis.Bitcoin.Features.PoA.IntegrationTests.Common\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

# TOOLS PROJECTS

rm "src\FodyNlogAdapter\bin\debug\" -Recurse -Force
dotnet pack src\FodyNlogAdapter --configuration Debug --include-source --include-symbols 
dotnet nuget push "src\FodyNlogAdapter\bin\debug\*.symbols.nupkg" --source "https://api.nuget.org/v3/index.json"

PAUSE