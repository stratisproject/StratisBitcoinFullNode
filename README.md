Stratis.Bitcoin
===============

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

