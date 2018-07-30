FROM microsoft/dotnet:2.1-sdk

RUN git clone https://github.com/CityChainFoundation/city-chain.git \
    && cd /city-chain/src/City.Chain \
    && dotnet build
	
VOLUME /root/.citychain

WORKDIR /city-chain/src/City.Chain

COPY city.conf.docker /root/.citychain/city/CityTest/city.conf

EXPOSE 24333 24334 24335 24336

CMD ["dotnet", "run", "-testnet"]
