Stratis.Bitcoin
===============

Bitcoin full node written in C# for the .NET Core platform, based on NBitcoin.

The daemon is a full implementation of the Stratis Bitcoin Full Node, with the following characteristics:

* Full blockchain validation
* Blockchain database
* Mempool
* Wallet system & wallet database (HD keys w/ bip44 derivation)
* Bitcoind-compatible JSON rpc api
* A TransactionBuilder supporting Stealth, Open Asset, and all standard transactions
* Full script evaluation and parsing
* A SPV Wallet implementation with sample
* The parsing of standard scripts and creation of custom ones
* The serialization of blocks, transactions and script
* The signing and verification with private keys (with support for compact signatures) for proving ownership
* Bloom filters and partial merkle trees
* Segregated Witness (BIP 141, BIP 143, BIP 144)
* Mnemonic code for generating deterministic keys (BIP 39), credits to Thasshiznets
* Hierarchical Deterministic Wallets (BIP 32)
* Payment Protocol (BIP 70)
* Payment URLs (BIP 21,BIP 72)
* Two-Factor keys (BIP 38)
* Stealth Addresses (Also on codeproject)

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

