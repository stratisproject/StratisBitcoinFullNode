FROM mcr.microsoft.com/dotnet/core/aspnet:2.1 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:2.1 AS build
WORKDIR /src
COPY ["City.Chain/City.Chain.csproj", "City.Chain/"]
COPY ["Stratis.Bitcoin/Stratis.Bitcoin.csproj", "Stratis.Bitcoin/"]
COPY ["NBitcoin/NBitcoin.csproj", "NBitcoin/"]
COPY ["Stratis.Bitcoin.Features.ColdStaking/Stratis.Bitcoin.Features.ColdStaking.csproj", "Stratis.Bitcoin.Features.ColdStaking/"]
COPY ["Stratis.Bitcoin.Features.Wallet/Stratis.Bitcoin.Features.Wallet.csproj", "Stratis.Bitcoin.Features.Wallet/"]
COPY ["Stratis.Bitcoin.Features.MemoryPool/Stratis.Bitcoin.Features.MemoryPool.csproj", "Stratis.Bitcoin.Features.MemoryPool/"]
COPY ["Stratis.Bitcoin.Features.Consensus/Stratis.Bitcoin.Features.Consensus.csproj", "Stratis.Bitcoin.Features.Consensus/"]
COPY ["Stratis.Bitcoin.Features.RPC/Stratis.Bitcoin.Features.RPC.csproj", "Stratis.Bitcoin.Features.RPC/"]
COPY ["Stratis.Bitcoin.Features.BlockStore/Stratis.Bitcoin.Features.BlockStore.csproj", "Stratis.Bitcoin.Features.BlockStore/"]
COPY ["Stratis.Bitcoin.Features.Notifications/Stratis.Bitcoin.Features.Notifications.csproj", "Stratis.Bitcoin.Features.Notifications/"]
COPY ["Stratis.Bitcoin.Features.Dns/Stratis.Bitcoin.Features.Dns.csproj", "Stratis.Bitcoin.Features.Dns/"]
COPY ["Stratis.Bitcoin.Features.Miner/Stratis.Bitcoin.Features.Miner.csproj", "Stratis.Bitcoin.Features.Miner/"]
COPY ["Stratis.Bitcoin.Networks/Stratis.Bitcoin.Networks.csproj", "Stratis.Bitcoin.Networks/"]
COPY ["City/City.csproj", "City/"]
COPY ["Stratis.Bitcoin.Features.Api/Stratis.Bitcoin.Features.Api.csproj", "Stratis.Bitcoin.Features.Api/"]
COPY ["Stratis.Bitcoin.Features.Apps/Stratis.Bitcoin.Features.Apps.csproj", "Stratis.Bitcoin.Features.Apps/"]
RUN dotnet restore "City.Chain/City.Chain.csproj"
COPY . .
WORKDIR "/src/City.Chain"
RUN dotnet build "City.Chain.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "City.Chain.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
EXPOSE 4333 4334 4335 4336
ENTRYPOINT ["dotnet", "City.Chain.dll", "-apiuri=http://0.0.0.0:34335"]