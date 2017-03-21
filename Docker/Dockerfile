FROM stratisplatform/dotnet-build:1.0.0-preview2-1-003177

MAINTAINER Dan Gershony - Stratis Platform <dan.gershony@gmail.com>

RUN git clone https://github.com/stratisproject/StratisBitcoinFullNode.git \
	&& cd  StratisBitcoinFullNode \
	&& dotnet restore
	
VOLUME /root/.stratisbitcoin

WORKDIR /StratisBitcoinFullNode/Stratis.BitcoinD

EXPOSE 8333 8332

CMD ["dotnet", "run"]
