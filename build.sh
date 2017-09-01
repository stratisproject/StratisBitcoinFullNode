#!/bin/bash
dotnet --info
echo STARTED dotnet restore
dotnet restore -v m
echo STARTED dotnet build
dotnet build -c Release ${path} -v m
echo STARTED dotnet test
dotnet test -c Release ./Stratis.Bitcoin.Tests/Stratis.Bitcoin.Tests.csproj -v m
dotnet test -c Release ./Stratis.Bitcoin.Features.IndexStore.Tests/Stratis.Bitcoin.Features.IndexStore.Tests.csproj -v m
dotnet test -c Release ./Stratis.Bitcoin.Features.Wallet.Tests/Stratis.Bitcoin.Features.Wallet.Tests.csproj -v m
dotnet test -c Release ./Stratis.Bitcoin.Features.WatchOnlyWallet.Tests/Stratis.Bitcoin.Features.WatchOnlyWallet.Tests.csproj -v m
dotnet test -c Release ./Stratis.Bitcoin.Features.RPC.Tests/Stratis.Bitcoin.Features.RPC.Tests.csproj -v m
