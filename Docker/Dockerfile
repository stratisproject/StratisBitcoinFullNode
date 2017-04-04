FROM stratisplatform/dotnet-build:1.0.0-preview2-1-003177

MAINTAINER Dan Gershony - Stratis Platform <dan.gershony@stratisplatform.com>

RUN git clone https://github.com/stratisproject/StratisBitcoinFullNode.git \
	&& cd  StratisBitcoinFullNode \
	&& dotnet restore \
	&& cd Stratis.BitcoinD \
	&& dotnet build
	
VOLUME /root/.stratisbitcoin

WORKDIR /StratisBitcoinFullNode/Stratis.BitcoinD

EXPOSE 8333 8332 18333 18332

CMD ["dotnet", "run"]
