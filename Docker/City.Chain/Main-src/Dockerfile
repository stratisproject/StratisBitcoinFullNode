FROM microsoft/dotnet:2.1-sdk

RUN git clone https://github.com/CityChainFoundation/city-chain.git \
    && cd /city-chain/src/City.Chain \
    && dotnet build
	
VOLUME /root/.citychain

WORKDIR /city-chain/src/City.Chain

COPY city.conf.docker /root/.citychain/city/CityMain/city.conf

EXPOSE 4333 4334 4335 4336

CMD ["dotnet", "run"]
