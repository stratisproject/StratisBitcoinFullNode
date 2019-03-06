#!/bin/sh
Message="Changing dir..."
NodesDirectory="$HOME/LocalSCNodes"
echo $Message
cd ../..
git checkout release/3.0.1.0-LSC
cd src/Stratis.LocalSmartContracts.MinerD
echo "Running miner 1..."
echo "**Data held in $NodesDirectory/miner1**"
dotnet run -datadir=$NodesDirectory/miner1 -port=36201 -apiport=38201 -txindex=1 connect=0 listen=1 -bind=127.0.0.1