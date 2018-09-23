FROM microsoft/dotnet:2.1-aspnetcore-runtime

VOLUME /root/.citychain

WORKDIR /usr/local/app/

RUN apt-get update \
    && apt-get install -y curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN curl -Ls https://github.com/CityChainFoundation/city-chain/releases/download/v0.0.26/City.Chain-0.0.26-linux-x64.tar.gz \
    | tar -xvz -C .

COPY city.conf.docker /root/.citychain/city/CityMain/city.conf

EXPOSE 4333 4334 4335 4336

ENTRYPOINT ["dotnet", "City.Chain.dll"]