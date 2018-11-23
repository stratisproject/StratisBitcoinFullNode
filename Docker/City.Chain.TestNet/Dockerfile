FROM microsoft/dotnet:2.1-aspnetcore-runtime

VOLUME /root/.citychain

WORKDIR /usr/local/app/

RUN apt-get update \
    && apt-get install -y curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN curl -Ls https://github.com/CityChainFoundation/city-chain/releases/download/v1.0.10/City.Chain-1.0.10-linux-x64.tar.gz \
    | tar -xvz -C .

COPY city.conf.docker /root/.citychain/city/CityTest/city.conf

EXPOSE 24333 24334 24335 24336

ENTRYPOINT ["dotnet", "City.Chain.dll", "-testnet"]
