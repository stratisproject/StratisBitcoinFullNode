Stratis Bitcoin
===============

An implementation of the Bitcoin protocol in C#.
Bitcoin full node written in C# for the .NET Core platform, based on NBitcoin.

The daemon is a full implementation of the Stratis Bitcoin Full Node, with the following characteristics:


NOTE: Some of these are in the NBitcoin project.


Getting started
---------------

1. Clone the repository

2. You will need .NET Core 1.1

3. Build, and check the tests pass

4. You can also check the node at least runs

5. Publish the Stratis.BitcoinD project

   This will create output under bin\Release\PublishOutput

6. Copy the output to your server, e.g. to C:\StratisBitcoin

7. Create a data directory, e.g. C:\StratisBitcoinData

8. Run the node, e.g. on testnet:

   dotnet .\Stratis.BitcoinD.dll -testnet -datadir=C:\StratisBitcoinData

   (Note it may take a while to discover peers.)


Enabling RPC
------------

1. Edit the bitcoin.conf file in you data directory (C:\StratisBitcoinData)

2. Change the server setting to activate the RPC server:

   server=1

3. Also add the following two values (use a proper password for a production system):

   rpcuser=bitcoinrpc
   rpcpassword=testnetpassword

4. Restart the node

5. Find the bitcoin-cli.exe tool, which should be in:

   Stratis.Bitcoin.Tests\TestData\bitcoin-0.13.1\bin

6. Use it to test the server is working:

   .\bitcoin-cli.exe -testnet -rpcuser=bitcoinrpc -rpcpassword=testnetpassword getblockhash

