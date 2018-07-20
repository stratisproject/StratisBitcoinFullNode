FROM microsoft/dotnet:2.1-sdk

RUN git clone https://github.com/stratisproject/StratisBitcoinFullNode.git \
    && cd /StratisBitcoinFullNode/src/Stratis.StratisD \
    && dotnet build
	
VOLUME /root/.stratisbitcoin

WORKDIR /StratisBitcoinFullNode/src/Stratis.StratisD

COPY stratis.conf.docker /root/.stratisnode/stratis/StratisTest/stratis.conf

EXPOSE 18444 18442 26174 26178 38221

CMD ["dotnet", "run", "-testnet"]
