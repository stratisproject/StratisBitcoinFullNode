

# Getting started - Building and running a Stratis Full Node 

---------------

## Supported Platforms

* <b>Windows</b> - works from Windows 7 and later, on both x86 and x64 architecture. Most of the development and testing is happening here.
* <b>Linux</b> - works and Ubuntu 14.04 and later (x64). It's been known to run on some other distros so your mileage may vary.
* <b>MacOS</b> - works from OSX 10.12 and later. 

## Prerequisites

To install and run the node, you need
* [.NET Core 2.1](https://www.microsoft.com/net/download/core)
* [Git](https://git-scm.com/)

## Build instructions

### Get the repository and its dependencies

```
git clone https://github.com/stratisproject/StratisBitcoinFullNode.git  
cd StratisBitcoinFullNode/src
```

### Build and run the code
With this node, you can connect to either the Stratis network or the Bitcoin network, either on MainNet or TestNet.
So you have 4 options:

1. To run a <b>Stratis</b> node on <b>MainNet</b>, do
```
cd Stratis.StratisD
dotnet run
```  

2. To run a <b>Stratis</b>  node on <b>TestNet</b>, do
```
cd Stratis.StratisD
dotnet run -testnet
```  

3. To run a <b>Bitcoin</b> node on <b>MainNet</b>, do
```
cd Stratis.BitcoinD
dotnet run
```  

4. To run a <b>Bitcoin</b> node on <b>TestNet</b>, do
```
cd Stratis.BitcoinD
dotnet run -testnet
```  

### Advanced options

You can get a list of command line arguments to pass to the node with the -help command line argument. For example:
```
cd Stratis.StratisD
dotnet run -help
```  

### Script
We have a nifty little script that can execute all the previous commands for you, including starting the node.  
You just need to edit the file and specify whether you want to run a Stratis or a Bitcoin node, on MainNet or Testnet.  
It's located [here](https://gist.github.com/bokobza/e68832f5d7d4102bcb33fcde8d9a72fb#file-build-and-run-a-stratis-node-ps1).

### Faucet
If you need testnet funds (TSTRAT) for testing there is a faucet located [here](https://faucet.stratisplatform.com/).

Docker Containers
-------------------

Two containers are available [here](https://hub.docker.com/u/stratisplatform/)

- stratis-node: Run on the Bitcoin Main or Test networks
- stratis-node-sim: Join our simulation network

Swagger Endpoints
-------------------

Once the node is running, a Swagger interface (web UI for testing an API) is available.

* For Bitcoin: http://localhost:37220/swagger/
* For Stratis: http://localhost:37221/swagger/
* For Bitcoin Testnet: http://localhost:38220/swagger/
* For Stratis Testnet: http://localhost:38221/swagger/
