FROM microsoft/dotnet:2.1-sdk

RUN git clone https://github.com/stratisproject/StratisBitcoinFullNode.git \
    && cd /StratisBitcoinFullNode/src/Stratis.BitcoinD \
	&& dotnet build
	
VOLUME /root/.stratisbitcoin

WORKDIR /StratisBitcoinFullNode/src/Stratis.BitcoinD

COPY bitcoin.conf.docker /root/.stratisnode/bitcoin/Main/bitcoin.conf

EXPOSE 8333 8332 18333 18332 37220

CMD ["dotnet", "run"]
